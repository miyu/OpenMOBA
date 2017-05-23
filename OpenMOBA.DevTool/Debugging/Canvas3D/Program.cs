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
   public interface ITechnique {
      int Passes { get; set; }
      void BeginPass(IRenderContext renderContext, int pass);
   }

   public interface ITechniqueCollection {
      ITechnique DefaultPositionColor { get; }
      ITechnique DefaultPositionColorShadow { get; }
      ITechnique DefaultPositionColorTexture { get; }
      ITechnique DefaultPositionColorTextureDerivative { get; }
   }
   public class DefaultTechniqueCollectionImpl : ITechniqueCollection {
      public ITechnique DefaultPositionColor { get; private set; }
      public ITechnique DefaultPositionColorShadow { get; private set; }
      public ITechnique DefaultPositionColorTexture { get; private set; }
      public ITechnique DefaultPositionColorTextureDerivative { get; private set; }

      public static DefaultTechniqueCollectionImpl Create(IAssetManager assetManager) {
         var collection = new DefaultTechniqueCollectionImpl();
         collection.DefaultPositionColor = new Technique {
            PixelShader = assetManager.LoadPixelShaderFromFile("shaders/defaultPositionColor", "PSMain"),
            VertexShader = assetManager.LoadVertexShaderFromFile("shaders/defaultPositionColor", InputLayoutType.PositionColor, "VSMain")
         };
         collection.DefaultPositionColorShadow = new Technique {
            PixelShader = assetManager.LoadPixelShaderFromFile("shaders/defaultPositionColorShadow", "PSMain"),
            VertexShader = assetManager.LoadVertexShaderFromFile("shaders/defaultPositionColorShadow", InputLayoutType.PositionColor, "VSMain")
         };
         collection.DefaultPositionColorTexture = new Technique {
            PixelShader = assetManager.LoadPixelShaderFromFile("shaders/defaultPositionColorTexture", "PSMain"),
            VertexShader = assetManager.LoadVertexShaderFromFile("shaders/defaultPositionColorTexture", InputLayoutType.PositionColorTexture, "VSMain")
         };
         collection.DefaultPositionColorTextureDerivative = new Technique {
            PixelShader = assetManager.LoadPixelShaderFromFile("shaders/defaultPositionColorTextureDerivative", "PSMain"),
            VertexShader = assetManager.LoadVertexShaderFromFile("shaders/defaultPositionColorTextureDerivative", InputLayoutType.PositionColorTexture, "VSMain")
         };
         return collection;
      }

      private class Technique : ITechnique {
         public IPixelShader PixelShader { get; set; }
         public IVertexShader VertexShader { get; set; }

         public int Passes { get; set; }

         public void BeginPass(IRenderContext renderContext, int pass) {
            renderContext.SetPixelShader(PixelShader);
            renderContext.SetVertexShader(VertexShader);
         }
      }
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct AtlasLocation {
      public Vector3 Position;
      public Vector2 Size;
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct ShadowMapEntry {
      public AtlasLocation Location;
      public Matrix ProjViewWorld;
      public Vector4 Color;

      public const int SIZE = 4 * (3 + 2) + 4 * 4 * 4 + 4 * 4;
   }

   public static class CanvasProgram {
      public static void EntryPoint(string[] args) {
         var renderForm = new RenderForm { ClientSize = new Size(1280, 720) };
         var graphicsDevice = Direct3DGraphicsDevice.Create(renderForm);
         var techniqueCollection = (ITechniqueCollection)DefaultTechniqueCollectionImpl.Create(graphicsDevice.AssetManager);

         var cubeVertices = new[] {
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Red), // Front
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Red),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Red),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Red),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Red),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Red),

            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Lime), // BACK
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Lime),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Lime),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Lime),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Lime),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Lime),

            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Blue), // Top
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Blue),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Blue),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Blue),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Blue),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Blue),

            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Yellow), // Bottom
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Yellow),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Yellow),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Yellow),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Yellow),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Yellow),

            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Magenta), // Left
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Magenta),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Magenta),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Magenta),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Magenta),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Magenta),

            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Cyan), // Right
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Cyan),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Cyan),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Cyan),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Cyan),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Cyan),
         };

         // Switch cube from LHS to RHS coordinate system. Flip clockness of verts, 
         for (var i = 0; i < cubeVertices.Length; i += 3) {
            // // Flip vert clockness
            // var temp = cubeVertices[i + 1];
            // cubeVertices[i + 1] = cubeVertices[i + 2];
            // cubeVertices[i + 2] = temp;

            // Flip Zs, which effectively flips clockness. Important because we have
            // colored left/right which will be flipped if we just flip clockness as above.
            cubeVertices[i].Position.Z *= -1.0f;
            cubeVertices[i + 1].Position.Z *= -1.0f;
            cubeVertices[i + 2].Position.Z *= -1.0f;
            
            // cubeVertices[i].Color = Color.White;
            // cubeVertices[i + 1].Color = Color.White;
            // cubeVertices[i + 2].Color = Color.White;
         }

         var cubeBuffer = Buffer.Create(graphicsDevice.InternalD3DDevice, BindFlags.VertexBuffer, cubeVertices);

         var planeXYBuffer = Buffer.Create(graphicsDevice.InternalD3DDevice, BindFlags.VertexBuffer, new[] {
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), Color.White), // Back
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White),

            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), Color.White), // Front
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White),
            new Direct3DVertexPositionColor(0.5f * new Vector3(1.0f, -1.0f, 0.0f), Color.White)
         });

         var quad1x1Buffer = Buffer.Create(graphicsDevice.InternalD3DDevice, BindFlags.VertexBuffer, new[] {
            new Direct3DVertexPositionColorTexture(new Vector3(0, 1.0f, 0.0f), Color.White, new Vector2(0, 1)), // Back
            new Direct3DVertexPositionColorTexture(new Vector3(0, 0, 0.0f), Color.White, new Vector2(0, 0)),
            new Direct3DVertexPositionColorTexture(new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),
            new Direct3DVertexPositionColorTexture(new Vector3(0, 0, 0.0f), Color.White, new Vector2(0, 0)),
            new Direct3DVertexPositionColorTexture(new Vector3(1.0f, 0, 0.0f), Color.White, new Vector2(1, 0)),
            new Direct3DVertexPositionColorTexture(new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),

            new Direct3DVertexPositionColorTexture(new Vector3(0, 1.0f, 0.0f), Color.White, new Vector2(0, 1)), // Front
            new Direct3DVertexPositionColorTexture(new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),
            new Direct3DVertexPositionColorTexture(new Vector3(0, 0, 0.0f), Color.White, new Vector2(0, 0)),
            new Direct3DVertexPositionColorTexture(new Vector3(0, 0, 0.0f), Color.White, new Vector2(0, 0)),
            new Direct3DVertexPositionColorTexture(new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),
            new Direct3DVertexPositionColorTexture(new Vector3(1.0f, 0, 0.0f), Color.White, new Vector2(1, 0))
         });

         var constantBuffer = new Buffer(
            graphicsDevice.InternalD3DDevice, 
            3 * Utilities.SizeOf<Matrix>(),
            ResourceUsage.Default, 
            BindFlags.ConstantBuffer, 
            CpuAccessFlags.None, 
            ResourceOptionFlags.None,
            0);

         var shadowMapEntriesBufferLength = 16;
         var shadowMapEntriesBuffer = new Buffer(
            graphicsDevice.InternalD3DDevice,
            shadowMapEntriesBufferLength * ShadowMapEntry.SIZE,
            ResourceUsage.Dynamic,
            BindFlags.ShaderResource,
            CpuAccessFlags.Write,
            ResourceOptionFlags.BufferStructured,
            ShadowMapEntry.SIZE);
         var shadowMapEntriesBufferSrv = new ShaderResourceView(
            graphicsDevice.InternalD3DDevice, 
            shadowMapEntriesBuffer,
            new ShaderResourceViewDescription{
               Dimension = ShaderResourceViewDimension.Buffer,
               Format = Format.Unknown,
               Buffer = {
                  ElementCount = shadowMapEntriesBufferLength,
                  FirstElement = 0
               }
            });
         //         var light0ProjViewWorldConstantBuffer = new Buffer(graphicsDevice.InternalD3DDevice, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
         var shadowMapResolution = new Size(2048, 2048);
         var arraySize = 10;
         var lightDepthBuffer = new Texture2D(graphicsDevice.InternalD3DDevice,
            new Texture2DDescription {
               Format = Format.R16_Typeless,
               ArraySize = arraySize,
               MipLevels = 1,
               Width = shadowMapResolution.Width,
               Height = shadowMapResolution.Height,
               SampleDescription = new SampleDescription(1, 0),
               Usage = ResourceUsage.Default,
               BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
               CpuAccessFlags = CpuAccessFlags.None,
               OptionFlags = ResourceOptionFlags.None
            });
         var lightDepthStencilView = new DepthStencilView(graphicsDevice.InternalD3DDevice, lightDepthBuffer, 
            new DepthStencilViewDescription {
               Format = Format.D16_UNorm,
               Dimension = DepthStencilViewDimension.Texture2DArray,
               Texture2DArray = {
                  ArraySize = 1,
                  FirstArraySlice = 0,
                  MipSlice = 0
               }
            });
         var lightShaderResourceView = new ShaderResourceView(graphicsDevice.InternalD3DDevice, lightDepthBuffer,
            new ShaderResourceViewDescription{
               Format = Format.R16_UNorm,
               Dimension = ShaderResourceViewDimension.Texture2DArray,
               Texture2DArray = {
                  MipLevels = 1,
                  MostDetailedMip = 0,
                  ArraySize = arraySize,
                  FirstArraySlice = 0
               }
            });
         renderForm.Show();
         var start = DateTime.Now;
         var random = new Random();
         using (var renderLoop = new RenderLoop(renderForm)) {
            while (renderLoop.NextFrame() || true) {
               graphicsDevice.DoEvents();

               var context = graphicsDevice.RenderContext;

               //----------------------------------------------------------------------------------
               // Render as Light
               //----------------------------------------------------------------------------------
               var dt = (float)(DateTime.Now - start).TotalSeconds - 22f;
               var lightPositionR = 2.0f + 1.0f * (float)Math.Sin(dt / 2);
               var lightPositionTheta = dt / 7.0f;
               var lightPositionY = 3.0f + (float)Math.Sin(dt / 3.0);
               var lightPosition = new Vector3(lightPositionR, lightPositionY, 0);
               lightPosition = Vector3.Transform(lightPosition, Matrix3x3.RotationY(lightPositionTheta));
               var lightLookat = new Vector3(0, 0, 0);
               var lightUp = new Vector3(0, 1, 0);

               var lightView = MatrixCM.LookAtRH(lightPosition, lightLookat, lightUp);
               var lightProj = MatrixCM.PerspectiveFovRH((float)Math.PI / 4.0f, 1.0f, 0.1f, 100.0f);
               var lightProjView = lightProj * lightView;

               var x = new ShadowMapEntry[16];
               x[0].Location = new AtlasLocation { Position = new Vector3(0, 0, 0), Size = new Vector2(1.0f, 1.0f) };
               x[0].ProjViewWorld = lightProjView;
               x[0].Color = Color4.White;
               var box = graphicsDevice.InternalD3DDevice.ImmediateContext.MapSubresource(shadowMapEntriesBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
               var cur = box.DataPointer;
               for (var i = 0; i < x.Length; i++)
                  cur = Utilities.WriteAndPosition(cur, ref x[i]);
               graphicsDevice.InternalD3DDevice.ImmediateContext.UnmapSubresource(shadowMapEntriesBuffer, 0);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(x, shadowMapEntriesBuffer, 0);

               DepthStencilView oldDsv;
               var oldRtvs = graphicsDevice.InternalD3DDevice.ImmediateContext.OutputMerger.GetRenderTargets(2, out oldDsv);
               graphicsDevice.InternalD3DDevice.ImmediateContext.ClearDepthStencilView(lightDepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
               graphicsDevice.InternalD3DDevice.ImmediateContext.OutputMerger.SetTargets(lightDepthStencilView);
               graphicsDevice.InternalD3DDevice.ImmediateContext.Rasterizer.SetViewport(new Viewport(0, 0, shadowMapResolution.Width, shadowMapResolution.Height, 0.0f, 1.0f));
               techniqueCollection.DefaultPositionColorTexture.BeginPass(graphicsDevice.RenderContext, 0);
               {
                  var cubeWorld = Matrix.Identity;
                  var cubeProjViewWorld = lightProjView * cubeWorld;
                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
                     0, new VertexBufferBinding(cubeBuffer, Direct3DVertexPositionColor.Size, 0));
                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref cubeProjViewWorld, constantBuffer, 0);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
                  //                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, null);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.UnmapSubresource(constantBuffer, 1);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(36, 0);
               }
               {
                  var planeWorld = MatrixCM.Translation(0, -1f, 0) * MatrixCM.Scaling(4) * MatrixCM.RotationX((float)Math.PI / 2);
                  var planeProjViewWorld = lightProjView * planeWorld;
                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
                     0, new VertexBufferBinding(planeXYBuffer, Direct3DVertexPositionColor.Size, 0));
                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref planeProjViewWorld, constantBuffer, 0);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
                  //                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, null);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.UnmapSubresource(constantBuffer, 1);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(12, 0);
               }

               graphicsDevice.InternalD3DDevice.ImmediateContext.OutputMerger.SetTargets(oldDsv, oldRtvs);

               //----------------------------------------------------------------------------------
               // Render as Camera
               //----------------------------------------------------------------------------------
               context.ClearTargetAndDepthBuffers(Color.Black);
               
               var time = (float)(DateTime.Now - start).TotalSeconds * 0;
               var position = new Vector3(0, 4, 8);

               var lookat = 0 * new Vector3(1, 1, 1) / 2;
               var up = new Vector3(0, 1, 0);

               var view = MatrixCM.LookAtRH(Vector3.Transform(position, Matrix3x3.RotationY(time)), lookat, up);
               var proj = MatrixCM.PerspectiveFovRH((float)Math.PI / 4.0f, 1280.0f / 720.0f, 0.1f, 100.0f);
               var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, 1280.0f, 720.0f, 0.0f, 0.1f, 100.0f); // top-left origin
               var projView = proj * view;

               //               graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, light0ProjViewWorldConstantBuffer);
               graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
               graphicsDevice.InternalD3DDevice.ImmediateContext.Rasterizer.SetViewport(new Viewport(0, 0, 1280, 720, 0.0f, 1.0f));
               techniqueCollection.DefaultPositionColorShadow.BeginPass(context, 0);
               {
                  var cubeWorld = Matrix.Identity;
                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
                     0, new VertexBufferBinding(cubeBuffer, Direct3DVertexPositionColor.Size, 0));
                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(new[] { projView, lightProjView, cubeWorld }, constantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref cubeProjViewWorld, constantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, constantBuffer, 1);
                  //                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, light0ProjViewWorldConstantBuffer, 0);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(10, lightShaderResourceView);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(11, shadowMapEntriesBufferSrv);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetConstantBuffer(0, constantBuffer);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(36, 0);
               }
               {
                  var planeWorld = MatrixCM.Translation(0, -0.5f, 0) * MatrixCM.Scaling(4) * MatrixCM.RotationX((float)Math.PI / 2);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
                     0, new VertexBufferBinding(planeXYBuffer, Direct3DVertexPositionColor.Size, 0));
                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(new[] { projView, lightProjView, planeWorld }, constantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref planeProjViewWorld, constantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, constantBuffer, 1);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref lightProjViewWorld, light0ProjViewWorldConstantBuffer, 0);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(10, lightShaderResourceView);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetConstantBuffer(0, constantBuffer);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(11, shadowMapEntriesBuffer);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(12, 0);
               }

               graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);
