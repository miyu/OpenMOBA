using System.Collections.Generic;
using Dargon.Commons.Collections;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.ThirdParty.ClipperLib;

namespace Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay {
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
      public readonly Dictionary<DoubleLineSegment2, TerrainOverlayNetworkEdgeGroup> OutboundEdgeGroupsBySegment = new Dictionary<DoubleLineSegment2, TerrainOverlayNetworkEdgeGroup>();
      public readonly List<(DoubleLineSegment2, Clockness)> OutboundEdgeSegments = new List<(DoubleLineSegment2, Clockness)>();
      public TerrainOverlayNetwork Network; // populated by TONN

      public override string ToString() => SectorNodeDescription.ToString();
   }
}