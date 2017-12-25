using System;
using System.Linq;
using SharpDX;

namespace Canvas3D {
   internal static class Program {
      private static readonly Matrix proj = MatrixCM.PerspectiveFovRH((float)Math.PI / 4.0f, 1280.0f / 720.0f, 0.1f, 100.0f);
      private static readonly Vector3 cameraEye = new Vector3(3, 2.5f, 5);
      //private static readonly Vector3 cameraEye = new Vector3(1, 4.5f, 1);
      private static readonly Matrix view = MatrixCM.LookAtRH(cameraEye, new Vector3(0, 0.5f, 0), new Vector3(0, 1, 0));
      private static readonly Matrix projView = proj * view;

      private const int NUM_LAYERS = 100;
      private const int CUBES_PER_LAYER = 100;
      private static readonly Matrix[] cubeDefaultTransforms = (
         from layer in Enumerable.Range(0, NUM_LAYERS)
         from i in Enumerable.Range(0, CUBES_PER_LAYER)
         select
         MatrixCM.RotationY(
            2 * (float)Math.PI * i / CUBES_PER_LAYER + layer * i + layer
         ) *
         MatrixCM.Translation(
            0.8f + 0.2f * (float)Math.Pow(layer, 0.5f),
            0.9f + 0.4f * (float)Math.Sin((8 + 7 * layer) * Math.PI * i / CUBES_PER_LAYER),
            0) *
         MatrixCM.Scaling(0.2f / (float)Math.Sqrt(NUM_LAYERS)) *
         MatrixCM.RotationY(i)).ToArray();

      public static void Main(string[] args) {
         var graphicsLoop = GraphicsLoop.CreateWithNewWindow(1280, 720, InitFlags.DisableVerticalSync | InitFlags.EnableDebugStats);
         var floatingCubesBatch = new RenderJobBatch();
         floatingCubesBatch.Mesh = graphicsLoop.AssetManager.GetPresetMesh(MeshPreset.UnitCube);
         foreach (var transform in cubeDefaultTransforms) {
            floatingCubesBatch.Jobs.Add(new RenderJobDescription {
               WorldTransform = transform
            });
         }

         for (var frame = 0; graphicsLoop.IsRunning(out var renderer); frame++) {
            var t = (float)graphicsLoop.Statistics.FrameTime.TotalSeconds;
            renderer.ClearScene();
            renderer.SetCamera(cameraEye, projView);

            // Draw floor
            renderer.AddRenderable(MeshPreset.UnitCube, MatrixCM.Scaling(4f, 0.1f, 4f) * MatrixCM.Translation(0, -0.5f, 0) * MatrixCM.RotationX((float)Math.PI));

            // Draw center cube / sphere
            //renderer.AddRenderable(MeshPreset.UnitCube, MatrixCM.Translation(0, 0.5f, 0));
            renderer.AddRenderable(MeshPreset.UnitSphere, MatrixCM.Translation(0, 0.5f, 0) * MatrixCM.Scaling(0.5f));

            // Draw floating cubes circling around center cube
            floatingCubesBatch.BatchTransform = MatrixCM.RotationY(t * (float)Math.PI / 10.0f);
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
   }
}
