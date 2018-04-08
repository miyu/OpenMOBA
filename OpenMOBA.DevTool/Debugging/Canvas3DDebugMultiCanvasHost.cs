using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Canvas3D;
using Canvas3D.LowLevel;
using OpenMOBA.Geometry;
using SharpDX;
using Color = SharpDX.Color;
using Vector2 = SharpDX.Vector2;
using Vector3 = SharpDX.Vector3;
using Vector4 = SharpDX.Vector4;
using Quaternion = SharpDX.Quaternion;

namespace OpenMOBA.DevTool.Debugging {
   public class Canvas3DDebugMultiCanvasHost : IDebugMultiCanvasHost {
      private readonly ConcurrentQueue<IScene> sceneQueue;
      private readonly IGraphicsFacade graphicsFacade;
      private readonly IPresetsStore presets;

      public Canvas3DDebugMultiCanvasHost(ConcurrentQueue<IScene> sceneQueue, IGraphicsFacade graphicsFacade, IPresetsStore presets) {
         this.graphicsFacade = graphicsFacade;
         this.sceneQueue = sceneQueue;
         this.presets = presets;
      }

      public IDebugCanvas CreateAndAddCanvas(int timestamp) {
         var scene = new Scene();
         sceneQueue.Enqueue(scene);
         return new Canvas3DDebugCanvas(graphicsFacade, presets, scene);
      }

