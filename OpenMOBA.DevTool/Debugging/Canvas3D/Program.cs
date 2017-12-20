using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using OpenMOBA.DevTool.Debugging.Canvas3D;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using Buffer = SharpDX.Direct3D11.Buffer;
using Color = SharpDX.Color;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.Direct3D11.Resource;

namespace Shade {
   public static class CanvasProgram {
      public static void EntryPoint(string[] args) {
         var renderForm = new RenderForm { ClientSize = new Size(1280, 720) };
         var graphicsDevice = Direct3DGraphicsDevice.Create(renderForm);
         var techniqueCollection = graphicsDevice.TechniqueCollection;
         var meshPresets = graphicsDevice.MeshPresets;
         var immediateContext = graphicsDevice.ImmediateContext;
         var renderer = new BatchedRenderer3D(graphicsDevice);

         graphicsDevice.ImmediateContext.SetVsyncEnabled(false);

         renderForm.Show();
         var start = DateTime.Now;
         using (var renderLoop = new RenderLoop(renderForm)) {
            while (renderLoop.NextFrame()) {
               graphicsDevice.DoEvents();

               var proj = MatrixCM.PerspectiveFovRH((float)Math.PI / 4.0f, 1280.0f / 720.0f, 0.1f, 100.0f);
               var view = MatrixCM.LookAtRH(new Vector3(3, 2.5f, 5), new Vector3(0, 0.5f, 0), new Vector3(0, 1, 0));
               var projView = proj * view;

               renderer.ClearScene();
               renderer.SetProjView(projView);
               renderer.AddRenderable(meshPresets.UnitCube, MatrixCM.Scaling(4f, 0.1f, 4f) * MatrixCM.Translation(0, -0.5f, 0) * MatrixCM.RotationX((float)Math.PI));
               renderer.AddRenderable(meshPresets.UnitCube, MatrixCM.Translation(0, 0.5f, 0));

               var dt = (float)(DateTime.Now - start).TotalSeconds / 4;
               for (var i = 0; i < 10; i++) {
                  renderer.AddRenderable(
                     meshPresets.UnitCube,
                     MatrixCM.RotationY(2 * (float)Math.PI * i / 10.0f + dt * (float)Math.PI) * MatrixCM.Translation(1.0f, 0.9f + 0.4f * (float)Math.Sin(8 * Math.PI * i / 10.0), 0) * MatrixCM.Scaling(0.2f) * MatrixCM.RotationY(i)
                  );
               }

               renderer.AddSpotlight(
                  new Vector3(5, 4, 3), new Vector3(0, 0, 0), (float)Math.PI / 8.0f,
                  Color.White, 100.0f,
                  0.0f, 6.0f, 3.0f,
                  0.5f / 256.0f);
               renderer.AddSpotlight(new Vector3(5, 4, -5), new Vector3(0, 0, 0), (float)Math.PI / 8.0f, Color.Red, 100.0f, 3.0f, 6.0f, 1.0f);
               renderer.RenderScene();
            }
         }
      }
   }
}
//      public static void EntryPoint(string[] args) {
//         var renderForm = new RenderForm { ClientSize = new Size(1280, 720) };
//         var graphicsDevice = Direct3DGraphicsDevice.Create(renderForm);
//         var techniqueCollection = graphicsDevice.TechniqueCollection;
//         var meshPresets = graphicsDevice.MeshPresets;
//
//         var constantBuffer = new Buffer(
//            graphicsDevice.InternalD3DDevice, 
//            3 * Utilities.SizeOf<Matrix>(),
//            ResourceUsage.Default, 
//            BindFlags.ConstantBuffer, 
//            CpuAccessFlags.None, 
//            ResourceOptionFlags.None,
//            0);
//
//         var shadowMapEntriesBufferLength = 16;
//         var shadowMapEntriesBuffer = new Buffer(
//            graphicsDevice.InternalD3DDevice,
//            shadowMapEntriesBufferLength * ShadowMapEntry.SIZE,
//            ResourceUsage.Dynamic,
//            BindFlags.ShaderResource,
//            CpuAccessFlags.Write,
//            ResourceOptionFlags.BufferStructured,
//            ShadowMapEntry.SIZE);
//         var shadowMapEntriesBufferSrv = new ShaderResourceView(
//            graphicsDevice.InternalD3DDevice, 
//            shadowMapEntriesBuffer,
//            new ShaderResourceViewDescription{
//               Dimension = ShaderResourceViewDimension.Buffer,
//               Format = Format.Unknown,
//               Buffer = {
//                  ElementCount = shadowMapEntriesBufferLength,
//                  FirstElement = 0
//               }
//            });
//         //         var light0ProjViewWorldConstantBuffer = new Buffer(graphicsDevice.InternalD3DDevice, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
//         var shadowMapResolution = new Size(2048, 2048);
//         var shadowMapBufferCount = 10;
//         var lightDepthBuffer = new Texture2D(graphicsDevice.InternalD3DDevice,
//            new Texture2DDescription {
//               Format = Format.R16_Typeless,
//               ArraySize = shadowMapBufferCount,
//               MipLevels = 1,
//               Width = shadowMapResolution.Width,
//               Height = shadowMapResolution.Height,
//               SampleDescription = new SampleDescription(1, 0),
//               Usage = ResourceUsage.Default,
//               BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
//               CpuAccessFlags = CpuAccessFlags.None,
//               OptionFlags = ResourceOptionFlags.None
//            });
//         var lightDepthStencilViews = new DepthStencilView[shadowMapBufferCount];
//         for (var i = 0; i < shadowMapBufferCount; i++) {
//            lightDepthStencilViews[i] = new DepthStencilView(graphicsDevice.InternalD3DDevice, lightDepthBuffer,
//               new DepthStencilViewDescription {
//                  Format = Format.D16_UNorm,
//                  Dimension = DepthStencilViewDimension.Texture2DArray,
//                  Texture2DArray = {
//                     ArraySize = 1,
//                     FirstArraySlice = 0,
//                     MipSlice = 0
//                  }
//               });
//         }
//         var lightShaderResourceView = new ShaderResourceView(graphicsDevice.InternalD3DDevice, lightDepthBuffer,
//            new ShaderResourceViewDescription{
//               Format = Format.R16_UNorm,
//               Dimension = ShaderResourceViewDimension.Texture2DArray,
//               Texture2DArray = {
//                  MipLevels = 1,
//                  MostDetailedMip = 0,
//                  ArraySize = shadowMapBufferCount,
//                  FirstArraySlice = 0
//               }
//            });
//         renderForm.Show();
//         var start = DateTime.Now;
//         var random = new Random();
//         using (var renderLoop = new RenderLoop(renderForm)) {
//            while (renderLoop.NextFrame() || true) {
//               graphicsDevice.DoEvents();
//
//               var context = graphicsDevice.ImmediateContext;
//
//               //----------------------------------------------------------------------------------
//               // Render as Light
//               //----------------------------------------------------------------------------------
//               var dt = (float)(DateTime.Now - start).TotalSeconds - 22f;
//               var lightPositionR = 2.0f + 1.0f * (float)Math.Sin(dt / 2);
//               var lightPositionTheta = dt / 7.0f;
//               var lightPositionY = 3.0f + (float)Math.Sin(dt / 3.0);
//               var lightPosition = new Vector3(lightPositionR, lightPositionY, 0);
//               lightPosition = Vector3.Transform(lightPosition, Matrix3x3.RotationY(lightPositionTheta));
//               var lightLookat = new Vector3(0, 0, 0);
//               var lightUp = new Vector3(0, 1, 0);
//
//               var lightView = MatrixCM.LookAtRH(lightPosition, lightLookat, lightUp);
//               var lightProj = MatrixCM.PerspectiveFovRH((float)Math.PI / 4.0f, 1.0f, 0.1f, 100.0f);
//               var lightProjView = lightProj * lightView;
//
//               var x = new ShadowMapEntry[16];
//               x[0].Location = new AtlasLocation { Position = new Vector3(0, 0, 0), Size = new Vector2(1.0f, 1.0f) };
//               x[0].ProjViewWorld = lightProjView;
//               x[0].Color = Color4.White;
//               var box = graphicsDevice.InternalD3DDevice.ImmediateContext.MapSubresource(shadowMapEntriesBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
//               var cur = box.DataPointer;
//               for (var i = 0; i < x.Length; i++)
//                  cur = Utilities.WriteAndPosition(cur, ref x[i]);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.UnmapSubresource(shadowMapEntriesBuffer, 0);
////               graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(x, shadowMapEntriesBuffer, 0);
//
//               DepthStencilView oldDsv;
//               var oldRtvs = graphicsDevice.InternalD3DDevice.ImmediateContext.OutputMerger.GetRenderTargets(2, out oldDsv);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.ClearDepthStencilView(lightDepthStencilViews[0], DepthStencilClearFlags.Depth, 1.0f, 0);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.OutputMerger.SetTargets(lightDepthStencilViews[0]);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.Rasterizer.SetViewport(new Viewport(0, 0, shadowMapResolution.Width, shadowMapResolution.Height, 0.0f, 1.0f));
//               techniqueCollection.DefaultPositionColorTexture.BeginPass(graphicsDevice.ImmediateContext, 0);
//               {
//                  var cubeWorld = Matrix.Identity;
//                  var cubeProjViewWorld = lightProjView * cubeWorld;
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
//                     0, new VertexBufferBinding(cubeBuffer, VertexPositionColor.Size, 0));
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref cubeProjViewWorld, constantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
//                  //                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, null);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UnmapSubresource(constantBuffer, 1);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(36, 0);
//               }
//               {
//                  var planeWorld = MatrixCM.Translation(0, -1f, 0) * MatrixCM.Scaling(4) * MatrixCM.RotationX((float)Math.PI / 2);
//                  var planeProjViewWorld = lightProjView * planeWorld;
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
//                     0, new VertexBufferBinding(planeXYBuffer, VertexPositionColor.Size, 0));
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref planeProjViewWorld, constantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
//                  //                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, null);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UnmapSubresource(constantBuffer, 1);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(12, 0);
//               }
//
//               graphicsDevice.InternalD3DDevice.ImmediateContext.OutputMerger.SetTargets(oldDsv, oldRtvs);
//
//               //----------------------------------------------------------------------------------
//               // Render as Camera
//               //----------------------------------------------------------------------------------
//               context.ClearTargetAndDepthBuffers(Color.Black);
//               
//               var time = (float)(DateTime.Now - start).TotalSeconds * 0;
//               var position = new Vector3(0, 4, 8);
//
//               var lookat = 0 * new Vector3(1, 1, 1) / 2;
//               var up = new Vector3(0, 1, 0);
//
//               var view = MatrixCM.LookAtRH(Vector3.Transform(position, Matrix3x3.RotationY(time)), lookat, up);
//               var proj = MatrixCM.PerspectiveFovRH((float)Math.PI / 4.0f, 1280.0f / 720.0f, 0.1f, 100.0f);
//               var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, 1280.0f, 720.0f, 0.0f, 0.1f, 100.0f); // top-left origin
//               var projView = proj * view;
//
//               //               graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, light0ProjViewWorldConstantBuffer);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
//               graphicsDevice.InternalD3DDevice.ImmediateContext.Rasterizer.SetViewport(new Viewport(0, 0, 1280, 720, 0.0f, 1.0f));
//               techniqueCollection.DefaultPositionColorShadow.BeginPass(context, 0);
//               {
//                  var cubeWorld = Matrix.Identity;
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
//                     0, new VertexBufferBinding(cubeBuffer, VertexPositionColor.Size, 0));
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(new[] { projView, lightProjView, cubeWorld }, constantBuffer, 0);
////                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref cubeProjViewWorld, constantBuffer, 0);
////                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, constantBuffer, 1);
//                  //                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, light0ProjViewWorldConstantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(10, lightShaderResourceView);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(11, shadowMapEntriesBufferSrv);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetConstantBuffer(0, constantBuffer);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(36, 0);
//               }
//               {
//                  var planeWorld = MatrixCM.Translation(0, -0.5f, 0) * MatrixCM.Scaling(4) * MatrixCM.RotationX((float)Math.PI / 2);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
//                     0, new VertexBufferBinding(planeXYBuffer, VertexPositionColor.Size, 0));
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(new[] { projView, lightProjView, planeWorld }, constantBuffer, 0);
////                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref planeProjViewWorld, constantBuffer, 0);
////                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, constantBuffer, 1);
////                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, light0ProjViewWorldConstantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(10, lightShaderResourceView);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetConstantBuffer(0, constantBuffer);
////                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(11, shadowMapEntriesBuffer);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(12, 0);
//               }
//
//               graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
////               graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, light0ProjViewWorldConstantBuffer);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.Rasterizer.SetViewport(new Viewport(0, 0, 1280, 720, 0.0f, 1.0f));
//               techniqueCollection.DefaultPositionColorTexture.BeginPass(context, 0);
//               {
//                  var quadWorld = MatrixCM.Scaling(512, 512, 0);
//                  var quadProjViewWorld = orthoProj * quadWorld;
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref quadProjViewWorld, constantBuffer, 0);
////                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref quadProjViewWorld, light0ProjViewWorldConstantBuffer, 1);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(0, lightShaderResourceView);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
//                     0, new VertexBufferBinding(quad1x1Buffer, VertexPositionColorTexture.Size, 0));
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(12, 0);
//                  //quad1x1Buffer
//               }
//
//               context.Present();
//            }
//         }
//      }
//   }

