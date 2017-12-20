using System;
using System.Drawing;
using Canvas3D.LowLevel.Direct3D;
using SharpDX.Windows;

namespace Canvas3D {
   public class GraphicsLoop {
      private GraphicsLoop(RenderForm form, Direct3DGraphicsDevice graphicsDevice, BatchedRenderer3D renderer) {
         Form = form;
         GraphicsDevice = graphicsDevice;
         Renderer = renderer;
         RenderLoop = new RenderLoop(Form);
      }

      public RenderForm Form { get; }
      private Direct3DGraphicsDevice GraphicsDevice { get; }
      private BatchedRenderer3D Renderer { get; }
      private RenderLoop RenderLoop { get; }

      public bool IsRunning(out BatchedRenderer3D renderer) {
         if (RenderLoop.NextFrame()) {
            GraphicsDevice.DoEvents();
            renderer = Renderer;
            return true;
         }
         renderer = null;
         return false;
      }

      public static GraphicsLoop CreateWithNewWindow(int clientWidth, int clientHeight, InitFlags flags = 0) {
         return CreateWithNewWindow(new Size(clientWidth, clientHeight), flags);
      }

      public static GraphicsLoop CreateWithNewWindow(Size clientSize, InitFlags flags = 0) {
         var renderForm = new RenderForm { ClientSize = clientSize };
         var graphicsDevice = Direct3DGraphicsDevice.Create(renderForm);
         var renderer = new BatchedRenderer3D(graphicsDevice);

         if (!flags.HasFlag(InitFlags.HiddenWindow)) {
            renderForm.Show();
         }

         graphicsDevice.ImmediateContext.SetVsyncEnabled(
            !flags.HasFlag(InitFlags.DisableVerticalSync));

         return new GraphicsLoop(renderForm, graphicsDevice, renderer);
      }
   }

   [Flags]
   public enum InitFlags {
      None = 0,
      DisableVerticalSync = 1 << 1,
      HiddenWindow = 1 << 2,
   }
}
