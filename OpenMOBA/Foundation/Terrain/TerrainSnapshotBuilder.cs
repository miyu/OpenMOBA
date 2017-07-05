using System;
using System.Collections.Generic;
using System.Linq;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;

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
         if (cachedSnapshot?.Version == store.Version) {
            return cachedSnapshot;
         }

         var sectorToSnapshot = new Dictionary<Sector, SectorSnapshot>();
         foreach (var sector in store.EnumerateSectors()) {
            var snapshot = new SectorSnapshot {
               Sector = sector,
               WorldTransform = sector.InstanceMetadata.WorldTransform,
               WorldTransformInv = sector.InstanceMetadata.WorldTransformInv,
               TemporaryHoles = new DynamicTerrainHole[0],
               Crossovers = new Dictionary<SectorSnapshot, List<CrossoverSnapshot>>()
            };
            sectorToSnapshot.Add(sector, snapshot);
         }
         
         var crossoverToSnapshot = new Dictionary<Crossover, CrossoverSnapshot>();
         foreach (var crossover in store.EnumerateCrossovers()) {
            var snapshot = new CrossoverSnapshot {
               A = sectorToSnapshot[crossover.A],
               B = sectorToSnapshot[crossover.B],
               Segment = crossover.Segment
            };
          
            List<CrossoverSnapshot> aToBCrossovers;
            if (!snapshot.A.Crossovers.TryGetValue(snapshot.B, out aToBCrossovers)) {
               aToBCrossovers = new List<CrossoverSnapshot>();
               snapshot.A.Crossovers[snapshot.B] = aToBCrossovers;
            }
            aToBCrossovers.Add(snapshot);
         
            List<CrossoverSnapshot> bToACrossovers;
            if (!snapshot.B.Crossovers.TryGetValue(snapshot.A, out bToACrossovers)) {
               bToACrossovers = new List<CrossoverSnapshot>();
               snapshot.B.Crossovers[snapshot.A] = bToACrossovers;
            }
            bToACrossovers.Add(snapshot);
         
            crossoverToSnapshot[crossover] = snapshot;
         }
         
         foreach (var sectorSnapshot in sectorToSnapshot.Values) {
            sectorSnapshot.BuildCrossoverVisibilityStructures();
         }
         
         return cachedSnapshot = new TerrainSnapshot {
            Version = store.Version,
            CrossoverSnapshots = new CrossoverSnapshot[0],
            SectorSnapshots = sectorToSnapshot.Values.ToList(),
            TemporaryHoles = new DynamicTerrainHole[0]
         };
      }
   }
}