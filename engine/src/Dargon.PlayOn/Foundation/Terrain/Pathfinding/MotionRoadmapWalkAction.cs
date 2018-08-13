using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Pathfinding {
   public class MotionRoadmapWalkAction : MotionRoadmapAction {
      public MotionRoadmapWalkAction(TerrainOverlayNetworkNode node, IntVector2 source, IntVector2 destination) {
         Node = node;
         Source = source;
         Destination = destination;
         SourceToDestinationUnit = source.To(destination).ToDoubleVector2().ToUnit();
         DestinationToSourceUnit = -SourceToDestinationUnit;
      }

      public readonly TerrainOverlayNetworkNode Node;
      public readonly IntVector2 Source;
      public readonly IntVector2 Destination;
      public readonly DoubleVector2 SourceToDestinationUnit;
      public readonly DoubleVector2 DestinationToSourceUnit;
   }
}