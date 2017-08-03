using System;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ClipperLib;
using OpenMOBA.Foundation.Terrain.Snapshots;

namespace OpenMOBA.Foundation.Terrain {
   public class Crossover {
      public Sector A { get; set; }
      public Sector B { get; set; }
      public IntLineSegment3 Segment { get; set; }
      public Matrix3x2 AToBTransformation { get; set; }
      public Matrix3x2 BToATransformation { get; set; }
   }

   public interface ITerrainServiceStore {
      void AddSector(Sector sector);
      void RemoveSector(Sector sector);
      void AddTemporaryHole(DynamicTerrainHole hole);
      void RemoveTemporaryHole(DynamicTerrainHole hole);
      void HackAddSectorCrossover(Crossover crossover);
   }

   public class TerrainServiceStore : ITerrainServiceStore {
      private readonly HashSet<Sector> sectors = new HashSet<Sector>();
      private readonly HashSet<Crossover> crossovers = new HashSet<Crossover>();
      public int Version { get; private set; }

      public IEnumerable<Sector> EnumerateSectors() => sectors;
      public IEnumerable<Crossover> EnumerateCrossovers() => crossovers;

      public void AddSector(Sector sector) {
         if (sectors.Add(sector)) {
            Version++;

//            foreach (var hole in temporaryHoles.Where(hole => hole.AbsoluteBounds.IntersectsWith(sector.AbsoluteBounds))) {
//               sectorsByHole.Add(hole, sector);
//               holesBySector.Add(sector, hole);
//            }
         }
      }

      public void RemoveSector(Sector sector) {
         if (sectors.Remove(sector)) {
            Version++;

//            HashSet<DynamicTerrainHole> holes;
//            if (holesBySector.TryGetValue(sector, out holes)) {
//               holesBySector.Remove(sector);
//
//               foreach (var hole in holes) {
//                  sectorsByHole.Remove(hole, sector);
//               }
//            }
         }
      }

      public void AddTemporaryHole(DynamicTerrainHole hole) {
//         if (temporaryHoles.Add(hole)) {
//            version++;
//
//            foreach (var sector in sectors.Where(sector => sector.AbsoluteBounds.IntersectsWith(hole.AbsoluteBounds))) {
//               holesBySector.Add(sector, hole);
//               sectorsByHole.Add(hole, sector);
//            }
//         }
      }

      public void RemoveTemporaryHole(DynamicTerrainHole hole) {
//         if (temporaryHoles.Remove(hole)) {
//            version++;
//
//            HashSet<Sector> sectors;
//            if (sectorsByHole.TryGetValue(hole, out sectors)) {
//               sectorsByHole.Remove(hole);
//
//               foreach (var sector in sectors) {
//                  holesBySector.Remove(sector, hole);
//               }
//            }
//         }
      }

      // this should be computed automagically at sector snapshot build
      public void HackAddSectorCrossover(Crossover crossover) {
         if (crossovers.Add(crossover)) {
            Version++;
         }
      }
   }

   public class TerrainService : ITerrainServiceStore, ITerrainSnapshotBuilder {
      private readonly TerrainServiceStore storage;
      private readonly TerrainSnapshotBuilder snapshotBuilder;

      public TerrainService(TerrainServiceStore storage, TerrainSnapshotBuilder snapshotBuilder) {
         this.storage = storage;
         this.snapshotBuilder = snapshotBuilder;
      }

      public Sector CreateSector(TerrainStaticMetadata metadata) => new Sector(this, metadata);
      public void AddSector(Sector sector) => storage.AddSector(sector);
      public void RemoveSector(Sector sector) => storage.RemoveSector(sector);
      public void AddTemporaryHole(DynamicTerrainHole hole) => storage.AddTemporaryHole(hole);
      public void RemoveTemporaryHole(DynamicTerrainHole hole) => storage.RemoveTemporaryHole(hole);
      public void HackAddSectorCrossover(Crossover crossover) => storage.HackAddSectorCrossover(crossover);

      public TerrainSnapshot BuildSnapshot() => snapshotBuilder.BuildSnapshot();
   }

   public static class TerrainHoleHelpers {
      public static bool ContainsPoint(this DynamicTerrainHole dynamicTerrainHole, double holeDilationRadius, DoubleVector3 point) {
         // Padding so that when flooring the point, we don't accidentally say a point isn't
         // in the hole when in reality, it is. 
         var paddedHoleShapeUnion = PolygonOperations.Offset()
                                                     .Dilate(holeDilationRadius)
                                                     .Include(dynamicTerrainHole.Polygons)
                                                     .Execute();

         PolyNode node;
         bool isHole;
         paddedHoleShapeUnion.PickDeepestPolynodeGivenHoleShapePolytree(point.XY.LossyToIntVector2(), out node, out isHole);

         // we want land inside the hole-shape-union because we want to know if we're in the hole, not a hole of the hole shape.
         return !isHole;
      }
   }
}
