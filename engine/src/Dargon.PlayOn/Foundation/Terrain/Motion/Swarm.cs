using System.Collections.Generic;
using System.Diagnostics;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class Swarm {
      private readonly Dictionary<int, (TerrainOverlayNetworkNode, PathfinderResultContext)> pathfinderResultContextByComputedRadius = new Dictionary<int, (TerrainOverlayNetworkNode, PathfinderResultContext)>();
      private DoubleVector3 destination;

      public List<Entity> Entities { get; set; } = new List<Entity>();

      public DoubleVector3 Destination {
         get => destination;
         set => SetDestination(value);
      }

      public void SetDestination(DoubleVector3 value) {
         destination = value;
         pathfinderResultContextByComputedRadius.Clear();
      }

      public PathfinderResultContext GetPriorPathfinderResultContextOrNull(int computedRadius, TerrainOverlayNetworkNode destinationNode) {
         if (!pathfinderResultContextByComputedRadius.TryGetValue(computedRadius, out var tuple) ||
             tuple.Item1 != destinationNode) {
            return null;
         }
         Trace.Assert(tuple.Item2 != null);
         return tuple.Item2;
      }

      public void SetPriorPathfinderResultContext(int computedRadius, TerrainOverlayNetworkNode destinationNode, PathfinderResultContext pathfinderResultContext) {
         pathfinderResultContextByComputedRadius[computedRadius] = (destinationNode, pathfinderResultContext);
      }
   }
}