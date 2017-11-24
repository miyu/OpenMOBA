using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using OpenMOBA;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Geometry;

namespace VisibilityPolygonBenchmark {
   public class Program {
      private static readonly Size bounds = new Size(1280, 720);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(50, 50), new OrthographicXYProjector());
      private static int frameCounter = 0;

      public static void Main(string[] args) {
         //for (var i = 0; i < 200; i++) RenderVisualizationFrame();
         Benchmark();
      }

      private static void RenderVisualizationFrame() {
         var (p, segments) = RandomInput();
         p = new DoubleVector2(bounds.Width / 2, bounds.Height / 2);
         segments = segments.Where(s => GeometryOperations.Clockness(p.X, p.Y, s.X1, s.Y1, s.X2, s.Y2) == Clockness.Clockwise).ToArray();

         var canvas = host.CreateAndAddCanvas(frameCounter++);
         var vp = VisibilityPolygon.Create(p, segments);

         canvas.BatchDraw(() => {
            canvas.DrawVisibilityPolygon(vp, 0, new FillStyle(Color.FromArgb(120, Color.Cyan)));
            foreach (var s in segments) canvas.DrawLine(s.First, s.Second, StrokeStyle.BlackHairLineSolid);
            canvas.DrawPoint(p, StrokeStyle.RedThick5Solid);
         });
      }

      private static void Benchmark() {
         var inputs = Util.Generate(5, RandomInput);
         while (true) {
            var sw = new Stopwatch();
            sw.Start();
            const int niters = 1000;
            for (var i = 0; i < niters; i++) {
               var (p, segments) = inputs[i % inputs.Length];
               VisibilityPolygon.Create(p, segments);
            }
            Console.WriteLine(sw.Elapsed.TotalMilliseconds / niters);
         }
      }

      private static (DoubleVector2, IntLineSegment2[]) RandomInput() {
         var segments = new IntLineSegment2[1000];
         for (var i = 0; i < segments.Length; i++) {
            var s = RandomSegment(); 
            if (segments.Take(i).Any(s.Intersects)) continue;
            segments[i] = s;
         }
         var p = RandomPoint();
         return (p.ToDoubleVector2(), segments);
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