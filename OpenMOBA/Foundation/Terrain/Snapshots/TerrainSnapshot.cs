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
      public readonly int CrossoverDilationFactor;

      public LocalGeometryView(LocalGeometryViewManager localGeometryViewManager, double actorRadius, LocalGeometryView preview) {
         LocalGeometryViewManager = localGeometryViewManager;
         ActorRadius = actorRadius;
         Preview = preview ?? this;

         CrossoverErosionRadius = (int)Math.Ceiling(ActorRadius * 2);
         CrossoverDilationFactor = (CrossoverErosionRadius / 2) + kCrossoverAdditionalPathingDilation;
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

         void TagBoundingVolumeHierarchies(PolyNode node) {
            var contourEdges = node.Contour.Zip(node.Contour.RotateLeft(), IntLineSegment2.Create).ToArray();
            var bvh = BvhILS2.Build(contourEdges);
            node.visibilityGraphNodeData.ContourBvh = bvh;
            node.Childs.ForEach(TagBoundingVolumeHierarchies);
         }

         PrunePolytree(punchedLand);
         TagSectorSnapshotAndGeometryContext(punchedLand);
         TagBoundingVolumeHierarchies(punchedLand);
         return punchedLand;
      }

      public Triangulation Triangulation => _triangulation ?? (_triangulation = new Triangulator().Triangulate(PunchedLand));
   }

   public class TerrainOverlayNetworkManager {
      private readonly Dictionary<SectorNodeDescription, LocalGeometryViewManager> localGeometryViewManagerBySectorNodeDescription;
      private readonly IReadOnlyList<SectorEdgeDescription> edgeDescriptions;
      private readonly Dictionary<double, TerrainOverlayNetwork> terrainOverlayNetworkCache = new Dictionary<double, TerrainOverlayNetwork>();

      public TerrainOverlayNetworkManager(
         Dictionary<SectorNodeDescription, LocalGeometryViewManager> localGeometryViewManagerBySectorNodeDescription, 
         IReadOnlyList<SectorEdgeDescription> edgeDescriptions
      ) {
         this.localGeometryViewManagerBySectorNodeDescription = localGeometryViewManagerBySectorNodeDescription;
         this.edgeDescriptions = edgeDescriptions;
      }

      public TerrainOverlayNetwork CompileTerrainOverlayNetwork(double agentRadius) {
         if (terrainOverlayNetworkCache.TryGetValue(agentRadius, out TerrainOverlayNetwork existingTerrainOverlayNetwork)) {
            return existingTerrainOverlayNetwork;
         }

//         Console.WriteLine($"Compiling Terrain Overlay Network for Agent Radius {agentRadius}.");
         //----------------------------------------------------------------------------------------
         // Sector Node Description => Default Local Geometry View
         //----------------------------------------------------------------------------------------
         var renderedLocalGeometryViewBySectorNodeDescription = localGeometryViewManagerBySectorNodeDescription.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetErodedView(agentRadius));

         var defaultLocalGeometryViewBySectorNodeDescription = renderedLocalGeometryViewBySectorNodeDescription.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.IsPunchedLandEvaluated ? kvp.Value : kvp.Value.Preview);
         
         var landPolyNodesByDefaultLocalGeometryView = defaultLocalGeometryViewBySectorNodeDescription.Values.Distinct().ToDictionary(
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
         terrainOverlayNetwork.Initialize();
         return terrainOverlayNetworkCache[agentRadius] = terrainOverlayNetwork;
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
   }

   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<SectorNodeDescription> NodeDescriptions { get; set; }
      public IReadOnlyList<SectorEdgeDescription> EdgeDescriptions { get; set; }
      public TerrainOverlayNetworkManager OverlayNetworkManager { get; set; }
   }
}
