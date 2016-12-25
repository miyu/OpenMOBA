using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace OpenMOBA.Debugging {
   public class DebugDisplay {
      private readonly Bitmap bitmap;
      private readonly Size displaySize;

      private readonly object synchronization = new object();
      private readonly Point drawPadding;
      private readonly Form form;
      private readonly PictureBox pb;

      public DebugDisplay(Size inputDisplaySize = default(Size), Point inputDrawPadding = new Point()) {
         displaySize = inputDisplaySize == default(Size) ? new Size(1000, 1000) : inputDisplaySize;
         drawPadding = inputDrawPadding == default(Point) ? new Point(100, 100) : inputDrawPadding;

         var paddedSize = new Size(displaySize.Width + 2 * drawPadding.X, displaySize.Height + 2 * drawPadding.Y);
         form = new Form { ClientSize = new Size(paddedSize.Width / 2, paddedSize.Height / 2) };
         form.BackColor = Color.White;
         form.StartPosition = FormStartPosition.CenterScreen;
         pb = new PictureBox { Dock = DockStyle.Fill }; //Size = paddedSize };
         pb.SizeMode = PictureBoxSizeMode.Zoom;
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
         Draw(g => { g.DrawString(text, form.Font, Brushes.Black, (float)x, (float)y); });
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
         DebugDisplay display = null;
         var shownLatch = new ManualResetEvent(false);
         var thread = new Thread(() =>
         {
             display = new DebugDisplay(displaySize);
             display.form.Shown += (s, e) => shownLatch.Set();
             Application.Run(display.form);
         });
         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         shownLatch.WaitOne();
         return display;
      }
   }
}
