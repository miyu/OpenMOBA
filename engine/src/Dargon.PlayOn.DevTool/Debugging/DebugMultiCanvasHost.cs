using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Dargon.PlayOn.DevTool.Debugging {
   public class DebugMultiCanvasHost : IDebugMultiCanvasHost {
      private readonly object synchronization = new object();
      private readonly List<CanvasAndTimeStamp> frames = new List<CanvasAndTimeStamp>();
      private const double scale = 1.0f;
      private readonly Size canvasSize;
      private readonly Point canvasPadding;
      private readonly IProjector projector;
      private Form form;
      private PictureBox pb;
      private TrackBar slider;

      public DebugMultiCanvasHost(Size canvasSize, Point canvasPadding, IProjector projector = null) {
         this.canvasSize = canvasSize;
         this.canvasPadding = canvasPadding;
         this.projector = projector ?? OrthographicXYProjector.Instance;

         var paddedSize = new Size(canvasSize.Width + 2 * canvasPadding.X, canvasSize.Height + 2 * canvasPadding.Y);
         var displaySize = new Size((int)(paddedSize.Width * scale), (int)(paddedSize.Height * scale));
         form = new Form {
            ClientSize = new Size(displaySize.Width, displaySize.Height + 40),
            StartPosition = FormStartPosition.CenterScreen
         };
         pb = new PictureBox {
            Location = Point.Empty,
            Size = displaySize,
            SizeMode = PictureBoxSizeMode.Zoom
         };
         slider = new TrackBar {
            Orientation = Orientation.Horizontal,
            Location = new Point(0, displaySize.Height),
            Size = new Size(displaySize.Width, 40)
         };
         slider.ValueChanged += HandleSliderValueChanged;
         form.Controls.Add(pb);
         form.Controls.Add(slider);
         slider.KeyUp += (s, e) => {
            if (e.KeyCode != Keys.Space) {
               return;
            }
            var speedup = 1;
            if (e.Shift) {
               new Thread(() => {
                  var v = slider.Value;
                  while (slider.Value == v && v != 0) {
                     v = Math.Max(v - 2 * speedup, 0);
                     slider.Invoke(new Action(() => { slider.Value = v; }));
                     Thread.Sleep(12);
                  }
               }).Start();
            } else {
               new Thread(() => {
                  var v = slider.Value;
                  while (slider.Value == v && v != slider.Maximum) {
                     v = Math.Min(v + 1 * speedup, (int)slider.Maximum);
                     slider.Invoke(new Action(() => { slider.Value = v; }));
                     Thread.Sleep(12);
                  }
               }).Start();
            }
         };
      }

      public IDebugCanvas CreateAndAddCanvas(int timestamp) {
         var canvas = new DebugCanvas(canvasSize, canvasPadding, projector);
         lock (synchronization) {
            frames.Add(new CanvasAndTimeStamp {
               Canvas = canvas,
               Timestamp = timestamp
            });
            UpdateSlider();
         }
         canvas.Update += (s, e) => {
            var bitmapClone = (Bitmap)e.Bitmap.Clone();
            form.BeginInvoke(new Action(() => {
               lock (synchronization) {
                  if (Enumerable.Last(frames, f => f.Timestamp <= slider.Value).Canvas == canvas) {
                     pb.Image = bitmapClone;
                  }
               }
            }));
         };
         return canvas;
      }

      private void HandleSliderValueChanged(object sender, EventArgs e) {
         form.BeginInvoke(new Action(() => {
            lock (synchronization) {
               var canvasAndTimestamp = Enumerable.Last(frames, f => f.Timestamp <= slider.Value);

               canvasAndTimestamp.Canvas.EnterBitmapCriticalSection(
                  bitmap => {
                     form.Text = canvasAndTimestamp.Timestamp + "";
                     pb.Image = (Bitmap)bitmap.Clone();
                  }
               );
            }
         }));
      }

      private void UpdateSlider() {
         form.BeginInvoke(new Action(() => {
            lock (synchronization) {
               slider.Minimum = Enumerable.First(frames).Timestamp;
               slider.Maximum = Enumerable.Last(frames).Timestamp;
               slider.TickFrequency = 1;
               if (pb.Image == null) {
                  HandleSliderValueChanged(this, EventArgs.Empty);
               } else if (frames.Count - 3 >= 0 && slider.Value == frames[frames.Count - 3].Timestamp) {
                  // navigates to frame before last, not last because last is currently drawing.
                  slider.Value = frames[frames.Count - 2].Timestamp;
               }
            }
         }));
      }

      private struct CanvasAndTimeStamp {
         public DebugCanvas Canvas { get; set; }
         public int Timestamp { get; set; }
      }

      public static DebugMultiCanvasHost CreateAndShowCanvas(Size canvasSize, Point canvasPadding, IProjector projector = null) {
         DebugMultiCanvasHost multiCanvasHost = null;
         var shownLatch = new ManualResetEvent(false);
         var thread = new Thread(() => {
            multiCanvasHost = new DebugMultiCanvasHost(canvasSize, canvasPadding, projector);
            multiCanvasHost.form.Shown += (s, e) => shownLatch.Set();
            Application.Run((Form)multiCanvasHost.form);
         });
         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         shownLatch.WaitOne();
         return multiCanvasHost;
      }
   }
}