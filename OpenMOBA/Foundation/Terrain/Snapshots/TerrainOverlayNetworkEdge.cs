namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class TerrainOverlayNetworkEdge {
      public TerrainOverlayNetworkEdge(int sourceCrossoverIndex, int destinationCrossoverIndex, int cost) {
         SourceCrossoverIndex = sourceCrossoverIndex;
         DestinationCrossoverIndex = destinationCrossoverIndex;
         Cost = cost;
      }

      public readonly int SourceCrossoverIndex;
      public readonly int DestinationCrossoverIndex;
      public readonly int Cost;
   }
}