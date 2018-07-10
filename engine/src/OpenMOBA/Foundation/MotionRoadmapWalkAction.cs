using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation {
   public class MotionRoadmapWalkAction : MotionRoadmapAction {
      public MotionRoadmapWalkAction(TerrainOverlayNetworkNode node, IntVector2 source, IntVector2 destination) {
         Node = node;
         Source = source;
         Destination = destination;
      }

      public readonly TerrainOverlayNetworkNode Node;
      public readonly IntVector2 Source;
      public readonly IntVector2 Destination;
   }
}