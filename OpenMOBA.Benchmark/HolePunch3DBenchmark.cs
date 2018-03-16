using System.Numerics;
using BenchmarkDotNet.Attributes;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;

namespace OpenMOBA.Benchmarks {
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
}