using System;
using SharpDX;

namespace Canvas3D {
   internal static class Program {
      private static readonly Matrix proj = MatrixCM.PerspectiveFovRH((float)Math.PI / 4.0f, 1280.0f / 720.0f, 0.1f, 100.0f);
      private static readonly Vector3 cameraEye = new Vector3(3, 2.5f, 5);
      //private static readonly Vector3 cameraEye = new Vector3(1, 4.5f, 1);
      private static readonly Matrix view = MatrixCM.LookAtRH(cameraEye, new Vector3(0, 0.5f, 0), new Vector3(0, 1, 0));
      private static readonly Matrix projView = proj * view;

      public static void Main(string[] args) {
         var start = DateTime.Now;
         var graphicsLoop = GraphicsLoop.CreateWithNewWindow(1280, 720, InitFlags.DisableVerticalSync);
         while (graphicsLoop.IsRunning(out var renderer)) {
            renderer.ClearScene();
            renderer.SetCamera(cameraEye, projView);

            // Draw floor
            renderer.AddRenderable(MeshPreset.UnitCube, MatrixCM.Scaling(4f, 0.1f, 4f) * MatrixCM.Translation(0, -0.5f, 0) * MatrixCM.RotationX((float)Math.PI));

            // Draw center cube / sphere
            //renderer.AddRenderable(MeshPreset.UnitCube, MatrixCM.Translation(0, 0.5f, 0));
            renderer.AddRenderable(MeshPreset.UnitSphere, MatrixCM.Translation(0, 0.5f, 0) * MatrixCM.Scaling(0.5f));

            // Draw floating cubes circling around center cube
            var dt = (float)(DateTime.Now - start).TotalSeconds / 10;
            for (var i = 0; i < 10; i++) {
               renderer.AddRenderable(
                  MeshPreset.UnitCube,
                  MatrixCM.RotationY(2 * (float)Math.PI * i / 10.0f + dt * (float)Math.PI) * 
                  MatrixCM.Translation(1.0f, 0.9f + 0.4f * (float)Math.Sin(8 * Math.PI * i / 10.0), 0) * 
                  MatrixCM.Scaling(0.2f) * 
                  MatrixCM.RotationY(i)
               );
            }

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
