using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class TerrainOverlayNetwork {
      private readonly double agentRadius;

      private readonly Dictionary<SectorNodeDescription, LocalGeometryView> activeLocalGeometryViewBySectorNodeDescription;
      private readonly Dictionary<SectorNodeDescription, TerrainOverlayNetworkNode[]> activeTerrainNodesBySectorNodeDescription;
      private readonly Dictionary<LocalGeometryView, List<PolyNode>> landPolyNodesByDefaultLocalGeometryView;
      private readonly Dictionary<(SectorNodeDescription, PolyNode), TerrainOverlayNetworkNode> terrainNodesBySectorNodeDescriptionAndPolyNode;

      private readonly IReadOnlyList<SectorEdgeDescription> edgeDescriptions;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsBySource;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByDestination;
      private readonly ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByEndpoints;

      private readonly Dictionary<(SectorEdgeDescription, LocalGeometryView, LocalGeometryView), List<EdgeJob>> edgeJobCache = new Dictionary<(SectorEdgeDescription, LocalGeometryView, LocalGeometryView), List<EdgeJob>>();

      public TerrainOverlayNetwork(
         double agentRadius, 
         Dictionary<SectorNodeDescription, LocalGeometryView> activeLocalGeometryViewBySectorNodeDescription, 
         Dictionary<SectorNodeDescription, TerrainOverlayNetworkNode[]> activeTerrainNodesBySectorNodeDescription,
         Dictionary<LocalGeometryView, List<PolyNode>> landPolyNodesByDefaultLocalGeometryView,
         Dictionary<(SectorNodeDescription, PolyNode), TerrainOverlayNetworkNode> terrainNodesBySectorNodeDescriptionAndPolyNode,
         IReadOnlyList<SectorEdgeDescription> edgeDescriptions,
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsBySource, 
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByDestination, 
         ILookup<SectorNodeDescription, SectorEdgeDescription> edgeDescriptionsByEndpoints
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

      public void Initialize() {
         foreach (var edge in edgeDescriptions) {
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

            var sourceCrossoverIndices = sourceNode.CrossoverPointManager.AddMany(edgeJob.SourceSegment, sourceCrossoverPoints);
            var destinationCrossoverIndices = destinationNode.CrossoverPointManager.AddMany(edgeJob.DestinationSegment, destinationCrossoverPoints);

            var edges = sourceCrossoverIndices.Zip(destinationCrossoverIndices, (sci, dci) => new TerrainOverlayNetworkEdge(sci, dci, 0))
                                              .ToArray();
            sourceNode.EdgeGroups.Add(new TerrainOverlayNetworkEdgeGroup(sourceNode, destinationNode, edges));
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

   /// <summary>
   /// Handles finding optimal path between crossoverpoints in a given terrainNode
   /// Waypoints refer to waypoints in the polynode visibility graph.
   /// CrossoverPoints are a tacked on concept by the terrain engine.
   /// </summary>
   public class PolyNodeCrossoverPointManager {
      private readonly PolyNode landPolyNode;

      //-------------------------------------------------------------------------------------------
      // Data from the PolyNode
      //-------------------------------------------------------------------------------------------
      private readonly IntVector2[] waypoints;
      private readonly PolyNodeVisibilityGraph visibilityGraph;

      /// <summary>
      /// [DestinationWaypointIndex][SourceWaypointIndex] => (next hop, total path cost)
      /// </summary>
      private readonly PathLink[][] waypointToWaypointLut;

      //-------------------------------------------------------------------------------------------
      // Data about Crossover Points
      //-------------------------------------------------------------------------------------------
      private readonly AddOnlyOrderedHashSet<IntVector2> crossoverPoints = new AddOnlyOrderedHashSet<IntVector2>();

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

      public PolyNodeCrossoverPointManager(PolyNode landPolyNode) {
         this.landPolyNode = landPolyNode;
         waypoints = landPolyNode.FindAggregateContourCrossoverWaypoints();
         visibilityGraph = landPolyNode.ComputeVisibilityGraph();
         waypointToWaypointLut = visibilityGraph.BuildWaypointToWaypointLut();
      }

      public IReadOnlyList<IntVector2> Waypoints => waypoints;
      public IReadOnlyList<IntVector2> CrossoverPoints => crossoverPoints;
      public IReadOnlyList<IReadOnlyList<PathLink>> OptimalLinkToOtherCrossoversByCrossoverPointIndex => optimalLinkToOtherCrossoversByCrossoverPointIndex;

      public int[] AddMany(DoubleLineSegment2 edgeSegment, IntVector2[] points) {
         var segmentSeeingWaypoints = landPolyNode.ComputeSegmentSeeingWaypoints(edgeSegment);
         return points.Map(p => {
            TryAdd(p, out int cpi);
            return cpi;
         });

         bool TryAdd(IntVector2 crossoverPoint, out int crossoverPointIndex) {
            if (!crossoverPoints.TryAdd(crossoverPoint, out crossoverPointIndex)) {
               return false;
            }

            // Find cost from crossoverPoint to visible waypoints - crazy inefficient (has visibility poly, atan)!
            var crossoverPointWaypointLinks =
            (from wi in segmentSeeingWaypoints
               let cost = waypoints[wi].To(crossoverPoint).Norm2F()
               let costSquared = cost * cost
               let visibilityPolygon = landPolyNode.ComputeWaypointVisibilityPolygons()[wi]
               where visibilityPolygon.Stab(crossoverPoint).MidpointDistanceToOriginSquared >= costSquared
               select new PathLink { PriorIndex = wi, TotalCost = cost }).ToArray();
            visibleWaypointLinksByCrossoverPointIndex.Add(crossoverPointWaypointLinks);

            // Cost from crossoverPoint to all waypoints
            var optimalLinkToWaypoints = waypoints.Map((waypoint, wi) => {
               var optimalLink = crossoverPointWaypointLinks.MinBy(cpwl => cpwl.TotalCost + waypointToWaypointLut[wi][cpwl.PriorIndex].TotalCost);
               return new PathLink { PriorIndex = optimalLink.PriorIndex, TotalCost = optimalLink.TotalCost + waypointToWaypointLut[wi][optimalLink.PriorIndex].TotalCost };
            });
            optimalLinkToWaypointsByCrossoverPointIndex.Add(optimalLinkToWaypoints);

            // Cost from crossoverPoint to other crossoverPoints...
            var optimalLinkToOtherCrossovers = new List<PathLink>();
            optimalLinkToOtherCrossoversByCrossoverPointIndex.Add(optimalLinkToOtherCrossovers);
            for (var otherCpi = 0; otherCpi < crossoverPoints.Count - 1; otherCpi++) {
               var otherOptimalLinkByWaypointIndex = optimalLinkToWaypointsByCrossoverPointIndex[otherCpi];
               var optimalLinkToOtherCrossoverPoint = crossoverPointWaypointLinks.MinBy(cpwl => cpwl.TotalCost + otherOptimalLinkByWaypointIndex[cpwl.PriorIndex].TotalCost);
               var optimalLinkFromOtherCrossoverPoint = otherOptimalLinkByWaypointIndex[optimalLinkToOtherCrossoverPoint.PriorIndex];
               var totalCost = optimalLinkToOtherCrossoverPoint.TotalCost + optimalLinkFromOtherCrossoverPoint.TotalCost;
               optimalLinkToOtherCrossoversByCrossoverPointIndex[otherCpi].Add(new PathLink {
                  PriorIndex = optimalLinkFromOtherCrossoverPoint.PriorIndex,
                  TotalCost = totalCost
               });
               optimalLinkToOtherCrossovers.Add(new PathLink {
                  PriorIndex = optimalLinkToOtherCrossoverPoint.PriorIndex,
                  TotalCost = totalCost
               });
            }
            return true;
         }
      }
   }
}