using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.Declarations;

namespace OpenMOBA.Foundation.Terrain.CompilationResults.Overlay {
   public class TerrainOverlayNetworkNode {
      public TerrainOverlayNetworkNode(SectorNodeDescription sectorNodeDescription, LocalGeometryView localGeometryView, PolyNode landPolyNode) {
         SectorNodeDescription = sectorNodeDescription;
         LocalGeometryView = localGeometryView;
         LandPolyNode = landPolyNode;

         CrossoverPointManager = new PolyNodeCrossoverPointManager(landPolyNode);
      }

      public readonly SectorNodeDescription SectorNodeDescription;
      public readonly LocalGeometryView LocalGeometryView;
      public readonly PolyNode LandPolyNode;
      public readonly PolyNodeCrossoverPointManager CrossoverPointManager;
      public readonly MultiValueDictionary<TerrainOverlayNetworkNode, TerrainOverlayNetworkEdgeGroup> InboundEdgeGroups = new MultiValueDictionary<TerrainOverlayNetworkNode, TerrainOverlayNetworkEdgeGroup>();
      public readonly MultiValueDictionary<TerrainOverlayNetworkNode, TerrainOverlayNetworkEdgeGroup> OutboundEdgeGroups = new MultiValueDictionary<TerrainOverlayNetworkNode, TerrainOverlayNetworkEdgeGroup>();
      public TerrainOverlayNetwork Network;

      public override string ToString() => SectorNodeDescription.ToString();
   }
}