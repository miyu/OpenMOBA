using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using OpenMOBA.Geometry;

namespace OpenMOBA.Debugging {
   public class DebugDisplay {
      private readonly object synchronization = new object();

      private readonly Point drawPadding;
      private readonly Size displaySize;
      private readonly Bitmap bitmap;
      private readonly PictureBox pb;
      private readonly Form form;

      public DebugDisplay(Size inputDisplaySize = default(Size), Point inputDrawPadding = new Point()) {
         this.displaySize = inputDisplaySize == default(Size) ? new Size(1000, 1000) : inputDisplaySize;
         this.drawPadding = inputDrawPadding == default(Point) ? new Point(100, 100) : inputDrawPadding;

         var paddedSize = new Size(displaySize.Width + 2 * drawPadding.X, displaySize.Height + 2 * drawPadding.Y);
         form = new Form { ClientSize = paddedSize };
         pb = new PictureBox { Size = paddedSize };
         form.Controls.Add(pb);

         bitmap = new Bitmap(paddedSize.Width, paddedSize.Height);
      }

      public Font Font => form.Font;

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
         Draw(g => {
            g.DrawString(text, form.Font, Brushes.Black, (float)x, (float)y);
         });
      }

      private void UpdateDisplay() {
         var displayBitmap = (Image)bitmap.Clone();

         form.BeginInvoke(new Action(() => {
            lock (synchronization) {
               pb.Image = displayBitmap;
            }
         }));
      }

      public static DebugDisplay CreateShow(Size displaySize = default(Size)) {
         var display = new DebugDisplay(displaySize);
         var shownLatch = new ManualResetEvent(false);
         display.form.Shown += (s, e) => shownLatch.Set();
         new Thread(() => {
            Application.Run(display.form);
         }) { ApartmentState = ApartmentState.STA }.Start();
         shownLatch.WaitOne();
         return display;
      }
   }
}
