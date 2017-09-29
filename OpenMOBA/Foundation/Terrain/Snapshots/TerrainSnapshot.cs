using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class LocalGeometryViewManager {
      public readonly LocalGeometryJob Job;
      public readonly LocalGeometryViewManager PreviewViewManager;
      private readonly Dictionary<double, LocalGeometryView> views = new Dictionary<double, LocalGeometryView>();

      public LocalGeometryViewManager(LocalGeometryJob job, LocalGeometryViewManager previewViewManager = null) {
         Job = job;
         PreviewViewManager = previewViewManager ?? this;
      }

      public LocalGeometryView GetErodedView(double actorRadius) {
         if (views.TryGetValue(actorRadius, out LocalGeometryView cachedView)) return cachedView;
         var preview = PreviewViewManager == this ? null : PreviewViewManager.GetErodedView(actorRadius);
         return views[actorRadius] = new LocalGeometryView(this, actorRadius, preview);
      }
   }

   public class LocalGeometryView {
      private const int kCrossoverAdditionalPathingDilation = 2;

      public readonly LocalGeometryViewManager LocalGeometryViewManager;
      public readonly double ActorRadius;
      public readonly LocalGeometryView Preview;

      public readonly int CrossoverErosionRadius;
      public readonly int CrossoverErosionDiameterSquared;
      public readonly int CrossoverDilationFactor;

      public LocalGeometryView(LocalGeometryViewManager localGeometryViewManager, double actorRadius, LocalGeometryView preview) {
         LocalGeometryViewManager = localGeometryViewManager;
         ActorRadius = actorRadius;
         Preview = preview ?? this;

         CrossoverErosionRadius = (int)Math.Ceiling((double)(ActorRadius * 2));
         CrossoverErosionDiameterSquared = 4 * CrossoverErosionRadius * CrossoverErosionRadius;
         CrossoverDilationFactor = CrossoverErosionRadius / 2 + kCrossoverAdditionalPathingDilation;
      }

      public LocalGeometryJob Job => LocalGeometryViewManager.Job;
      public bool IsPunchedLandEvaluated => _punchedLand != null;

      private PolyTree _dilatedHolesUnion;
      private IntLineSegment2?[] _erodedCrossoverSegments;
      private PolyTree _punchedLand;
      private Triangulation _triangulation;

      public PolyNode DilatedHolesUnion =>
         _dilatedHolesUnion ?? (_dilatedHolesUnion =
            PolygonOperations.Offset().Include(Job.TerrainStaticMetadata.LocalExcludedContours)
                             .Dilate(ActorRadius)
                             .Execute());

      public IntLineSegment2?[] ErodedBoundaryCrossoverSegments =>
         _erodedCrossoverSegments ?? (_erodedCrossoverSegments =
            Job.CrossoverSegments.Select(segment =>
               segment.TryErode(CrossoverErosionRadius, out IntLineSegment2 erosionResult)
                  ? erosionResult
                  : (IntLineSegment2?)null).ToArray());

      private PolyTree ComputeErodedOuterContour() =>
         PolygonOperations.Offset().Include(Job.TerrainStaticMetadata.LocalIncludedContours)
                          .Erode(ActorRadius)
                          .Execute();

      private IEnumerable<Polygon2> ComputeCrossoverLandPolys() =>
         ErodedBoundaryCrossoverSegments
            .Where(s => s.HasValue)
            .SelectMany(s => PolylineOperations.ExtrudePolygon(s.Value.Points, CrossoverDilationFactor)
                                               .FlattenToPolygons());

      public PolyTree PunchedLand =>
         _punchedLand ?? (_punchedLand =
            PostProcessPunchedLand(
               PolygonOperations.Punch()
                                .Include(ComputeErodedOuterContour().FlattenToPolygons())
                                .Include(ComputeCrossoverLandPolys())
                                .Exclude(DilatedHolesUnion.FlattenToPolygons())
                                .Execute()
            ));

      private PolyTree PostProcessPunchedLand(PolyTree punchedLand) {
         void PrunePolytree(PolyNode polyTree) {
            for (var i = polyTree.Childs.Count - 1; i >= 0; i--) {
               var child = polyTree.Childs[i];
               if (Math.Abs(Clipper.Area(child.Contour)) < 16 * 16) {
                  Console.WriteLine("Prune: " + Clipper.Area(child.Contour) + " " + child.Contour.Count);
                  polyTree.Childs.RemoveAt(i);
                  continue;
               }

               PrunePolytree(child);
            }
         }

         void TagSectorSnapshotAndGeometryContext(PolyNode node) {
            node.visibilityGraphNodeData.LocalGeometryView = this;
            node.Childs.ForEach(TagSectorSnapshotAndGeometryContext);
         }

         PrunePolytree(punchedLand);
         TagSectorSnapshotAndGeometryContext(punchedLand);
         return punchedLand;
      }

      public Triangulation Triangulation => _triangulation ?? (_triangulation = new Triangulator().Triangulate(PunchedLand));
   }

   public class TerrainOverlayNetworkManager {
      private readonly Dictionary<SectorNodeDescription, LocalGeometryViewManager> localGeometryViewManagerBySectorNodeDescription;
      private readonly HashSet<SectorEdgeDescription> edgeDescriptions;

      public TerrainOverlayNetworkManager(
         Dictionary<SectorNodeDescription, LocalGeometryViewManager> localGeometryViewManagerBySectorNodeDescription, 
         HashSet<SectorEdgeDescription> edgeDescriptions
      ) {
         this.localGeometryViewManagerBySectorNodeDescription = localGeometryViewManagerBySectorNodeDescription;
         this.edgeDescriptions = edgeDescriptions;
      }

      public void CompileTerrainOverlayNetwork(double agentRadius) {
         //----------------------------------------------------------------------------------------
         // Sector Node Description => Default Local Geometry View
         //----------------------------------------------------------------------------------------
         var renderedLocalGeometryViewBySectorNodeDescription = localGeometryViewManagerBySectorNodeDescription.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetErodedView(agentRadius));

         var defaultLocalGeometryViewBySectorNodeDescription = renderedLocalGeometryViewBySectorNodeDescription.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.IsPunchedLandEvaluated ? kvp.Value : kvp.Value.Preview);

         var landPolyNodesByDefaultLocalGeometryView = defaultLocalGeometryViewBySectorNodeDescription.Values.ToDictionary(
            lgv => lgv,
            lgv => lgv.PunchedLand.EnumerateLandNodes().ToList());

         var terrainNodesBySectorNodeDescription = defaultLocalGeometryViewBySectorNodeDescription.ToDictionary(
            kvp => kvp.Key,
            kvp => landPolyNodesByDefaultLocalGeometryView[kvp.Value].Map(pn => new TerrainOverlayNetworkNode(kvp.Key, kvp.Value, pn)));

         var terrainNodesBySectorNodeDescriptionAndPolyNode = terrainNodesBySectorNodeDescription.Values.SelectMany(tns => tns).ToDictionary(
            tn => (tn.SectorNodeDescription, tn.LandPolyNode));

         //----------------------------------------------------------------------------------------
         // Edge Lookups
         //----------------------------------------------------------------------------------------
         var edgesBySource = edgeDescriptions.ToLookup(ed => ed.Source);
         var edgesByDestination = edgeDescriptions.ToLookup(ed => ed.Destination);
         var edgesByEndpoints = edgeDescriptions.Select(ed => ed.Source.PairValue(ed))
                                                .Concat(edgeDescriptions.Select(ed => ed.Destination.PairValue(ed)))
                                                .Distinct()
                                                .ToLookup(kvp => kvp.Key, kvp => kvp.Value);

         //----------------------------------------------------------------------------------------
         // Build and Initialize Terrain Overlay Network
         //----------------------------------------------------------------------------------------
         var terrainOverlayNetwork = new TerrainOverlayNetwork(
            agentRadius, 
            defaultLocalGeometryViewBySectorNodeDescription,
            terrainNodesBySectorNodeDescription,
            landPolyNodesByDefaultLocalGeometryView,
            terrainNodesBySectorNodeDescriptionAndPolyNode,
            edgeDescriptions,
            edgesBySource,
            edgesByDestination,
            edgesByEndpoints);
      }

      //
      //         var terrainNodesByLocalGeometryView = landNodesByLocalGeometryView.ToDictionary(
      //            kvp => kvp.Key,
      //            kvp => kvp.Value.Select(landPolyNode => new TerrainOverlayNetworkNode(kvp.Key, landPolyNode)).ToHashSet()
      //         );
      //
      //         var terrainNodeByPolyNode = terrainNodesByLocalGeometryView
      //            .Values.SelectMany(terrainNodes => terrainNodes)
      //            .ToDictionary(terrainNode => terrainNode.LandPolyNode);
      //
      //         var edgesBySource = edgeDescriptions.ToLookup(ed => ed.Source);
      //         var edgesByDestination = edgeDescriptions.ToLookup(ed => ed.Destination);
      //         var edgesByEndpoints = edgeDescriptions.Select(ed => ed.Source.PairValue(ed))
      //                                                .Concat(edgeDescriptions.Select(ed => ed.Destination.PairValue(ed)))
      //                                                .Distinct()
      //                                                .ToLookup(kvp => kvp.Key, kvp => kvp.Value);
      //
      //         var terrainOverlayNetwork = new TerrainOverlayNetwork(holeDilationRadius, terrainNodeByPolyNode, edgesBySource, edgesByDestination, edgesByEndpoints);
      //
      //         foreach (var edgeDescription in edgeDescriptions) {
      //            var sourceLgv = localGeometryViews[edgeDescription.Source];
      //            var destinationLgv = localGeometryViews[edgeDescription.Destination];
      //            terrainOverlayNetwork.UpdateEdges(edgeDescription, sourceLgv, destinationLgv, true);
      //         }
      //      }
   }

   public class TerrainOverlayNetworkNode {
      public TerrainOverlayNetworkNode(SectorNodeDescription sectorNodeDescription, LocalGeometryView localGeometryView, PolyNode landPolyNode) {
         SectorNodeDescription = sectorNodeDescription;
         LocalGeometryView = localGeometryView;
         LandPolyNode = landPolyNode;
      }

      public readonly SectorNodeDescription SectorNodeDescription;
      public readonly LocalGeometryView LocalGeometryView;
      public readonly PolyNode LandPolyNode;
      public readonly AddOnlyOrderedHashSet<IntVector2[]> CrossoverPointSets = new AddOnlyOrderedHashSet<IntVector2[]>();
   }

   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<SectorSnapshot> SectorSnapshots { get; set; }
      public IReadOnlyList<DynamicTerrainHoleDescription> TemporaryHoles { get; set; }

      private readonly Dictionary<double, XErodedLocalGeometryView> erodedViews = new Dictionary<double, XErodedLocalGeometryView>();

      public XErodedLocalGeometryView GetErodedView(double actorRadius) {
         if (erodedViews.TryGetValue(actorRadius, out XErodedLocalGeometryView cachedContext)) return cachedContext;
         return erodedViews[actorRadius] = new XErodedLocalGeometryView(this, actorRadius);
      }
   }

   public class XErodedLocalGeometryView {
      public XErodedLocalGeometryView(TerrainSnapshot terrainSnapshot, double holeDilationRadius) {
         HoleDilationRadius = holeDilationRadius;
         this.TerrainSnapshot = terrainSnapshot;
      }

      public readonly TerrainSnapshot TerrainSnapshot;
      public readonly double HoleDilationRadius;
      public IReadOnlyList<SectorSnapshot> SectorSnapshots => TerrainSnapshot.SectorSnapshots;

      // Geometry context
      public Dictionary<SectorSnapshot, SectorSnapshotGeometryContext> GeometryContextsBySectorSnapshot { get; set; } = new Dictionary<SectorSnapshot, SectorSnapshotGeometryContext>();

      public SectorSnapshotGeometryContext GetGeometryContext(SectorSnapshot sectorSnapshot) {
         if (GeometryContextsBySectorSnapshot.TryGetValue(sectorSnapshot, out SectorSnapshotGeometryContext cachedResult)) return cachedResult;
         return GeometryContextsBySectorSnapshot[sectorSnapshot] = new SectorSnapshotGeometryContext(sectorSnapshot, HoleDilationRadius);
      }
   }

   public class Q {
      private readonly SectorSnapshot s;
      private readonly SectorSnapshotGeometryContext ssgc;

      public Q(SectorSnapshot s, SectorSnapshotGeometryContext ssgc) {
         this.s = s;
         this.ssgc = ssgc;
      }

      public void GCOw() {
//         var ecs = ssgc.ErodedCrossoverSegments;
//         var pl = ssgc.PunchedLand;
      }
   }

   public class SSGCPNWM {
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

      public SSGCPNWM(PolyNode polyNode) {
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

   public static class SSGCPNWMCalculator {
      public static void FindVisibleWaypointLinks() {

      }

      public static void BuildOptimalFirstPathLinkToWaypointLut(
         PathLink[][] waypointToWaypointLut
         ) {
      }
   }
}
