using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class TerrainOverlayNetwork {
      private readonly double agentRadius;

      private readonly Dictionary<SectorNodeDescription, LocalGeometryView> activeLocalGeometryViewBySectorNodeDescription;
      private readonly Dictionary<SectorNodeDescription, TerrainOverlayNetworkNode[]> activeTerrainNodesBySectorNodeDescription;
      private readonly Dictionary<LocalGeometryView, List<PolyNode>> landPolyNodesByDefaultLocalGeometryView;
      private readonly Dictionary<(SectorNodeDescription, PolyNode), TerrainOverlayNetworkNode> terrainNodesBySectorNodeDescriptionAndPolyNode;

      private readonly HashSet<SectorEdgeDescription> edges;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgesBySource;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgesByDestination;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgesByEndpoints;

      private readonly Dictionary<(SectorEdgeDescription, LocalGeometryView, LocalGeometryView), List<EdgeJob>> edgeJobCache = new Dictionary<(SectorEdgeDescription, LocalGeometryView, LocalGeometryView), List<EdgeJob>>();

      public TerrainOverlayNetwork(
         double agentRadius, 
         Dictionary<SectorNodeDescription, LocalGeometryView> activeLocalGeometryViewBySectorNodeDescription, 
         Dictionary<SectorNodeDescription, TerrainOverlayNetworkNode[]> activeTerrainNodesBySectorNodeDescription,
         Dictionary<LocalGeometryView, List<PolyNode>> landPolyNodesByDefaultLocalGeometryView,
         Dictionary<(SectorNodeDescription, PolyNode), TerrainOverlayNetworkNode> terrainNodesBySectorNodeDescriptionAndPolyNode,
         HashSet<SectorEdgeDescription> edges,
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgesBySource, 
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgesByDestination, 
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgesByEndpoints
      ) {
         this.agentRadius = agentRadius;
         this.activeLocalGeometryViewBySectorNodeDescription = activeLocalGeometryViewBySectorNodeDescription;
         this.activeTerrainNodesBySectorNodeDescription = activeTerrainNodesBySectorNodeDescription;
         this.landPolyNodesByDefaultLocalGeometryView = landPolyNodesByDefaultLocalGeometryView;
         this.terrainNodesBySectorNodeDescriptionAndPolyNode = terrainNodesBySectorNodeDescriptionAndPolyNode;
         this.edges = edges;
         this.edgesBySource = edgesBySource;
         this.edgesByDestination = edgesByDestination;
         this.edgesByEndpoints = edgesByEndpoints;
      }

      public void Initialize() {
         foreach (var edge in edges) {
            UpdateEdge(edge, false);
         }
      }

      private void UpdateEdge(SectorEdgeDescription edgeDescription, bool forceRender) {
         var sourceLgv = activeLocalGeometryViewBySectorNodeDescription[edgeDescription.Source];
         var destinationLgv = activeLocalGeometryViewBySectorNodeDescription[edgeDescription.Destination];

         // Compute Edge Job
         var key = (edgeDescription, sourceLgv, destinationLgv);
         List<EdgeJob> edgeJobs;
         if (!edgeJobCache.TryGetValue(key, out edgeJobs)) {
            var crossoverPointSpacing = Math.Max(5.0f, agentRadius * 0.1f);
            edgeJobs = edgeJobCache[key] = edgeDescription.EmitCrossoverJobs(crossoverPointSpacing, sourceLgv, destinationLgv);
         }

         // Update Source/Destination PolyNodes' Waypoint Sets
         foreach (var edgeJob in edgeJobs) {
            var sourceNode = terrainNodesBySectorNodeDescriptionAndPolyNode[(edgeDescription.Source, edgeJob.SourcePolyNode)];
            var destinationNode = terrainNodesBySectorNodeDescriptionAndPolyNode[(edgeDescription.Destination, edgeJob.DestinationPolyNode)];
            var (sourceCrossoverPoints, destinationCrossoverPoints) = ComputeEdgeCrossoverPoints(edgeJob.SourceSegment, edgeJob.DestinationSegment);

            var isNewSourceCrossoverPoints = sourceNode.CrossoverPointSets.Add(sourceCrossoverPoints);
            var isNewDestinationCrossoverPoints = destinationNode.CrossoverPointSets.Add(destinationCrossoverPoints);

            void HandleNewCrossoverPoints(TerrainOverlayNetworkNode node, IntVector2[] addedCps) {
               for (var i = 0; i < node.CrossoverPointSets.Count - 1; i++) {
                  var existingCps = node.CrossoverPointSets[i];
               }
            }

            if (isNewSourceCrossoverPoints) HandleNewCrossoverPoints(sourceNode, sourceCrossoverPoints);
            if (isNewDestinationCrossoverPoints) HandleNewCrossoverPoints(destinationNode, destinationCrossoverPoints);
            
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

         var longestSegmentLength = Math.Max(sourceSegmentLength, destinationSegmentLength);
         var crossoverPointSpacing = 5;
         var points = (int)Math.Ceiling(longestSegmentLength / crossoverPointSpacing) + 1;

         var sourceCrossoverPoints = new IntVector2[points]; 
         var destinationCrossoverPoints = new IntVector2[points]; 
         for (var i = 0; i < points; i++) {
            var t = i / (double)(points - 1);
            sourceCrossoverPoints[i] = (sourceSegment.First + t * sourceSegmentVector).LossyToIntVector2();
            destinationCrossoverPoints[i] = (destinationSegment.First + t * destinationSegmentVector).LossyToIntVector2();
         }
         return (sourceCrossoverPoints, destinationCrossoverPoints);
      }
   }


   public class JSSGCPNWM {
      private readonly PolyNode polyNode;
      private readonly IntVector2[] waypoints;
      private readonly PolyNodeVisibilityGraph visibilityGraph;

      /// <summary>
      /// [DestinationWaypointIndex][SourceWaypointIndex] => (next hop, total path cost)
      /// </summary>
      private readonly PathLink[][] waypointToWaypointLut;

      /// <summary>
      /// [CrossoverIndex] => visible waypoint links of (visible waypoint index, cost from crossover point to waypoint)[]
      /// </summary>
      private readonly List<PathLink[]> visibleWaypointLinksByCrossoverPointIndex = new List<PathLink[]>();

      /// <summary>
      /// [CrossoverIndex][WaypointIndex] => links of (next hop, total path cost)
      /// Note: final hop will be a waypoint to itself of nonzero cost. This should hop to the crossover point.
      /// </summary>
      private readonly List<PathLink[]> optimalLinkToWaypointsByCrossoverPointIndex = new List<PathLink[]>();

      /// <summary>
      /// [SourceCrossoverIndex][DestCrossoverIndex] = (first hop, total path cost),
      /// probably followed by a lookup of [DestCrossoverIndex][first hop] in optimalLinkToWaypointsByCrossoverPointIndex
      /// </summary>
      private readonly List<List<PathLink>> optimalLinkToOtherCrossoversByCrossoverPointIndex = new List<List<PathLink>>();

      public JSSGCPNWM(PolyNode polyNode) {
         this.polyNode = polyNode;
         waypoints = polyNode.FindAggregateContourCrossoverWaypoints();
         visibilityGraph = polyNode.ComputeVisibilityGraph();
         waypointToWaypointLut = visibilityGraph.BuildWaypointToWaypointLut();
      }

      //      private IReadOnlyList<CrossoverSnapshot> CrossoverSnapshots => polyNode.visibilityGraphNodeData.CrossoverSnapshots;
      //      private IReadOnlyList<IntLineSegment2> ErodedCrossoverSegments => polyNode.visibilityGraphNodeData.ErodedCrossoverSegments;

      //      public void Add(CrossoverSnapshot cs, IntVector2 crossoverPoint) {
      //         var crossoverPointIndex = crossoverPoints.Count;
      //
      //         // Find cost from crossoverPoint to visible waypoints
      //         var crossoverPointSeeingWaypointIndices = polyNode.ComputeCrossoverSeeingWaypoints(cs)
      //            .Where(wi => polyNode.SegmentInLandPolygonNonrecursive(waypoints[wi], crossoverPoint))
      //            .ToList();
      //         var crossoverPointWaypointLinks = crossoverPointSeeingWaypointIndices.Map(wi => {
      //            var cost = waypoints[wi].To(crossoverPoint).Norm2F();
      //            return new PathLink { PriorIndex = wi, TotalCost = cost };
      //         });
      //         visibleWaypointLinksByCrossoverPointIndex.Add(crossoverPointWaypointLinks);
      //
      //         // Cost from crossoverPoint to all waypoints
      //         var optimalLinkToWaypoints = waypoints.Map((waypoint, wi) => crossoverPointWaypointLinks.MinBy(cpwl => cpwl.TotalCost + waypointToWaypointLut[wi][cpwl.PriorIndex].TotalCost));
      //         optimalLinkToWaypointsByCrossoverPointIndex.Add(optimalLinkToWaypoints);
      //
      //         // Cost from crossoverPoint to other crossoverPoints...
      //         for (var otherCrossoverPointIndex = 0; otherCrossoverPointIndex < crossoverPoints.Count; otherCrossoverPointIndex++) {
      ////            var optimalLinkToWaypointsB = optimalLinkToWaypointsByCrossoverPointIndex[otherCrossoverPointIndex];
      ////            var optimalLinkFromCrossoverPointToOtherCrossoverPoint = crossoverPointWaypointLinks.MinBy(
      ////               cpwl => cpwl.TotalCost + )
      //         }
      //      }

      private struct Kappa {

      }
   }
}