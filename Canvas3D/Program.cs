using System;
using System.Drawing;
using System.Linq;
using SharpDX;
using Color = SharpDX.Color;

namespace Canvas3D {
   internal static class Program {
      private static readonly Vector3 cameraEye = new Vector3(3, 2.5f, 5);
      private static readonly Matrix view = MatrixCM.LookAtRH(cameraEye, new Vector3(0, 0.5f, 0), new Vector3(0, 1, 0));
      private static Matrix projView;

      private const int NUM_LAYERS = 100;
      private const int CUBES_PER_LAYER = 10 ;
      private static readonly Matrix[] cubeDefaultTransforms = (
         from layer in Enumerable.Range(0, NUM_LAYERS)
         from i in Enumerable.Range(0, CUBES_PER_LAYER)
         select
         MatrixCM.RotationY(
            2 * (float)Math.PI * i / CUBES_PER_LAYER + (layer + 1) * i + layer
         ) *
         MatrixCM.Translation(
            0.8f + 0.02f * layer,
            0.5f + 0.3f * (float)Math.Sin((8 + 7 * layer) * Math.PI * i / CUBES_PER_LAYER),
            0) *
         MatrixCM.Scaling(0.2f / (float)Math.Sqrt(NUM_LAYERS)) *
         MatrixCM.RotationY(i)).ToArray();

      public static void Main(string[] args) {
         var graphicsLoop = GraphicsLoop.CreateWithNewWindow(1280, 720, InitFlags.DisableVerticalSync | InitFlags.EnableDebugStats);
         graphicsLoop.Form.Resize += (s, e) => {
            UpdateProjViewMatrix(graphicsLoop.Form.ClientSize);
         };
         UpdateProjViewMatrix(graphicsLoop.Form.ClientSize);

         var floatingCubesBatch = new RenderJobBatch();
         floatingCubesBatch.Mesh = graphicsLoop.AssetManager.GetPresetMesh(MeshPreset.UnitCube);
         foreach (var transform in cubeDefaultTransforms) {
            floatingCubesBatch.Jobs.Add(new RenderJobDescription {
               WorldTransform = transform,
               MaterialIndex = -1
            });
         }

         for (var frame = 0; graphicsLoop.IsRunning(out var renderer); frame++) {
            var t = (float)graphicsLoop.Statistics.FrameTime.TotalSeconds;
            renderer.ClearScene();
            renderer.SetCamera(cameraEye, projView);

            // Draw floor
            renderer.AddRenderable(
               MeshPreset.UnitCube,
               MatrixCM.Scaling(4f, 0.1f, 4f) * MatrixCM.Translation(0, -0.5f, 0) * MatrixCM.RotationX((float)Math.PI),
               new MaterialDescription { Metallic = 0.0f, Roughness = 0.04f });

            // Draw center cube / sphere
            renderer.AddRenderable(
               false ? MeshPreset.UnitCube : MeshPreset.UnitSphere, 
               MatrixCM.Translation(0, 0.5f, 0),
               new MaterialDescription { Metallic = 1.0f, Roughness = 0.8f });

            // Draw floating cubes circling around center cube
            floatingCubesBatch.BatchTransform = MatrixCM.RotationY(t * (float)Math.PI / 10.0f);
            floatingCubesBatch.MaterialIndexOverride = renderer.AddMaterial(new MaterialDescription {
               Metallic = 0.0f,
               Roughness = 0.04f
            });
            renderer.AddRenderJobBatch(floatingCubesBatch);

            // Add spotlights
            renderer.AddSpotlight(
               new Vector3(5, 4, 3), new Vector3(0, 0, 0), (float)Math.PI / 8.0f,
               Color.White, 100.0f,
               0.0f, 6.0f, 3.0f,
               0.5f / 256.0f);
            renderer.AddSpotlight(new Vector3(5, 4, -5), new Vector3(0, 0, 0), (float)Math.PI / 8.0f, Color.White, 100.0f, 3.0f, 6.0f, 1.0f);

            // Draw the scene
            renderer.RenderScene();
         }
      }

      private static bool zfirst = true;
      private static void UpdateProjViewMatrix(Size clientSize) {
         var verticalFov = (float)Math.PI / 4;
         var aspect = clientSize.Width / (float)clientSize.Height;
         var proj = MatrixCM.PerspectiveFovRH(verticalFov, aspect, 1.0f, 100.0f);
         projView = proj * view;

         if (zfirst) {
            zfirst = false;
            var lookat = new Vector3(0, 0.5f, 0);
            //var pos = new Vector4(lookat + 0.1f * (cameraEye - lookat), 1);//new Vector4(1, 2, 3, 1);
            var pos = new Vector4(0.5f, 1.0f, 0.5f, 1);
            var projViewRm = projView;
            projViewRm.Transpose();
            var homogeneous = Vector4.Transform(pos, projViewRm);
            Console.WriteLine("pos: " + pos);
            Console.WriteLine("posh: " + homogeneous);
            homogeneous /= homogeneous.W;
            Console.WriteLine("poshn: " + homogeneous);
            var projViewRmInv = projViewRm;
            projViewRmInv.Invert();
            Console.WriteLine();
            Console.WriteLine(projViewRmInv * projViewRm);
            Console.WriteLine();
            Console.WriteLine(projViewRm * projViewRmInv);
            Console.WriteLine();
            var x = Vector4.Transform(homogeneous, projViewRmInv);
            Console.WriteLine(x);
            x /= x.W;
            Console.WriteLine(x);
         }
      }
   }
}
