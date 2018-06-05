using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using OpenMOBA;
using OpenMOBA.Geometry;

namespace RoboticsMotionPlan {
   public partial class Program {
      public class MapPolygonizerForm : Form {
         private readonly Bitmap baseImage;
         private readonly Pen HolePen = new Pen(Color.Red, 4);
         private readonly Pen LandPen = new Pen(Color.Magenta, 4);
         private readonly PictureBox pb;

         public unsafe MapPolygonizerForm(string mapPath) {
            if (mapPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) {
               var lines = File.ReadAllLines(mapPath).Map(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
               var width = lines[0].Length;
               var height = lines.Length;
               baseImage = new Bitmap(width, height, PixelFormat.Format32bppRgb);
               var data = baseImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
               var pScan0 = (byte*)data.Scan0;
               for (var y = 0; y < height; y++) {
                  for (var x = 0; x < width; x++) {
                     byte* pPixel = pScan0 + data.Stride * y + 4 * x;
                     *(uint*)pPixel = lines[y][x] == '#' ? 0xFF000000U : 0xFFFFFFFU;
                  }
               }
               baseImage.UnlockBits(data);
            } else {
               baseImage = (Bitmap)Bitmap.FromFile(mapPath);
            }
            pb = new PictureBox();
            pb.Image = (Bitmap)baseImage.Clone();
            pb.Location = new Point(0, 0);
            var scale = 1;
            pb.Size = new Size((int)(baseImage.Size.Width / scale), (int)(baseImage.Size.Height / scale));
            Console.WriteLine(baseImage.Size);
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            Controls.Add(pb);
            ClientSize = pb.Size;
            // not flipped for map trace, flipped for plan
            pb.MouseMove += (s, e) => { Text = e.X * scale + " " + e.Y * scale; };
         }

         Font font = new Font(FontFamily.GenericSerif, 30);

         public void Render(List<List<IntVector2>> landPolys, List<List<IntVector2>> holePolys, IntVector2 start, List<IntVector2> goodWaypoints, List<IntVector2> badWaypoints, List<(DoubleVector2, double, bool)> plan) {
            var image = (Bitmap)baseImage.Clone();
            using (var g = Graphics.FromImage(image)) {
               foreach (var landPoly in landPolys) DrawPoly(g, landPoly, LandPen);
               foreach (var holePoly in holePolys) DrawPoly(g, holePoly, HolePen);
               DrawPoint(g, start, Brushes.Lime);
               goodWaypoints.ForEach(p => DrawPoint(g, p, Brushes.Blue));
               badWaypoints.ForEach(p => DrawPoint(g, p, Brushes.Red));

               foreach (var (i, (p, theta, isRoi)) in plan.Enumerate()) {
                  g.FillEllipse(
                     isRoi ? Brushes.Orange : Brushes.Black,
                     (float)p.X - 5,
                     (float)p.Y - 5,
                     10,
                     10);
                  var to = p + DoubleVector2.FromRadiusAngle(50, theta);
                  g.DrawLine(
                     Pens.Magenta,
                     (float)p.X, (float)p.Y,
                     (float)to.X, (float)to.Y);
                  g.DrawString("#" + i, font, Brushes.Black, (float)p.X, (float)p.Y);
               }
            }
            BeginInvoke(new Action(() => { pb.Image = image; }));
         }

         private void DrawPoly(Graphics g, List<IntVector2> poly, Pen pen) {
            for (var i = 0; i < poly.Count - 1; i++) g.DrawLine(pen, poly[i].X, poly[i].Y, poly[i + 1].X, poly[i + 1].Y);
         }

         private void DrawPoint(Graphics g, IntVector2 p, Brush brush) {
            g.FillRectangle(brush, p.X - 8, p.Y - 8, 16, 16);
         }

         public static void Run(string mapPath, string polygonPath, string planPath) {
            MapPolygonizerForm form = null;
            var latch = new ManualResetEvent(false);

            new Thread(() => {
               form = new MapPolygonizerForm(mapPath);
               form.Shown += (s, e) => latch.Set();
               Application.Run(form);
            }) { ApartmentState = ApartmentState.STA }.Start();
            latch.WaitOne();

            if (!File.Exists(polygonPath)) File.Create(polygonPath).Close();

            var start = FileLoader.LoadPoints("start.csv").First();
            var goodWaypoints = FileLoader.LoadPoints("good_waypoints.csv");
            var badWaypoints = FileLoader.LoadPoints("bad_waypoints.csv");

            while (true) {
               try {
                  var (landPolys, holePolys) = FileLoader.LoadMap(polygonPath);
                  var plan = FileLoader.LoadPlan(planPath);
                  form.Render(landPolys, holePolys, start, goodWaypoints, badWaypoints, plan);
                  //                  Console.ReadLine();
                  Thread.Sleep(100);
               } catch (Exception e) {
                  Console.WriteLine("Parse fail");
               }
            }
         }
      }
   }
}