      private static Vector3 ToV3(DoubleVector3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

      public static Canvas3DDebugMultiCanvasHost CreateAndShowCanvas(Size size) {
         var sceneQueue = new ConcurrentQueue<IScene>();
         var scenes = new List<IScene>();
         var activeSceneIndex = -1;
         GraphicsLoop graphicsLoop = null;
         IPresetsStore presets = null;
         var initLatch = new ManualResetEvent(false);
         var thread = new Thread(() => {
            graphicsLoop = GraphicsLoop.CreateWithNewWindow(size, InitFlags.EnableDebugStats);
            presets = graphicsLoop.Presets;
            initLatch.Set();

            var rotation = 80 * Math.PI / 180.0;
            var lookat = new DoubleVector3(0, 0, 0);
            //var lookat = new DoubleVector3(0, 0, 0); 

            // originally offset -10, -100, 70)
            //var offset = new DoubleVector3(-100, 100, 200) * 7;// DoubleVector3.FromRadiusAngleAroundXAxis(400, rotation) + new DoubleVector3(100, -50, -100);
//            var offset = new DoubleVector3(-10, -100, 70) * 30;// DoubleVector3.FromRadiusAngleAroundXAxis(400, rotation) + new DoubleVector3(100, -50, -100);
//            var offset = new DoubleVector3(-10, -100, 30) * 30;// DoubleVector3.FromRadiusAngleAroundXAxis(400, rotation) + new DoubleVector3(100, -50, -100);
            var offset = new DoubleVector3(0, -30, 200) * 5;// DoubleVector3.FromRadiusAngleAroundXAxis(400, rotation) + new DoubleVector3(100, -50, -100);
            var up = new DoubleVector3(-1, 0, 0).Cross(offset).ToUnit(); //DoubleVector3.FromRadiusAngleAroundXAxis(1, rotation - Math.PI / 2);
            Console.WriteLine(offset);

            IScene lastScene = null;
            while (graphicsLoop.IsRunning(out var renderer)) {
               while (sceneQueue.TryDequeue(out var res)) {
                  res.SetCamera(Vector3.Zero, Matrix.Identity);
                  scenes.Add(res);
                  if (activeSceneIndex == scenes.Count - 2) {
                     activeSceneIndex = scenes.Count - 1;
                  }
               }

               var scene = activeSceneIndex == -1 ? new Scene() : scenes[activeSceneIndex];
               lock (scene) {
                  var view = MatrixCM.ViewLookAtRH(ToV3(lookat + offset), ToV3(lookat), ToV3(up));
                  var verticalFov = 105.0f * (float)Math.PI / 180.0f;
                  var aspect = size.Width / (float)size.Height;
                  var proj = MatrixCM.PerspectiveFovRH(verticalFov, aspect, 1.0f, 10000.0f);

                  void DrawAxes(Matrix transform, float scale = 1.0f) {
                     float length = 1.0f * scale;
                     float thickness = 0.06f * scale;
                     scene.AddRenderable(
                        graphicsLoop.Presets.UnitCube,
                        transform * MatrixCM.Translation(length / 2, 0, 0) * MatrixCM.Scaling(length, thickness, thickness),
                        new MaterialDescription {
                           Resources = { BaseColor = Color.Red },
                           Properties = { Metallic = 0.0f, Roughness = 0.04f },
                        });
                     scene.AddRenderable(
                        graphicsLoop.Presets.UnitCube,
                        transform * MatrixCM.Translation(0, length / 2, 0) * MatrixCM.Scaling(thickness, length, thickness),
                        new MaterialDescription {
                           Resources = { BaseColor = Color.Lime },
                           Properties = { Metallic = 0.0f, Roughness = 0.04f },
                        });
                     scene.AddRenderable(
                        graphicsLoop.Presets.UnitCube,
                        transform * MatrixCM.Translation(0, 0, length / 2) * MatrixCM.Scaling(thickness, thickness, length),
                        new MaterialDescription {
                           Resources = { BaseColor = Color.Blue },
                           Properties = { Metallic = 0.0f, Roughness = 0.04f },
                        });
                  }

                  if (scene != lastScene) {
                     lastScene = scene;

                     scene.SetCamera(ToV3(lookat + offset), Matrix.Multiply(proj, view));
                     scene.AddSpotlight(
                        ToV3(lookat + offset * 3),
                        ToV3(lookat),
                        ToV3(up),
                        (float)Math.PI * 0.49f,
                        Color.White,
                        100f,
                        100000.0f,
                        1, 1, 1000);
                     //DrawAxes(Matrix.Identity, 200);
                     DrawAxes(MatrixCM.Translation((float)lookat.X, (float)lookat.Y, (float)lookat.Z), 200);
                     //scene.AddRenderable(
                     //   graphicsLoop.Presets.UnitCube,
                     //   MatrixCM.Translation(1200, 500, 0) * MatrixCM.Scaling(100.0f),
                     //   new MaterialDescription {
                     //      Resources = { BaseColor = Color.White },
                     //      Properties = { Metallic = 0.0f, Roughness = 0.04f },
                     //   });
                  }
                  renderer.RenderScene(scene.ExportSnapshot());
               }
            }
         });
         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         initLatch.WaitOne();
         return new Canvas3DDebugMultiCanvasHost(sceneQueue, graphicsLoop.GraphicsFacade, presets);
      }

      public class Canvas3DDebugCanvas : IDebugCanvas {
         private readonly IGraphicsFacade graphicsFacade;
         private readonly IPresetsStore presets;
         private readonly Scene scene;
         private readonly IMesh<VertexPositionNormalColorTexture> unitTriangleMesh;

         private Matrix4x4 transformDotNet = Matrix4x4.Identity;
         private Matrix transformSharpDx = Matrix.Identity;

         public Canvas3DDebugCanvas(IGraphicsFacade graphicsFacade, IPresetsStore presets, Scene scene) {
            this.graphicsFacade = graphicsFacade;
            this.presets = presets;
            this.scene = scene;


            unitTriangleMesh = graphicsFacade.CreateMesh(new[] {
               new VertexPositionNormalColorTexture(new Vector3(0, 0, 0), -Vector3.UnitZ, Color.White, new Vector2(0, 0)),
               new VertexPositionNormalColorTexture(new Vector3(1, 0, 0), -Vector3.UnitZ, Color.White, new Vector2(1, 0)),
               new VertexPositionNormalColorTexture(new Vector3(0, 1, 0), -Vector3.UnitZ, Color.White, new Vector2(0, 1)),

               new VertexPositionNormalColorTexture(new Vector3(1, 0, 0), Vector3.UnitZ, Color.White, new Vector2(1, 0)),
               new VertexPositionNormalColorTexture(new Vector3(0, 0, 0), Vector3.UnitZ, Color.White, new Vector2(0, 0)),
               new VertexPositionNormalColorTexture(new Vector3(0, 1, 0), Vector3.UnitZ, Color.White, new Vector2(0, 1)),
            });
         }

         public IGraphicsFacade GraphicsFacade => graphicsFacade;
         public Scene Scene => scene;

         public Matrix4x4 Transform
         {
            get => transformDotNet;
            set
            {
               transformDotNet = value;
               transformSharpDx = new Matrix(
                  value.M11, value.M12, value.M13, value.M14,
                  value.M21, value.M22, value.M23, value.M24,
                  value.M31, value.M32, value.M33, value.M34,
                  value.M41, value.M42, value.M43, value.M44);
               //transformSharpDx.Transpose();
            }
         }

         public void BatchDraw(Action callback) {
            lock (scene) {
               callback();
            }
         }

         public void DrawPoint(DoubleVector3 p, StrokeStyle strokeStyle) {
            var center = (Vector3)Vector4.Transform(new Vector4(ToV3(p), 1), transformSharpDx);
            lock (scene) {
               var translate = MatrixCM.Translation(center.X, center.Y, center.Z);
               var scale = MatrixCM.Scaling((float)strokeStyle.Thickness);
               scene.AddRenderable(
                  presets.UnitSphere,
                  translate * scale,
                  new MaterialDescription {
                     Properties = new MaterialProperties { Metallic = 0, Roughness = 0.04f },
                     Resources = {
                        BaseColor = Color.FromBgra(strokeStyle.Color.ToArgb())
                     }
                  });
            }
         }

         public void DrawLine(DoubleVector3 p1, DoubleVector3 p2, StrokeStyle strokeStyle) {
            var from = (Vector3)Vector4.Transform(new Vector4(ToV3(p1), 1), transformSharpDx);
            var to = (Vector3)Vector4.Transform(new Vector4(ToV3(p2), 1), transformSharpDx);

            lock (scene) {
               var thicknessMult = 4;
               var scale = MatrixCM.Scaling((float)strokeStyle.Thickness * thicknessMult, (float)strokeStyle.Thickness * thicknessMult, (from - to).Length());
               //var orien = Quaternion.RotationLookAtRH(from - to, Vector3.Cross(from - to, Vector3.UnitZ));
               //var lookat = MatrixCM.RotationLookAtRH(Vector3.UnitY, Vector3.UnitZ);
               var lookat = MatrixCM.RotationLookAtRH(from - to, Vector3.UnitZ);
               var offset = MatrixCM.Translation((from.X + to.X) / 2, (from.Y + to.Y) / 2, (from.Z + to.Z) / 2);
               scene.AddRenderable(
                  presets.UnitCube,
                  Matrix.Multiply(offset, Matrix.Multiply(lookat, scale)),
                  new MaterialDescription{
                     Properties = new MaterialProperties { Metallic = 0, Roughness = 1.00f }
                  },
                  Color.FromBgra(strokeStyle.Color.ToArgb()));
            }
         }

         public void DrawTriangle(DoubleVector3 p1, DoubleVector3 p2, DoubleVector3 p3, StrokeStyle strokeStyle) {
            BatchDraw(() => {
               DrawLine(p1, p2, strokeStyle);
               DrawLine(p2, p3, strokeStyle);
               DrawLine(p3, p1, strokeStyle);
            });
         }

         public void FillTriangle(DoubleVector3 p1, DoubleVector3 p2, DoubleVector3 p3, FillStyle fillStyle) {
            var p1w = (Vector3)Vector4.Transform(new Vector4(ToV3(p1), 1), transformSharpDx);
            var p2w = (Vector3)Vector4.Transform(new Vector4(ToV3(p2), 1), transformSharpDx);
            var p3w = (Vector3)Vector4.Transform(new Vector4(ToV3(p3), 1), transformSharpDx);

            /**
             * [m00, m01, m02, m03] * [ 0 1 0 ] = [ orig.x, orig.x + v1.x, orig.x + v2.x]
             * [m10, m11, m12, m13]     0 0 1       orig.y, orig.y + v1.y, orig.y + v2.y
             * [m20, m21, m22, m23]     0 0 0       orig.z, orig.z + v1.z, orig.z + v2.z
             * [m30, m31, m32, m33]     1 1 1       1     , 1            , 1            
             * 
             * 
             * m03 = orig.x
             * m13 = orig.y
             * m23 = orig.z
             * m33 = 1
             * 
             * m00 = v1.x
             * m10 = v1.y
             * m20 = v1.z
             * 
             * m01 = v2.x
             * m11 = v2.y
             * m21 = v2.z
             */

            var unitTriangleToWorld = new Matrix();
            var a = p2w - p1w;
            var b = p3w - p1w;
            unitTriangleToWorld.Column1 = new Vector4(a, 0);
            unitTriangleToWorld.Column2 = new Vector4(b, 0);
            unitTriangleToWorld.Column3 = new Vector4(Vector3.Cross(b, a), 0);
            unitTriangleToWorld.Column4 = new Vector4(p1w, 1);

//            unitTriangleToWorld.Transpose();
//            Console.WriteLine(p1w + " " + p2w + " " + p3w);
//            Console.WriteLine("o: " + Vector3.TransformCoordinate(new Vector3(0, 0, 0), unitTriangleToWorld));
//            Console.WriteLine("o + 1: " + Vector3.TransformCoordinate(new Vector3(1, 0, 0), unitTriangleToWorld));
//            Console.WriteLine("o + 2: " + Vector3.TransformCoordinate(new Vector3(0, 1, 0), unitTriangleToWorld));
//            Console.WriteLine("n: " + Vector3.TransformNormal(new Vector3(0, 0, 1), unitTriangleToWorld));
//            unitTriangleToWorld.Transpose();

            scene.AddRenderable(
               unitTriangleMesh,
               unitTriangleToWorld,
               new MaterialDescription {
                  Properties = new MaterialProperties { Metallic = 0, Roughness = 1.00f }
               },
               Color.FromBgra(fillStyle.Color.ToArgb()));
         }

         public void FillPolygon(IReadOnlyList<DoubleVector3> polygonPoints, FillStyle fillStyle) {
            lock (scene) {


               DrawPolygon(polygonPoints, new StrokeStyle(fillStyle.Color));
            }
         }

         public void DrawPolygon(IReadOnlyList<DoubleVector3> polygonPoints, StrokeStyle strokeStyle) {
            lock (scene) {
               for (var i = 0; i < polygonPoints.Count; i++) {
                  DrawLine(polygonPoints[i], polygonPoints[(i + 1) % polygonPoints.Count], strokeStyle);
               }
            }
         }

         public void DrawText(string text, DoubleVector3 point) {
         }
      }
   }
}