//               graphicsDevice.InternalD3DDevice.ImmediateContext.VertexShader.SetConstantBuffer(1, light0ProjViewWorldConstantBuffer);
               graphicsDevice.InternalD3DDevice.ImmediateContext.Rasterizer.SetViewport(new Viewport(0, 0, 1280, 720, 0.0f, 1.0f));
               techniqueCollection.DefaultPositionColorTexture.BeginPass(context, 0);
               {
                  var quadWorld = MatrixCM.Scaling(512, 512, 0);
                  var quadProjViewWorld = orthoProj * quadWorld;
                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref quadProjViewWorld, constantBuffer, 0);
//                  graphicsDevice.InternalD3DDevice.ImmediateContext.UpdateSubresource(ref quadProjViewWorld, light0ProjViewWorldConstantBuffer, 1);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.PixelShader.SetShaderResource(0, lightShaderResourceView);
                  graphicsDevice.InternalD3DDevice.ImmediateContext.InputAssembler.SetVertexBuffers(
                     0, new VertexBufferBinding(quad1x1Buffer, Direct3DVertexPositionColorTexture.Size, 0));
                  graphicsDevice.InternalD3DDevice.ImmediateContext.Draw(12, 0);
                  //quad1x1Buffer
               }

               context.Present();
            }
         }
      }
   }

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
}
