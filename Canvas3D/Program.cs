using System;
using System.Drawing;
using SharpDX;
using SharpDX.Windows;
using Color = SharpDX.Color;

namespace Canvas3D {
   public static class Program {
      public static void Main(string[] args) {
         var renderForm = new RenderForm { ClientSize = new Size(1280, 720) };
         var graphicsDevice = Direct3DGraphicsDevice.Create(renderForm);
         var techniqueCollection = graphicsDevice.TechniqueCollection;
         var meshPresets = graphicsDevice.MeshPresets;
         var immediateContext = graphicsDevice.ImmediateContext;
         var renderer = new BatchedRenderer3D(graphicsDevice);

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

               var dt = (float)(DateTime.Now - start).TotalSeconds / 10;
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
