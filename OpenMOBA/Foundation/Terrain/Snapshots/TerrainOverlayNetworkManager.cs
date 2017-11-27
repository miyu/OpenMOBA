using System.Collections.Generic;
using System.Linq;
using OpenMOBA;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
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
         var renderedLocalGeometryViewBySectorNodeDescription = Enumerable.ToDictionary<KeyValuePair<SectorNodeDescription, LocalGeometryViewManager>, SectorNodeDescription, LocalGeometryView>(localGeometryViewManagerBySectorNodeDescription, kvp => kvp.Key,
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
         var edgesBySource = Enumerable.ToLookup<SectorEdgeDescription, SectorNodeDescription>(edgeDescriptions, ed => ed.Source);
         var edgesByDestination = Enumerable.ToLookup<SectorEdgeDescription, SectorNodeDescription>(edgeDescriptions, ed => ed.Destination);
         var edgesByEndpoints = Enumerable.Select<SectorEdgeDescription, KeyValuePair<SectorNodeDescription, SectorEdgeDescription>>(edgeDescriptions, ed => LinqExtensions.PairValue<SectorNodeDescription, SectorEdgeDescription>(ed.Source, ed))
                                                .Concat(Enumerable.Select<SectorEdgeDescription, KeyValuePair<SectorNodeDescription, SectorEdgeDescription>>(edgeDescriptions, ed => LinqExtensions.PairValue<SectorNodeDescription, SectorEdgeDescription>(ed.Destination, ed)))
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
}