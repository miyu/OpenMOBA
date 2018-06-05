using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenMOBA.Foundation;

namespace OpenMOBA.Benchmarks {
   public class Program {
      public static void Main(string[] args) {
         var sww = new Stopwatch();
         sww.Start();
         for (var j = 0; j < 2; j++) {
            var d = new Dictionary<object, int>();
            for (var i = 0; i < 15000; i++) {
               d.Add(new object(), i);
            }
            d.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
         }
         Console.WriteLine("ins: " + sww.ElapsedMilliseconds);

         var bench = new HolePunch3DBenchmark();
         bench.LoadBunny();
         var snapshot = bench.terrainService.CompileSnapshot();
         void RunGC() {
            for (var i = 0; i < 10; i++)
               for (var k = 0; k <= GC.MaxGeneration; k++)
                  GC.Collect(k, GCCollectionMode.Forced, true, true);
         }
         for (var j = 0; j < 5; j++) {
            RunGC();
            var benchmarkMemoryAllocated = false;
            int niters = benchmarkMemoryAllocated ? 1 : 10;
            var ers = benchmarkMemoryAllocated && GC.TryStartNoGCRegion(1024 * 1024 * 240);
            var initialMemory = GC.GetTotalMemory(false);
            Console.WriteLine("TryStart: " + ers);
            var sw = new Stopwatch();
            sw.Start();
            int[] GetGcCollections() => Util.GenerateRange(GC.MaxGeneration + 1).Map(GC.CollectionCount);
            var initialCollections = GetGcCollections();
            for (var i = 0; i < niters; i++) {
               snapshot.OverlayNetworkManager.InvalidateCaches();
               var ton = snapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(15);
               //if (i == 0) {
               //   Console.WriteLine("Snapshot Nodes: " + snapshot.NodeDescriptions.Count + " Edges: " + snapshot.EdgeDescriptions.Count);
               //}
            }
            Console.WriteLine($"{niters}iter: {sw.ElapsedMilliseconds} => {sw.ElapsedMilliseconds / (float)niters}");
            Console.WriteLine($"GC ~{((GC.GetTotalMemory(false) - initialMemory) / 1000000.0):F2} MB; Collections: " + string.Join(", ", GetGcCollections().Map((x, i) => $"GEN{i} " + (x - initialCollections[i]))));
            if (ers) GC.EndNoGCRegion();
         }
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
