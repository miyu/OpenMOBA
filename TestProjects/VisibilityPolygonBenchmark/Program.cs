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
         // RenderAlgorithmVisualizationFrames();
         RenderRandomVisualizationFrames();
         // Benchmark();
      }

      private static void RenderAlgorithmVisualizationFrames() {
         for (var i = 0; i < 9; i++) RandomInput();

         var (p, segments) = RandomInput();
         p = new DoubleVector2(bounds.Width / 2, bounds.Height / 2);
         segments = segments.Where(s => GeometryOperations.Clockness(p.X, p.Y, s.X1, s.Y1, s.X2, s.Y2) == Clockness.Clockwise).ToArray();

         for (var i = 0; i < 50; i++)
            RenderVisualizationFrame(p, segments, 0);

         for (var i = 0; i < 500; i++) {
            RenderVisualizationFrame(p, segments, i);
         }
      }

      private static void RenderRandomVisualizationFrames() {
         for (var i = 0; i < 100; i++) {
            var (p, segments) = RandomInput();
            p = new DoubleVector2(bounds.Width / 2, bounds.Height / 2);
            segments = segments.Where(s => GeometryOperations.Clockness(p.X, p.Y, s.X1, s.Y1, s.X2, s.Y2) == Clockness.Clockwise).ToArray();
            RenderVisualizationFrame(p, segments);
         }
      }

      private static void RenderVisualizationFrame(DoubleVector2 p, IntLineSegment2[] segments, int eventLimit = -1) {
         var canvas = host.CreateAndAddCanvas(frameCounter++);
         var vp = VisibilityPolygon.Create(p, segments, eventLimit);

         vp = new VisibilityPolygon(p);
         foreach (var seg in segments) {
            vp.Insert(seg);
         }
//         vp.Insert(new IntLineSegment2(p.LossyToIntVector2() + new IntVector2(50, -20), p.LossyToIntVector2() + new IntVector2(50, 20)));

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