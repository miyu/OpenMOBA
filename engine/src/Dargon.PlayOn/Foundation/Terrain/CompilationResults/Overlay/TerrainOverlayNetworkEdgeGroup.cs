namespace Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay {
   public class TerrainOverlayNetworkEdgeGroup {
      public readonly TerrainOverlayNetworkNode Source;
      public readonly TerrainOverlayNetworkNode Destination;
      public readonly EdgeJob EdgeJob;
      public readonly TerrainOverlayNetworkEdge[] Edges;

      public TerrainOverlayNetworkEdgeGroup(TerrainOverlayNetworkNode source, TerrainOverlayNetworkNode destination, EdgeJob edgeJob, TerrainOverlayNetworkEdge[] edges) {
         Source = source;
         Destination = destination;
         EdgeJob = edgeJob;
         Edges = edges;
      }
   }
}