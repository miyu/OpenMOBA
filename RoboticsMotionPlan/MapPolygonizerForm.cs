using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using OpenMOBA.Geometry;

namespace RoboticsMotionPlan {
   public partial class Program {
      public class MapPolygonizerForm : Form {
         private readonly Image baseImage;
         private readonly Pen HolePen = new Pen(Color.Red, 4);
         private readonly Pen LandPen = new Pen(Color.Magenta, 4);
         private readonly PictureBox pb;

         public MapPolygonizerForm(string mapPath) {
            baseImage = Image.FromFile(mapPath);
            pb = new PictureBox();
            pb.Image = (Bitmap)baseImage.Clone();
            pb.Location = Point.Empty;
            var scale = 2;
            pb.Size = new Size(baseImage.Size.Width / scale, baseImage.Size.Height / scale);
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            Controls.Add(pb);
            ClientSize = pb.Size;
            pb.MouseMove += (s, e) => { Text = e.X * scale + " " + (e.Y * scale); };
         }

         public void Render(List<List<IntVector2>> landPolys, List<List<IntVector2>> holePolys, IntVector2 start, List<IntVector2> goodWaypoints, List<IntVector2> badWaypoints) {
            var image = (Bitmap)baseImage.Clone();
            using (var g = Graphics.FromImage(image)) {
               foreach (var landPoly in landPolys) DrawPoly(g, landPoly, LandPen);
               foreach (var holePoly in holePolys) DrawPoly(g, holePoly, HolePen);
               DrawPoint(g, start, Brushes.Lime);
               goodWaypoints.ForEach(p => DrawPoint(g, p, Brushes.Blue));
               badWaypoints.ForEach(p => DrawPoint(g, p, Brushes.Red));
            }
            BeginInvoke(new Action(() => { pb.Image = image; }));
         }

         private void DrawPoly(Graphics g, List<IntVector2> poly, Pen pen) {
            for (var i = 0; i < poly.Count - 1; i++) g.DrawLine(pen, poly[i].X, poly[i].Y, poly[i + 1].X, poly[i + 1].Y);
         }

         private void DrawPoint(Graphics g, IntVector2 p, Brush brush) {
            g.FillRectangle(brush, p.X - 8, p.Y - 8, 16, 16);
         }

         public static void Run(string mapPath, string polygonPath) {
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
                  var (landPolys, holePolys) = FileLoader.Load(polygonPath);
                  form.Render(landPolys, holePolys, start, goodWaypoints, badWaypoints);

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
