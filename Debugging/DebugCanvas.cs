using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace OpenMOBA.Debugging {
   public class DebugCanvasHost {
      private readonly Form form;
      private readonly PictureBox pb;

      public DebugCanvasHost(DebugCanvas canvas) {
         const double scale = 1.0f;
         form = new Form { ClientSize = new Size((int)(canvas.PaddedSize.Width * scale), (int)(canvas.PaddedSize.Height * scale)) };
         form.BackColor = Color.White;
         form.StartPosition = FormStartPosition.CenterScreen;
         pb = new PictureBox { Dock = DockStyle.Fill };
         pb.SizeMode = PictureBoxSizeMode.Zoom;
         form.Controls.Add(pb);

         canvas.Update += (s, e) => {
            Bitmap clonedBitmap = (Bitmap)e.Bitmap.Clone();
            form.BeginInvoke(new Action(() => {
               pb.Image = clonedBitmap;
            }));
         };
      }

      private static DebugCanvasHost CreateShow(DebugCanvas canvas) {
         DebugCanvasHost canvasHost = null;
         var shownLatch = new ManualResetEvent(false);
         var thread = new Thread(() => {
            canvasHost = new DebugCanvasHost(canvas);
            canvasHost.form.Shown += (s, e) => shownLatch.Set();
            Application.Run(canvasHost.form);
         });
         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         shownLatch.WaitOne();
         return canvasHost;
      }

      public static DebugCanvas CreateAndShowCanvas(Size displaySize = default(Size)) {
         displaySize = displaySize == default(Size) ? new Size(1000, 1000) : displaySize;
         var drawPadding = new Point(100, 100);

         var canvas = new DebugCanvas(displaySize, drawPadding);
         DebugCanvasHost.CreateShow(canvas);

         return canvas;
      }
   }

   public class DebugMultiCanvasHost {
      private readonly object synchronization = new object();
      private readonly List<CanvasAndTimeStamp> frames = new List<CanvasAndTimeStamp>();
      private const double scale = 1.0f;
      private readonly Size canvasSize;
      private readonly Point canvasPadding;
      private Form form;
      private PictureBox pb;
      private TrackBar slider;

      public DebugMultiCanvasHost(Size canvasSize, Point canvasPadding) {
         this.canvasSize = canvasSize;
         this.canvasPadding = canvasPadding;

         var paddedSize = new Size(canvasSize.Width + 2 * canvasPadding.X, canvasSize.Height + 2 * canvasPadding.Y);
         var displaySize = new Size((int)(paddedSize.Width * scale), (int)(paddedSize.Height * scale));
         form = new Form {
            ClientSize = new Size(displaySize.Width, displaySize.Height + 100)
         };
         pb = new PictureBox {
            Location = Point.Empty,
            Size = displaySize,
            SizeMode = PictureBoxSizeMode.Zoom
         };
         slider = new TrackBar {
            Orientation = Orientation.Horizontal,
            Location = new Point(0, displaySize.Height),
            Size = new Size(displaySize.Width, 100)
         };
         slider.ValueChanged += HandleSliderValueChanged;
         form.Controls.Add(pb);
         form.Controls.Add(slider);
      }

      public DebugCanvas CreateAndAddCanvas(int timestamp) {
         var canvas = new DebugCanvas(canvasSize, canvasPadding);
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
                  if (frames.Last(f => f.Timestamp <= slider.Value).Canvas == canvas) {
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
               frames.Last(f => f.Timestamp <= slider.Value).Canvas.EnterBitmapCriticalSection(
                        bitmap => {
                           pb.Image = (Bitmap)bitmap.Clone();
                        }
                     );
            }
         }));
      }

      private void UpdateSlider() {
         form.BeginInvoke(new Action(() => {
            lock (synchronization) {
               slider.Minimum = frames.First().Timestamp;
               slider.Maximum = frames.Last().Timestamp;
               slider.TickFrequency = 1;
               if (pb.Image == null) {
                  HandleSliderValueChanged(this, EventArgs.Empty);
               }
            }
         }));
      }

      private struct CanvasAndTimeStamp {
         public DebugCanvas Canvas { get; set; }
         public int Timestamp { get; set; }
      }

      public static DebugMultiCanvasHost CreateAndShowCanvas(Size canvasSize, Point canvasPadding) {
         DebugMultiCanvasHost multiCanvasHost = null;
         var shownLatch = new ManualResetEvent(false);
         var thread = new Thread(() => {
            multiCanvasHost = new DebugMultiCanvasHost(canvasSize, canvasPadding);
            multiCanvasHost.form.Shown += (s, e) => shownLatch.Set();
            Application.Run(multiCanvasHost.form);
         });
         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         shownLatch.WaitOne();
         return multiCanvasHost;
      }
   }

   public class DebugCanvas {
      private readonly Bitmap bitmap;

      private readonly object synchronization = new object();
      private readonly Point drawPadding;
      private readonly PictureBox pb;

      public event EventHandler<DebugCanvasUpdateEventArgs> Update;

      public class DebugCanvasUpdateEventArgs : EventArgs {
         public Bitmap Bitmap { get; set; }
      }

      public DebugCanvas(Size displaySize, Point drawPadding) {
         this.drawPadding = drawPadding;
         PaddedSize = new Size(displaySize.Width + 2 * drawPadding.X, displaySize.Height + 2 * drawPadding.Y);
         bitmap = new Bitmap(PaddedSize.Width, PaddedSize.Height);
      }

      public Size PaddedSize { get; }

      public Font Font => SystemFonts.DefaultFont;

      public void Draw(Action<Graphics> callback) {
         lock (synchronization) {
            using (var g = Graphics.FromImage(bitmap)) {
               g.Transform = new Matrix(1, 0, 0, 1, drawPadding.X, drawPadding.Y);
               callback(g);
            }
            UpdateDisplay();
         }
      }

      public void DrawText(string text, double x, double y) {
         Draw(g => { g.DrawString(text, Font, Brushes.Black, (float)x, (float)y); });
      }

      private void UpdateDisplay() {
         lock (synchronization) {
            Update?.Invoke(this, new DebugCanvasUpdateEventArgs { Bitmap = bitmap });
         }
      }

      public void EnterBitmapCriticalSection(Action<Bitmap> callback) {
         lock (synchronization) {
            callback(bitmap);
         }
      }
   }
}
