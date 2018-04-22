using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using ClipperLib;
using OpenMOBA;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Geometry;

namespace CrossoverPointTests {
   public class NameThisEventually {
      private static readonly Size bounds = new Size(1600, 1600);
      //private static readonly Random random = new Random(3);
      //private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(50, 50), new OrthographicXYProjector());

      [STAThread]
      public static void Main() {
         /*
         var canvas = host.CreateAndAddCanvas(0);
         canvas.Transform = Matrix4x4.CreateScale(1000 / 60000.0f) * Matrix4x4.CreateTranslation(500, 500, 0);
         var input = @":
: -30000 30000,-30000 -30000,30000 -30000,30000 30000,-12000 30000,-12000 24000,-6000 24000,-6000 18000,0 18000,0 12000,-6000 12000,-6000 6000,-24000 6000,-24000 24000,-18000 24000,-18000 30000
:  -18000 -18000,-18000 -12000,-12000 -12000,-12000 -18000
:  24000 -24000,6000 -24000,6000 -6000,12000 -6000,12000 0,18000 0,18000 -6000,24000 -6000
:  6000 24000,24000 24000,24000 21000,24000 9000,24000 6000,6000 6000,6000 9000,6000 21000
:   9000 21000,9000 9000,21000 9000,21000 21000,6000 21000
:    12000 12000,12000 18000,18000 18000,18000 12000";
         var lines = input.Split('\n').Map(s => s.Trim());
         var polygons = lines.Map<string, (bool, Polygon2)>(
            l => {
               var d = l.Skip(1).TakeWhile(char.IsWhiteSpace).Count();

               var parts = l.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Skip(1).ToArray();
               var asdf = new List<IntVector2>();
               for (var i = 0; i < parts.Length; i += 2) {
                  var x = int.Parse(parts[i + 0]);
                  var y = int.Parse(parts[i + 1]);
                  asdf.Add(new IntVector2(x, y));
               }
               if (asdf.Count == 0) return (false, (Polygon2)null);
               return (d % 2 == 0, new Polygon2(asdf));
            }).Where(x => x.Item2 != null).ToList();
         canvas.DrawPolygonContours(
            polygons.Where(p => !p.Item1)
                    .Select(p => p.Item2).ToList(), StrokeStyle.BlackHairLineSolid);
         foreach (var (i, poly) in polygons.Where(p => p.Item1).Select(p => p.Item2).ToList().Enumerate()) {
            canvas.DrawPolygonContour(poly,
               new[] {
                  new StrokeStyle(Color.Cyan, 10),
                  new StrokeStyle(Color.Magenta, 8),
                  new StrokeStyle(Color.Yellow, 6),
                  new StrokeStyle(Color.Lime, 4),
                  new StrokeStyle(Color.Blue, 2),
                  new StrokeStyle(Color.Violet),
               }[i]);
         }
         return;
         /**/

         foreach (var x in SectorMetadataPresets.Test2D.LocalIncludedContours) {
            Console.WriteLine("0 " + x.Points.Count + " " + string.Join(" ", x.Points.Map(p => p.X + " " + p.Y)));
         }
         foreach (var x in SectorMetadataPresets.Test2D.LocalExcludedContours) {
            Console.WriteLine("1 " + x.Points.Count + " " + string.Join(" ", x.Points.Map(p => p.X + " " + p.Y)));
         }

         var clipper = new Clipper(Clipper.ioStrictlySimple);
         clipper.AddPaths(
            SectorMetadataPresets.Test2D.LocalIncludedContours.Select(x => x.Points).ToList(),
            PolyType.ptSubject,
            true);
         clipper.AddPaths(
            SectorMetadataPresets.Test2D.LocalExcludedContours.Select(x => x.Points).ToList(),
            PolyType.ptClip,
            true);

         PolyTree RunIteration() {
            var pt = new PolyTree();
            clipper.Execute(ClipType.ctDifference, pt, PolyFillType.pftPositive);
            return pt;
         }

         void R(PolyNode n, int indent = 0) {
            Console.WriteLine(
               ":" +
               new string(' ', indent) +
               string.Join(", ",
                  n.Contour.Map(p => $"{p.X} {p.Y}"))
            );
//            Console.WriteLine(Clipper.Area(n.Contour));
            foreach (var c in ((IEnumerable<PolyNode>)n.Childs)
               .Reverse()) {
               R(c, indent + 1);
            }
         }

         R(RunIteration());

         var r = new Random(0);

         IntVector2 RandomPoint() {
            return new IntVector2(r.Next(-30000, 30000), r.Next(-30000, 30000));
         }

//         var ls = Util.Generate(10000, i => new IntLineSegment2(RandomPoint(), RandomPoint()));

//         Clipboard.SetText(string.Join("\r\n", ls.Map(s => s.First.X + " " + s.First.Y + " " + s.Second.X + " " + s.Second.Y)));

         /**
          */

         for (var trial = 0; trial < 50000; trial++) {
            RunIteration();
         }

         var sw = new Stopwatch();
         sw.Start();
         var ntrials = 100000;
         for (var trial = 0; trial < ntrials; trial++) {
            RunIteration();
         }
         sw.Stop();

         Console.WriteLine($"{ntrials} {sw.ElapsedMilliseconds}");
      }

      private static void RunTrial(IntLineSegment2[] ls) {
         long count = 0;
         for (var i = 0; i < ls.Length; i++) {
            for (var j = 0; j < ls.Length; j++) {
               if (ls[i].Intersects(ref ls[j])) {
                  count++;
               }
            }
         }
         Console.WriteLine(count);
      }
   }
}