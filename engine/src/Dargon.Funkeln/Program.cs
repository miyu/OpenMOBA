using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Canvas3D.LowLevel;
using SharpDX;
using SharpDX.DirectInput;
using Color = SharpDX.Color;
using Point = System.Drawing.Point;
using SNVector3 = System.Numerics.Vector3;

namespace Canvas3D {
   internal static class Program {
      private static Vector3 cameraTarget = new Vector3(-0.8f, -0.9f, 0.0f) * 0;
      private static Vector3 cameraOffset = new Vector3(0.4f, 0.5f, 1) * 5;//new Vector3(3, 2.5f, 5) - cameraTarget;
      private static Vector3 cameraUp = new Vector3(0, 1, 0);
      private static Matrix view = MatrixCM.ViewLookAtRH(cameraTarget + cameraOffset, cameraTarget, cameraUp);
      private static Matrix projView;

      public static void Main(string[] args) {
         var graphicsLoop = GraphicsLoop.CreateWithNewWindow(1280, 720, InitFlags.DisableVerticalSync | InitFlags.EnableDebugStats);
         graphicsLoop.Form.Resize += (s, e) => UpdateProjViewMatrix(graphicsLoop.Form.ClientSize);
         UpdateProjViewMatrix(graphicsLoop.Form.ClientSize);

         while (graphicsLoop.IsRunning(out var renderer, out var input)) {
            if (graphicsLoop.Statistics.Frame % 1000 == 0) Console.WriteLine(graphicsLoop.Statistics.AveragedFrameInterval.TotalMilliseconds);
            renderer.RenderScene();
         }
      }

      private static bool zfirst = true;
      private static void UpdateProjViewMatrix(Size clientSize) {
         var verticalFov = (float)Math.PI / 4;
         var aspect = clientSize.Width / (float)clientSize.Height;
         var proj = MatrixCM.PerspectiveFovRH(verticalFov, aspect, 1.0f, 5000.0f);
         projView = proj * view;
      }
   }
}
