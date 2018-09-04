using System.Collections.Generic;
using System.Linq;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.ThirdParty.ClipperLib;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay {
   public class TerrainOverlayNetwork {
      private readonly Dictionary<SectorNodeDescription, LocalGeometryView> activeLocalGeometryViewBySectorNodeDescription;
      private readonly Dictionary<SectorNodeDescription, TerrainOverlayNetworkNode[]> activeTerrainNodesBySectorNodeDescription;
      private readonly cDouble agentRadius;

      private readonly IReadOnlyList<SectorEdgeDescription> edgeDescriptions;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByDestination;
      private readonly MultiValueDictionary<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByEndpoints;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsBySource;

      private readonly Dictionary<(SectorEdgeDescription, LocalGeometryView, LocalGeometryView), List<EdgeJob>> edgeJobCache = new Dictionary<(SectorEdgeDescription, LocalGeometryView, LocalGeometryView), List<EdgeJob>>();
      private readonly Dictionary<LocalGeometryView, List<PolyNode>> landPolyNodesByDefaultLocalGeometryView;
      private readonly Dictionary<(SectorNodeDescription, PolyNode), TerrainOverlayNetworkNode> terrainNodesBySectorNodeDescriptionAndPolyNode;

      public TerrainOverlayNetwork(
         cDouble agentRadius,
         Dictionary<SectorNodeDescription, LocalGeometryView> activeLocalGeometryViewBySectorNodeDescription,
         Dictionary<SectorNodeDescription, TerrainOverlayNetworkNode[]> activeTerrainNodesBySectorNodeDescription,
         Dictionary<LocalGeometryView, List<PolyNode>> landPolyNodesByDefaultLocalGeometryView,
         Dictionary<(SectorNodeDescription, PolyNode), TerrainOverlayNetworkNode> terrainNodesBySectorNodeDescriptionAndPolyNode,
         IReadOnlyList<SectorEdgeDescription> edgeDescriptions,
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsBySource,
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByDestination,
         MultiValueDictionary<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByEndpoints
      ) {
         this.agentRadius = agentRadius;
         this.activeLocalGeometryViewBySectorNodeDescription = activeLocalGeometryViewBySectorNodeDescription;
         this.activeTerrainNodesBySectorNodeDescription = activeTerrainNodesBySectorNodeDescription;
         this.landPolyNodesByDefaultLocalGeometryView = landPolyNodesByDefaultLocalGeometryView;
         this.terrainNodesBySectorNodeDescriptionAndPolyNode = terrainNodesBySectorNodeDescriptionAndPolyNode;
         this.edgeDescriptions = edgeDescriptions;
         this.edgeDescriptionsBySource = edgeDescriptionsBySource;
         this.edgeDescriptionsByDestination = edgeDescriptionsByDestination;
         this.edgeDescriptionsByEndpoints = edgeDescriptionsByEndpoints;
      }

      public IReadOnlyCollection<TerrainOverlayNetworkNode> TerrainNodes => terrainNodesBySectorNodeDescriptionAndPolyNode.Values;
      public BvhTreeAABB<TerrainOverlayNetworkNode> NodeBvh { get; private set; }

      public void Initialize() {
         foreach (var node in TerrainNodes) {
            node.Network = this;
         }

         NodeBvh = BvhTreeAABB<TerrainOverlayNetworkNode>.Build(TerrainNodes.Select(n => n.SectorNodeDescription.WorldBounds.PairValue(n)));
         foreach (var edge in edgeDescriptions) UpdateEdge(edge, false);
      }

      private void UpdateEdge(SectorEdgeDescription edgeDescription, bool forceRender) {
         var sourceLgv = activeLocalGeometryViewBySectorNodeDescription[edgeDescription.Source];
         var destinationLgv = activeLocalGeometryViewBySectorNodeDescription[edgeDescription.Destination];

         // Compute Edge Job
         var key = (edgeDescription, sourceLgv, destinationLgv);
         List<EdgeJob> edgeJobs;
         if (!edgeJobCache.TryGetValue(key, out edgeJobs)) {
            var crossoverPointSpacing = CDoubleMath.Max(CDoubleMath.c5, agentRadius * CDoubleMath.c0_1);
            edgeJobs = edgeJobCache[key] = edgeDescription.EmitCrossoverJobs(crossoverPointSpacing, sourceLgv, destinationLgv);
         }

         // Update Source/Destination PolyNodes' Waypoint Sets
         foreach (var edgeJob in edgeJobs) {
            var sourceNode = terrainNodesBySectorNodeDescriptionAndPolyNode[(edgeDescription.Source, edgeJob.SourcePolyNode)];
            var destinationNode = terrainNodesBySectorNodeDescriptionAndPolyNode[(edgeDescription.Destination, edgeJob.DestinationPolyNode)];
            var (sourceCrossoverPoints, destinationCrossoverPoints) = ComputeEdgeCrossoverPoints(edgeJob.SourceSegment, edgeJob.DestinationSegment);

            var sourceCrossoverIndices = sourceNode.CrossoverPointManager.AddMany(edgeJob.SourceSegment, sourceCrossoverPoints);
            var destinationCrossoverIndices = destinationNode.CrossoverPointManager.AddMany(edgeJob.DestinationSegment, destinationCrossoverPoints);

            var edges = new TerrainOverlayNetworkEdge[sourceCrossoverIndices.Length];
            for (var i = 0; i < edges.Length; i++) edges[i] = new TerrainOverlayNetworkEdge(sourceCrossoverIndices[i], destinationCrossoverIndices[i], 0);
            //            var edges = sourceCrossoverIndices.Zip(destinationCrossoverIndices, (sci, dci) => new TerrainOverlayNetworkEdge(sci, dci, 0))
            //                                              .ToArray();
            var edgeGroup = new TerrainOverlayNetworkEdgeGroup(sourceNode, destinationNode, edgeJob, edges);
            sourceNode.OutboundEdgeGroups.Add(destinationNode, edgeGroup);
            destinationNode.InboundEdgeGroups.Add(sourceNode, edgeGroup);
         }

         // Flag Source/Destination PolyNodes as dirty.
         //         JSSGCPNWM j = null;
         //         foreach (var (sourcePolyNode, destinationPolyNode, sourcePoint, destinationPoint) in edgeJobs.CrossoverJobs) {
         //            var sourceTerrainNode = terrainNodesBySectorNodeDescriptionAndPolyNode[(edgeDescription.Source, sourcePolyNode)];
         //            var destinationTerrainNode = terrainNodesBySectorNodeDescriptionAndPolyNode[(edgeDescription.Destination, destinationPolyNode)];
         //         }
      }

      private (IntVector2[], IntVector2[]) ComputeEdgeCrossoverPoints(DoubleLineSegment2 sourceSegment, DoubleLineSegment2 destinationSegment) {
         var sourceSegmentVector = sourceSegment.First.To(sourceSegment.Second);
         var sourceSegmentLength = sourceSegmentVector.Norm2D();

         var destinationSegmentVector = destinationSegment.First.To(destinationSegment.Second);
         var destinationSegmentLength = destinationSegmentVector.Norm2D();

         var longestSegmentLength = CDoubleMath.Max(sourceSegmentLength, destinationSegmentLength);
         var crossoverPointSpacing = (cDouble)1000;
         var points = (int)CDoubleMath.Ceiling(longestSegmentLength / crossoverPointSpacing) + 1;

         var sourceCrossoverPoints = new IntVector2[points];
         var destinationCrossoverPoints = new IntVector2[points];
         for (var i = 0; i < points; i++) {
            var t = (cDouble)i / (cDouble)(points - 1);
            sourceCrossoverPoints[i] = (sourceSegment.First + t * sourceSegmentVector).LossyToIntVector2();
            destinationCrossoverPoints[i] = (destinationSegment.First + t * destinationSegmentVector).LossyToIntVector2();
         }

         return (sourceCrossoverPoints, destinationCrossoverPoints);
      }

      private void Dijkstra(TerrainOverlayNetworkNode node) {
         //         node.CrossoverPointManager
      }
   }
}
