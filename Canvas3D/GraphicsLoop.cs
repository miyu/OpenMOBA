using System;
using System.Drawing;
using System.Linq;
using Canvas3D.LowLevel;
using Canvas3D.LowLevel.Direct3D;
using SharpDX.Windows;

namespace Canvas3D {
   public class GraphicsLoop {
      private readonly InitFlags _initFlags;

      private GraphicsLoop(InitFlags initFlags, RenderForm form, IGraphicsFacade graphicsFacade, RenderContext renderer) {
         _initFlags = initFlags;

         Form = form;
         GraphicsFacade = graphicsFacade;
         Renderer = renderer;
         RenderLoop = new RenderLoop(Form);
         Statistics = new GraphicsLoopStatistics();
      }

      public RenderForm Form { get; }
      public IGraphicsFacade GraphicsFacade { get; }
      private RenderContext Renderer { get; }
      private RenderLoop RenderLoop { get; }
      public IPresetsStore Presets => GraphicsFacade.Presets;
      public GraphicsLoopStatistics Statistics { get; }

      public bool IsRunning(out IRenderContext renderer) {
         if (RenderLoop.NextFrame()) {
            GraphicsFacade.Device.DoEvents();
            Statistics.HandleFrameEnter(_initFlags.HasFlag(InitFlags.EnableDebugStats) ? Form : null);
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
         var graphicsFacade = Direct3DGraphicsFacade.Create(renderForm);
         var renderer = new RenderContext(graphicsFacade);

         if (!flags.HasFlag(InitFlags.HiddenWindow)) {
            renderForm.Show();
         }

         graphicsFacade.Device.ImmediateContext.SetVsyncEnabled(
            !flags.HasFlag(InitFlags.DisableVerticalSync));

         return new GraphicsLoop(flags, renderForm, graphicsFacade, renderer);
      }
   }

   [Flags]
   public enum InitFlags {
      None = 0,
      DisableVerticalSync = 1 << 1,
      HiddenWindow = 1 << 2,
      EnableDebugStats = 1 << 4,
   }

   public class GraphicsLoopStatistics {
      private readonly TimeSpan[] frameIntervalBuffer = new TimeSpan[60];

      public GraphicsLoopStatistics() {
         StartTime = FrameWallClockTime = DateTime.Now;
      }

      public DateTime StartTime { get; }
      public int Frame { get; private set; }
      public DateTime FrameWallClockTime { get; private set; }
      public TimeSpan FrameTime { get; private set; }
      public TimeSpan FrameInterval { get; private set; }
      public TimeSpan AveragedFrameInterval { get; private set; }

      internal void HandleFrameEnter(RenderForm formOpt) {
         Frame++;
         var now = DateTime.Now;
         FrameInterval = now - FrameWallClockTime;
         FrameWallClockTime = now;
         FrameTime = FrameWallClockTime - StartTime;
         frameIntervalBuffer[Frame % frameIntervalBuffer.Length] = FrameInterval;

         if (Frame % frameIntervalBuffer.Length == 0) {
            var ticks = 0L;
            foreach (var fi in frameIntervalBuffer) ticks += fi.Ticks;
            AveragedFrameInterval = new TimeSpan(ticks / frameIntervalBuffer.Length);

            if (formOpt != null) {
               formOpt.Text = $"Frame: {Frame} | FDT {AveragedFrameInterval.TotalMilliseconds:F2} ms | FPS {1 / AveragedFrameInterval.TotalSeconds:F2}";
            }
         }
      }

      //var lastTime = start;
      //var dts = new float[60];
      //var t = (float)(now - start).TotalSeconds;
      //var dt = (float)(now - lastTime).TotalSeconds;
      //lastTime = now;
      //dts[frame % dts.Length] = dt;
      //if (frame % dts.Length == 0) {
      //   float timeSums = 0;
      //   foreach (var x in dts) timeSums += x;
      //   graphicsLoop.Form.Text = $"Time: {t:F2} | FDT: {timeSums * 1000 / dts.Length:F2} ms | FPS: {dts.Length / timeSums:F2}";
      //}
   }
}
