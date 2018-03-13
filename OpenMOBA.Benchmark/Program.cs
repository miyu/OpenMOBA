using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Jobs;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using OpenMOBA.Foundation.Terrain.Snapshots;

namespace OpenMOBA.Benchmark {
   public class Program {
      public static void Main(string[] args) {
         LinqExtensions.DictionaryMapper<string, int, string>.CloneIntArray(new int[0]);
         return;

         int j = 0;
         var sww = new Stopwatch();
         sww.Start();
         for (var i = 0; i < 1000000; i++) {
            Interlocked.Increment(ref j);
         }
         Console.WriteLine("Done " + sww.ElapsedMilliseconds);

         var benchmark = new HolePunch3DBenchmark();
         benchmark._ClearMapAndLoadBunny();
         var sw = new Stopwatch();
         sw.Start();
         for (var i = 0; i < 10; i++) {
            benchmark._InvalidateCaches();
            benchmark._CompileBunny();

            var ton = benchmark.terrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(15);
            var terrainNodes = ton.TerrainNodes.ToArray();
            PolyNodeCrossoverPointManager.DumpPerformanceCounters();
            Console.WriteLine("@t=" + sw.ElapsedMilliseconds);
            GC.KeepAlive(terrainNodes);
         }
         Console.WriteLine("10 iters: " + sw.ElapsedMilliseconds);

//         BenchmarkRunner.Run<HolePunch3DBenchmark>();
      }
   }


   [Config(typeof(FastAndDirtyConfig))]
   public class HolePunch3DBenchmark {
      public readonly TerrainService terrainService;

      public HolePunch3DBenchmark() {
         var sectorGraphDescriptionStore = new SectorGraphDescriptionStore();
         var snapshotCompiler = new TerrainSnapshotCompiler(sectorGraphDescriptionStore);
         terrainService = new TerrainService(sectorGraphDescriptionStore, snapshotCompiler);
      }

      [Benchmark]
      public void LoadBunny() => _ClearMapAndLoadBunny();

      [Benchmark]
      public void CompileBunny() {
         _InvalidateCaches();
         _CompileBunny();
      }


      [IterationSetup(Target = nameof(CompileBunny))]
      void CompileBunny_Setup() => _ClearMapAndLoadBunny();


      [Benchmark]
      public void IncrementallyRecompileHolePunchedBunny() {
         _PunchHoleIntoBunny();
         _CompileBunny();
      }

      [IterationSetup(Target = nameof(IncrementallyRecompileHolePunchedBunny))]
      public void CompileHolePunchedBunny_Setup() {
         _ClearMapAndLoadBunny();
         _CompileBunny();
      }

      public void _ClearMapAndLoadBunny() {
         terrainService.Clear();
         terrainService.LoadMeshAsMap("Assets/bunny.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 30000);
      }

      public void _InvalidateCaches() => terrainService.SnapshotCompiler.InvalidateCaches();

      public void _CompileBunny() => terrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(15);

      public void _PunchHoleIntoBunny() {
         var sphereHole = terrainService.CreateHoleDescription(new SphereHoleStaticMetadata { Radius = 500 });
         sphereHole.WorldTransform = Matrix4x4.CreateTranslation(-561.450012207031f, -1316.31005859375f, -116.25f);
         terrainService.AddTemporaryHoleDescription(sphereHole);
      }
   }

   public class FastAndDirtyConfig : ManualConfig {
      public FastAndDirtyConfig() {
         Add(DefaultConfig.Instance); // *** add default loggers, reporters etc? ***

         Add(Job.Default
                .WithLaunchCount(1) // benchmark process will be launched only once
                .WithIterationTime(100 * TimeInterval.Millisecond) // 100ms per iteration
                .WithWarmupCount(3) // 3 warmup iteration
                .WithTargetCount(3) // 3 target iteration
         );
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
