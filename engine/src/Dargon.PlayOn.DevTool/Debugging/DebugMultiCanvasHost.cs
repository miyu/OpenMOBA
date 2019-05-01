using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Dargon.PlayOn.DevTool.Debugging {
   public class DebugMultiCanvasHost : IDebugMultiCanvasHost {
      private readonly object synchronization = new object();
      private readonly List<CanvasAndFrameIndex> frames = new List<CanvasAndFrameIndex>();
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
                     Thread.Sleep(25);
                  }
               }).Start();
            } else {
               new Thread(() => {
                  var v = slider.Value;
                  while (slider.Value == v && v != slider.Maximum) {
                     v = Math.Min(v + 1 * speedup, (int)slider.Maximum);
                     slider.Invoke(new Action(() => { slider.Value = v; }));
                     Thread.Sleep(25);
                  }
               }).Start();
            }
         };
      }

      public IDebugCanvas CreateAndAddCanvas(int frameIndex) {
         var canvas = new DebugCanvas(canvasSize, canvasPadding, projector);
         lock (synchronization) {
            frames.Add(new CanvasAndFrameIndex {
               Canvas = canvas,
               FrameIndex = frameIndex,
               Timestamp = DateTime.Now
            });
            UpdateSlider();
         }
         canvas.Update += (s, e) => {
            var bitmapClone = (Bitmap)e.Bitmap.Clone();
            form.BeginInvoke(new Action(() => {
               lock (synchronization) {
                  if (Enumerable.Last(frames, f => f.FrameIndex <= slider.Value).Canvas == canvas) {
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
               var canvasAndTimestamp = Enumerable.Last(frames, f => f.FrameIndex <= slider.Value);

               canvasAndTimestamp.Canvas.EnterBitmapCriticalSection(
                  bitmap => {
                     form.Text = canvasAndTimestamp.FrameIndex + "";
                     pb.Image = (Bitmap)bitmap.Clone();
                  }
               );
            }
         }));
      }

      private void UpdateSlider() {
         form.BeginInvoke(new Action(() => {
            lock (synchronization) {
               slider.Minimum = Enumerable.First(frames).FrameIndex;
               slider.Maximum = Enumerable.Last(frames).FrameIndex;
               slider.TickFrequency = 1;
               if (pb.Image == null) {
                  HandleSliderValueChanged(this, EventArgs.Empty);
               } else if (frames.Count - 3 >= 0 && slider.Value == frames[frames.Count - 3].FrameIndex) {
                  // navigates to frame before last, not last because last is currently drawing.
                  slider.Value = frames[frames.Count - 2].FrameIndex;
               }
            }
         }));
      }

      public void DumpScreenshotsToDocumentsPictures() {
         var myPicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
         var outputDirectory = Path.Combine(myPicturesPath, "OpenMoba", "Dumps");
         Directory.CreateDirectory(outputDirectory);

         lock (synchronization) {
            foreach (var frame in frames) {
               var raw = frame.Canvas.BitmapReadOnlyUnsafe;
               var bitmap = new Bitmap(raw.Width, raw.Height, raw.PixelFormat);
               var rect = new Rectangle(0, 0, raw.Width, raw.Height);
               using (var g = Graphics.FromImage(bitmap)) {
                  g.Clear(SystemColors.Control);
                  g.DrawImage(raw, rect, rect, GraphicsUnit.Pixel);
               }

               var outputFileName = frame.Timestamp.ToString("u").Replace(' ', '_').Replace(':', '.') + "_f" + frame.FrameIndex + ".jpg";
               var outputFilePath = Path.Combine(outputDirectory, outputFileName);
               Console.WriteLine("Dump " + outputFilePath);
               bitmap.Save(outputFilePath, ImageFormat.Jpeg);
            }
         }
      }

      private struct CanvasAndFrameIndex {
         public DebugCanvas Canvas { get; set; }
         public int FrameIndex { get; set; }
         public DateTime Timestamp { get; set; }
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