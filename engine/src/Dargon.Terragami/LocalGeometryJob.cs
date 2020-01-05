using System.Collections.Generic;
using System.Linq;
using Dargon.Commons;
using Dargon.PlayOn;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   public struct LocalGeometryJob {
      public readonly SectorBlueprint SectorBlueprint;
      public readonly HashSet<(IntLineSegment2 segment, Clockness inClockness)> CrossoverSegments;
      public readonly Dictionary<(DynamicTerrainHoleDescription desc, int version), (IReadOnlyList<Polygon2> holeIncludedContours, IReadOnlyList<Polygon2> holeExcludedContours)> DynamicHoles;

      public LocalGeometryJob(SectorBlueprint sectorBlueprint, HashSet<(IntLineSegment2 segment, Clockness inClockness)> crossoverSegments = null) {
         this.SectorBlueprint = sectorBlueprint;
         CrossoverSegments = crossoverSegments ?? new HashSet<(IntLineSegment2 segment, Clockness inClockness)>();
         DynamicHoles = new Dictionary<(DynamicTerrainHoleDescription, int), (IReadOnlyList<Polygon2> holeIncludedContours, IReadOnlyList<Polygon2> holeExcludedContours)>();
      }

      public override bool Equals(object obj) {
         return obj is LocalGeometryJob other && Equals(other);
      }

      public bool Equals(LocalGeometryJob other) {
         return SectorBlueprint == other.SectorBlueprint &&
                CrossoverSegments.SetEquals(other.CrossoverSegments) &&
                DynamicHoles.Keys.ToHashSet().SetEquals(other.DynamicHoles.Keys);
      }

      public override int GetHashCode() {
         var hash = SectorBlueprint.GetHashCode() * 397;
         hash = CrossoverSegments.Aggregate(hash, (current, x) => current ^ x.GetHashCode());
         hash = DynamicHoles.Keys.Aggregate(hash, (current, x) => current ^ (x.desc.GetHashCode() * (13 + 27 * x.version)));
         return hash;
      }
   }
}
