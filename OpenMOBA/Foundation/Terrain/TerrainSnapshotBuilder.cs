using System.Collections.Generic;
using System.Linq;
using OpenMOBA.Foundation.Terrain.Snapshots;

namespace OpenMOBA.Foundation.Terrain {
   public interface ITerrainSnapshotBuilder {
      TerrainSnapshot BuildSnapshot();
   }

   public class TerrainSnapshotBuilder : ITerrainSnapshotBuilder {
      private readonly TerrainServiceStore store;
      private TerrainSnapshot cachedSnapshot;

      public TerrainSnapshotBuilder(TerrainServiceStore store) {
         this.store = store;
      }

      public TerrainSnapshot BuildSnapshot() {
         if (cachedSnapshot?.Version == store.Version) return cachedSnapshot;

         var sectorToSnapshot = new Dictionary<Sector, SectorSnapshot>();
         foreach (var sector in store.EnumerateSectors()) {
            var snapshot = new SectorSnapshot {
               Sector = sector,
               WorldTransform = sector.InstanceMetadata.WorldTransform,
               WorldTransformInv = sector.InstanceMetadata.WorldTransformInv,
               TemporaryHoles = new List<DynamicTerrainHole>(),
               CrossoverSnapshots = new List<CrossoverSnapshot>()
            };
            sectorToSnapshot.Add(sector, snapshot);
         }

         var crossovers = new List<CrossoverSnapshot>();
         foreach (var crossover in store.EnumerateCrossovers()) {
            var sectorA = sectorToSnapshot[crossover.A];
            var sectorB = sectorToSnapshot[crossover.B];
            var segmentA = sectorA.WorldToLocal(crossover.Segment);
            var segmentB = sectorB.WorldToLocal(crossover.Segment);

            var crossoverAToB = new CrossoverSnapshot {
               Crossover = crossover,
               LocalSegment = segmentA,
               RemoteSegment = segmentB,
               Remote = sectorB
            };

            var crossoverBToA = new CrossoverSnapshot {
               Crossover = crossover,
               LocalSegment = segmentA,
               RemoteSegment = segmentB,
               Remote = sectorA
            };

            sectorA.CrossoverSnapshots.Add(crossoverAToB);
            sectorB.CrossoverSnapshots.Add(crossoverBToA);

            crossovers.Add(crossoverAToB);
            crossovers.Add(crossoverBToA);
         }

         return cachedSnapshot = new TerrainSnapshot {
            Version = store.Version,
            Crossovers = crossovers,
            SectorSnapshots = sectorToSnapshot.Values.ToList(),
            TemporaryHoles = new DynamicTerrainHole[0]
         };
      }
   }
}
