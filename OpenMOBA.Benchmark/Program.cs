using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;

namespace OpenMOBA.Benchmark {
   public class Program {
      public static void Main(string[] args) {
         BenchmarkRunner.Run<HolePunch3DBenchmark>();
      }
   }

   public class HolePunch3DBenchmark {
      private readonly TerrainService terrainService;

      public HolePunch3DBenchmark() {
         var sectorGraphDescriptionStore = new SectorGraphDescriptionStore();
         var snapshotCompiler = new TerrainSnapshotCompiler(sectorGraphDescriptionStore);
         terrainService = new TerrainService(sectorGraphDescriptionStore, snapshotCompiler);
      }

      [Benchmark]
      public void LoadBunny() {
         terrainService.SnapshotCompiler.InvalidateCaches();
         terrainService.Clear();
         terrainService.LoadMeshAsMap("Assets/bunny.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 30000);
      }


      [IterationSetup(Target = nameof(CompileBunny))]
      public void CompileBunny_Setup() => LoadBunny();

      [Benchmark]
      public void CompileBunny() {
         terrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(15);
      }

      [IterationSetup(Target = nameof(IncrementallyRecompileHolePunchedBunny))]
      public void CompileHolePunchedBunny_Setup() {
         LoadBunny();
         CompileBunny();
      }

      [Benchmark]
      public void IncrementallyRecompileHolePunchedBunny() {
         PunchHoleIntoBunny();
         CompileBunny();
      }

      public void PunchHoleIntoBunny() {
         var sphereHole = terrainService.CreateHoleDescription(new SphereHoleStaticMetadata { Radius = 500 });
         sphereHole.WorldTransform = Matrix4x4.CreateTranslation(-561.450012207031f, -1316.31005859375f, -116.25f);
         terrainService.AddTemporaryHoleDescription(sphereHole);
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