//   public class CanvasProgramImpl : CanvasProgram {
//      private const double kScale = 30;
//      private List<DelaunayTriangle> triangulation;
//
//      public override void Setup() {
//         base.Setup();
//
//         var land = new List<List<TriangulationPoint>> {
//            GeometryFactory.Rectangle(1, 1, 8, 8),
//            GeometryFactory.Rectangle(9, 4, 2, 2),
//            GeometryFactory.Rectangle(11, 1, 8, 8),
//            GeometryFactory.Rectangle(14, 9, 2, 2),
//            GeometryFactory.Rectangle(11, 11, 8, 8)
//         };
//         var holes = new List<List<TriangulationPoint>> {
//            GeometryFactory.Rectangle(3, 3, 4, 4),
//            GeometryFactory.Rectangle(12.6, 2.6, 1.6, 1.6),
//            GeometryFactory.Rectangle(15.8, 2.6, 1.6, 1.6),
//            GeometryFactory.Rectangle(12.6, 5.8, 1.6, 1.6),
//            GeometryFactory.Rectangle(15.8, 5.8, 1.6, 1.6),
//            GeometryFactory.Rectangle(11, 11, 1.6, 1.6),
//            GeometryFactory.Rectangle(17.4, 11, 1.6, 1.6),
//            GeometryFactory.Rectangle(17.4, 17.4, 1.6, 1.6),
//            GeometryFactory.Rectangle(11, 17.4, 1.6, 1.6),
//            GeometryFactory.Rectangle(13, 14, 4, 2),
//            GeometryFactory.Rectangle(14, 13, 2, 4)
//         };
//         var blockers = new List<List<TriangulationPoint>> {
////            GeometryFactory.Rectangle(9, 4, 6, 1)
//         };
//
//         triangulation = new Triangulator().TriangulateNavigationMesh(land, holes, blockers, 0);
//      }
//
//      public override void Render(GameTime gameTime) {
//         base.Render(gameTime);
//
//         Canvas.Clear(Color.FromBgra(0xFF000000 + 0x010101 * 10));
//         
//         foreach (var triangle in triangulation) {
//            Canvas.BeginPath();
//            Canvas.SetLineStyle(1, Color.Cyan);
//            Canvas.MoveTo(triangle.Points[0].X * kScale, triangle.Points[0].Y * kScale);
//            Canvas.LineTo(triangle.Points[1].X * kScale, triangle.Points[1].Y * kScale);
//            Canvas.LineTo(triangle.Points[2].X * kScale, triangle.Points[2].Y * kScale);
//            Canvas.LineTo(triangle.Points[0].X * kScale, triangle.Points[0].Y * kScale);
//            Canvas.FillPath(Color.White);
//            Canvas.Stroke();
//
//            Canvas.BeginPath();
//            Canvas.SetLineStyle(1, Color.Red);
//            foreach (var neighbor in triangle.Neighbors.Where(n => n != null)) {
//               if (neighbor.IsInterior) {
//                  Canvas.MoveTo(triangle.Centroid().X * kScale, triangle.Centroid().Y * kScale);
//                  Canvas.LineTo(neighbor.Centroid().X * kScale, neighbor.Centroid().Y * kScale);
//               }
//            }
//            Canvas.Stroke();
//         }
//      }
//   }

//   public class DynamicElement : Canvas {
//      public DynamicElement(int width, int height) : base(width, height) {
//
//      }
//   }

//   public class TriangulationElement : DynamicElement {
//      private readonly List<DelaunayTriangle> triangles;
//
//      public TriangulationElement(List<DelaunayTriangle> triangles) {
//         Step += HandleStep;
//      }
//
//      private void HandleStep() {
//         foreach (var triangle in triangles) {
//            var points = triangle.Points;
//            BeginPath();
//            SetLineStyle(1, 0x00FF00);
//            MoveTo(points[0].X, points[0].Y);
//            LineTo(points[1].X, points[1].Y);
//            LineTo(points[2].X, points[2].Y);
//            LineTo(points[0].X, points[0].Y);
//            Stroke();
//         }
//      }
//   }

//   public class GeometryLayer2D {
//
//   }
//}
