using System.Collections.Generic;
using System.Linq;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.Declarations;

namespace OpenMOBA.Foundation.Terrain.CompilationResults.Overlay {
   public class TerrainOverlayNetworkManager {
      private readonly IReadOnlyList<SectorEdgeDescription> edgeDescriptions;
      private readonly Dictionary<SectorNodeDescription, LocalGeometryViewManager> localGeometryViewManagerBySectorNodeDescription;
      private readonly Dictionary<double, TerrainOverlayNetwork> terrainOverlayNetworkCache = new Dictionary<double, TerrainOverlayNetwork>();

      public TerrainOverlayNetworkManager(
         Dictionary<SectorNodeDescription, LocalGeometryViewManager> localGeometryViewManagerBySectorNodeDescription,
         IReadOnlyList<SectorEdgeDescription> edgeDescriptions
      ) {
         this.localGeometryViewManagerBySectorNodeDescription = localGeometryViewManagerBySectorNodeDescription;
         this.edgeDescriptions = edgeDescriptions;
      }

      public void InvalidateCaches() {
         terrainOverlayNetworkCache.Clear();
         foreach (var lgvm in localGeometryViewManagerBySectorNodeDescription.Values) lgvm.InvalidateCaches();
      }

      public TerrainOverlayNetwork CompileTerrainOverlayNetwork(double agentRadius) {
         if (terrainOverlayNetworkCache.TryGetValue(agentRadius, out var existingTerrainOverlayNetwork)) return existingTerrainOverlayNetwork;

         //         Console.WriteLine($"Compiling Terrain Overlay Network for Agent Radius {agentRadius}.");
         //----------------------------------------------------------------------------------------
         // Sector Node Description => Default Local Geometry View
         //----------------------------------------------------------------------------------------
         var renderedLocalGeometryViewBySectorNodeDescription = localGeometryViewManagerBySectorNodeDescription.Map(
            (k, v) => v.GetErodedView(agentRadius));

         var defaultLocalGeometryViewBySectorNodeDescription = renderedLocalGeometryViewBySectorNodeDescription.Map(
            (k, v) => true || v.IsPunchedLandEvaluated ? v : v.Preview);

         var landPolyNodesByDefaultLocalGeometryView = defaultLocalGeometryViewBySectorNodeDescription.Values.Distinct().ToDictionary(
            lgv => lgv,
            lgv => lgv.PunchedLand.GetLandNodes());

         var terrainNodesBySectorNodeDescription = defaultLocalGeometryViewBySectorNodeDescription.Map(
            (k, v) => landPolyNodesByDefaultLocalGeometryView[v].Map(pn => new TerrainOverlayNetworkNode(k, v, pn)));

         var terrainNodesBySectorNodeDescriptionAndPolyNode = terrainNodesBySectorNodeDescription.Values.SelectMany(tns => tns).ToDictionary(
            tn => (tn.SectorNodeDescription, tn.LandPolyNode));

         //----------------------------------------------------------------------------------------
         // Edge Lookups
         //----------------------------------------------------------------------------------------
         var edgesBySource = edgeDescriptions.ToLookup(ed => ed.Source);
         var edgesByDestination = edgeDescriptions.ToLookup(ed => ed.Destination);
         var edgesByEndpoints = MultiValueDictionary<SectorNodeDescription, SectorEdgeDescription>.Create(() => new HashSet<SectorEdgeDescription>());
         foreach (var (k, edges) in edgesBySource) {
            foreach (var edge in edges) {
               edgesByEndpoints.Add(k, edge);
            }
         }
         foreach (var (k, edges) in edgesByDestination) {
            foreach (var edge in edges) {
               edgesByEndpoints.Add(k, edge);
            }
         }

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
