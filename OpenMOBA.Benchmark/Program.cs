using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;

namespace OpenMOBA.Benchmarks {
   public class Program {
      public static void Main(string[] args) {
//         BenchmarkRunner.Run<HolePunch3DBenchmark>();

         int j = 0;
         var sww = new Stopwatch();
         sww.Start();
         var s1 = new IntLineSegment2(new IntVector2(10, 20), new IntVector2(50, 50));
         var s2 = new IntLineSegment2(new IntVector2(50, 280), new IntVector2(50213, 502));
         var d = new Dictionary<int, int>();
         for (var i = 0; i < 8000000; i++) {
            d[i] = i;
//            s1.Intersects(s2);
//            Interlocked.Increment(ref j);
         }
         Console.WriteLine("Done " + sww.ElapsedMilliseconds);

         var benchmark = new HolePunch3DBenchmark();
         benchmark._ClearMapAndLoadBunny();
         for (var i = 0; i < 500; i++) {
            benchmark._InvalidateCaches();
            benchmark._CompileBunny();

            if (i == 0)
               PolyNodeCrossoverPointManager.DumpPerformanceCounters();
         }
         var sw = new Stopwatch();
         sw.Start();
         for (var i = 0; i < 10; i++) {
            benchmark._InvalidateCaches();
            benchmark._CompileBunny();
         }
         Console.WriteLine("10 iters: " + sw.ElapsedMilliseconds);
      }
   }

   public class DictionaryMapBenchmark {
      private readonly Dictionary<int, int> input = new Dictionary<int, int>();

      public DictionaryMapBenchmark() {
         for (var i = 0; i < 10000; i++) {
            input.Add(i, i);
         }
      }

      [Benchmark]
      public void FastMapBenchmark() => input.Map(Mapper);

      [Benchmark]
      public void ToDictionaryBenchmark() => input.ToDictionary(kvp => kvp.Key, kvp => Mapper(kvp.Key, kvp.Value));

      private string Mapper(int arg1, int arg2) {
         return $"A{arg1}.{arg2}";
      }
   }


   //
   //   public class GeometryBenchmark {
   //      private readonly Size mapDimensions;
   //      private readonly Polygon[] holePolygons;
   //
   //      public GeometryBenchmark() {
   //         mapDimensions = new Size(1000, 1000);
   //         var simpleHoles = new[] {
   //            Polygon.CreateRectXY(100, 100, 300, 300),
   //            Polygon.CreateRectXY(400, 200, 100, 100),
   //            Polygon.CreateRectXY(200, -50, 100, 150),
   //            Polygon.CreateRectXY(600, 600, 300, 300),
   //            Polygon.CreateRectXY(700, 500, 100, 100),
   //            Polygon.CreateRectXY(200, 700, 100, 100),
   //            Polygon.CreateRectXY(600, 100, 300, 50),
   //            Polygon.CreateRectXY(600, 150, 50, 200),
   //            Polygon.CreateRectXY(850, 150, 50, 200),
   //            Polygon.CreateRectXY(600, 350, 300, 50),
   //            Polygon.CreateRectXY(700, 200, 100, 100)
   //         };
   //
   //         var holeSquiggle = PolylineOperations.ExtrudePolygon(
   //            new[] {
   //               new IntVector2(100, 50),
   //               new IntVector2(100, 100),
   //               new IntVector2(200, 100),
   //               new IntVector2(200, 150),
   //               new IntVector2(200, 200),
   //               new IntVector2(400, 250),
   //               new IntVector2(200, 300),
   //               new IntVector2(400, 315),
   //               new IntVector2(200, 330),
   //               new IntVector2(210, 340),
   //               new IntVector2(220, 350),
   //               new IntVector2(220, 400),
   //               new IntVector2(221, 400)
   //            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygons();
   //         holePolygons = simpleHoles.Concat(holeSquiggle).ToArray();
   //
   //         hd = HoleDilation();
   //      }
   //
   ////      [Benchmark]
   //      public List<Polygon> HoleDilation() {
   //         return PolygonOperations.Offset().Include(holePolygons).Dilate(15).Execute().FlattenToPolygons();
   //      }
   //
   //      private List<Polygon> hd;
   //
   //      [Benchmark]
   //      public void VisibilityGraph() {
   //         VisibilityGraphOperations.CreateVisibilityGraph(mapDimensions, hd);
   //      }
   //   }
}
