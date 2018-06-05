using System;
using System.Diagnostics;
using System.Drawing;
using OpenMOBA;
using OpenMOBA.Debugging;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Geometry;

namespace QuadSegmentIntersection {
   public class Program {
      private static readonly Size bounds = new Size(1280, 720);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(50, 50), new OrthographicXYProjector());
      private static int frameCounter = 0;

      public static void Main(string[] args) {
         for (var i = 0; i < 100; i++) RenderVisualizationFrame();
         Benchmark();
      }

      private static void RenderVisualizationFrame() {
         var segments = Util.Generate(200, RandomSegment);
//         var quad = new[] { new IntVector2(100, 100), new IntVector2(200, 200), new IntVector2(300, 300), new IntVector2(400, 400) };
         var quad = Util.Generate(4, RandomPoint);
         var hull = GeometryOperations.ConvexHull4(quad[0], quad[1], quad[2], quad[3]);

         var canvas = host.CreateAndAddCanvas(frameCounter++);
         canvas.BatchDraw(() => {
            foreach (var (x, y) in quad.Zip(quad.RotateLeft())) canvas.DrawLine(x, y, StrokeStyle.BlackHairLineDashed5);
            foreach (var (x, y) in hull.Zip(hull.RotateLeft())) canvas.DrawLine(x, y, StrokeStyle.BlackHairLineSolid);
            foreach (var p in hull) canvas.DrawPoint(p, StrokeStyle.RedThick5Solid);
            foreach (var s in segments) canvas.DrawLine(s.First, s.Second, GeometryOperations.SegmentIntersectsConvexPolygonInterior(s, hull) ? StrokeStyle.LimeHairLineDashed5 : StrokeStyle.RedHairLineDashed5);
         });
      }

      private static void Benchmark() {
         var segments = Util.Generate(100000, RandomSegment);
         var quad = Util.Generate(4, RandomPoint);
         var hull = GeometryOperations.ConvexHull(quad);
         while (true) {
            var sw = new Stopwatch();
            sw.Start();
            const int niters = 100;
            for (var i = 0; i < niters; i++) {
               for (var j = 0; j < segments.Length; j++) {
                  GeometryOperations.SegmentIntersectsConvexPolygonInterior(segments[j], hull);
               }
            }
            Console.WriteLine(sw.Elapsed.TotalMilliseconds / niters);
         }
      }

      private static IntVector2 RandomPoint() {
         return new IntVector2(random.Next(0, bounds.Width), random.Next(0, bounds.Height));
      }

      private static IntLineSegment2 RandomSegment() {
         var b = RandomPoint();
         var o = new IntVector2(random.Next(-100, 100), random.Next(-100, 100));
         return new IntLineSegment2(b, b + o);
      }
   }
}