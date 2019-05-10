using System;
using System.Linq;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.ThirdParty.ClipperLib;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif


namespace Dargon.PlayOn.Foundation.Terrain.CompilationResults {
   public static class TerrainQueryOperations {
      /// <summary>
      /// Note: Containment definition varies by hole vs terrain: Containment for holes
      /// does not include the hole edge, containment for terrain includes the terrain edge.
      /// This is important, else e.g. knockback + terrain push placing an entity on an edge
      /// would potentially infinite loop.
      /// </summary>
      public static (bool isHole, cDouble distance) FindNearestLandPoint(this LocalGeometryView localGeometryView, DoubleVector2 query, out DoubleVector2 nearestLandPoint) {
         var punchedLandPolytree = localGeometryView.PunchedLand;
         punchedLandPolytree.AssertIsContourlessRootHolePunchResult();

         punchedLandPolytree.PickDeepestPolynode(query.LossyToIntVector2(), out var pickedNode, out var isHole);

         // If query point not in a hole, nearest land point is query point
         if (!isHole) {
            nearestLandPoint = query;
            return (false, CDoubleMath.c0);
         }

         // Else, two cases to consider: nearest point is on an island inside this hole, alternatively
         // and (only if the hole has a contour), nearest point is on the hole contour.
         nearestLandPoint = DoubleVector2.Zero;
         var bestDistance = cDouble.MaxValue;
         if (pickedNode.Contour.Any()) {
            // the hole has a contour; that is, it's a hole inside of a landmass
            var result = GeometryOperations.FindNearestPointOnContour(pickedNode.Contour, query);
            bestDistance = result.Distance;
            nearestLandPoint = result.NearestPoint;
         }

         foreach (var childLandNode in pickedNode.Childs) {
            var result = GeometryOperations.FindNearestPointOnContour(childLandNode.Contour, query);
            if (result.Distance < bestDistance) {
               bestDistance = result.Distance;
               nearestLandPoint = result.NearestPoint;
            }
         }
         return (true, bestDistance);
      }

      public static cDouble FindNearestLandPointNonrecursive(this PolyNode landNode, DoubleVector2 query, out DoubleVector2 nearestLandPoint) {
         landNode.AssertIsLandNode();

         var q = query.LossyToIntVector2();

         // Case 1: We're outside the land node.
         var landContourPipResult = Clipper.PointInPolygon(q, landNode.Contour);
         if (landContourPipResult == PolygonContainmentResult.OutsidePolygon) {
            var result = GeometryOperations.FindNearestPointOnContour(landNode.Contour, query);
            nearestLandPoint = result.NearestPoint;
            return result.Distance;
         }

         // Case 2: We're inside a child hole.
         foreach (var holeNode in landNode.Childs) {
            var holeContourPipResult = Clipper.PointInPolygon(q, holeNode.Contour);
            if (holeContourPipResult == PolygonContainmentResult.OutsidePolygon) continue;
            if (holeContourPipResult == PolygonContainmentResult.OnPolygon) break; // done, we're not in a hole
            var result = GeometryOperations.FindNearestPointOnContour(holeNode.Contour, query);
            nearestLandPoint = result.NearestPoint;
            return result.Distance;
         }

         // Case 3: We're in/on land node contour & not in a child hole.
         nearestLandPoint = query;
         return CDoubleMath.c0;
      }

      public static (DoubleVector3 world, Localization localization) FindNearestLandPointLocalization(this TerrainOverlayNetwork terrainOverlayNetwork, DoubleVector3 pWorld, cDouble computedRadius) {
         // var paddedHoleDilationRadius = computedRadius + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius + InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
         var bestWorldDistance = cDouble.MaxValue;
         var bestWorld = DoubleVector3.Zero;
         var bestLocal = DoubleVector2.Zero;
         TerrainOverlayNetworkNode bestNode = null;
         foreach (var terrainOverlayNode in terrainOverlayNetwork.TerrainNodes) {
            var pLocal = (DoubleVector2)terrainOverlayNode.SectorNodeDescription.WorldToLocal(pWorld);
            terrainOverlayNode.LandPolyNode.FindNearestLandPointNonrecursive(pLocal, out var pNearestLocal);

            var pNearestWorld = terrainOverlayNode.SectorNodeDescription.LocalToWorld(pNearestLocal);
            var worldDistance = pWorld.To(pNearestWorld).Norm2D();
            if (worldDistance < bestWorldDistance) {
               bestWorldDistance = worldDistance;
               bestWorld = pNearestWorld;
               bestLocal = pNearestLocal;
               bestNode = terrainOverlayNode;
            }
         }

         if (bestNode == null) throw new InvalidStateException();

         // ensure containment within triangulation
         if (!bestNode.LocalGeometryView.Triangulation.TryIntersect(bestLocal.X, bestLocal.Y, out var island, out var triangleIndex)) {
            throw new NotImplementedException();
         }
         return (bestWorld, new Localization(terrainOverlayNetwork, bestNode, bestLocal, bestLocal.LossyToIntVector2(), island, triangleIndex));
      }

      public static bool TryFindPreciseLocalization(this TerrainOverlayNetwork network, DoubleVector3 pWorld, double computedRadius, out Localization localization) {
         if (!network.TryFindTerrainOverlayNode(pWorld, out var node, out var localPosition) || // todo determinism on triangulation intersect
             !node.LocalGeometryView.Triangulation.TryIntersect(localPosition.X, localPosition.Y, out var island, out var triangleIndex)) {
            localization = default;
            return false;
         }
         
         localization = new Localization {
            TerrainOverlayNetwork = network,
            TerrainOverlayNetworkNode = node,
            LocalPosition = localPosition.XY,
            LocalPositionIv2 = localPosition.XY.LossyToIntVector2(),
            TriangulationIsland = island,
            TriangleIndex = triangleIndex,
         };
         return true;
      }
   }
}