using System.Linq;
using ClipperLib;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.CompilationResults {
   public static class TerrainQueryOperations {
      // public static bool TryFindSector(this TerrainSnapshot terrainSnapshot, IntVector3 queryWorld, out SectorSnapshot result) {
      //    return TryFindSector(terrainSnapshot, queryWorld.ToDoubleVector3(), out result);
      // }

      // public static bool TryFindSector(this TerrainSnapshot terrainSnapshot, DoubleVector3 queryWorld, out SectorSnapshot result) {
      //    return terrainSnapshot.SectorSnapshots.TryFindFirst(sectorSnapshot => {
      //       var queryLocal = sectorSnapshot.WorldToLocal(queryWorld);
      //       var localBoundary = sectorSnapshot.StaticMetadata.LocalBoundary;
      //       return localBoundary.X <= queryLocal.X && queryLocal.X <= localBoundary.Right &&
      //              localBoundary.Y <= queryLocal.Y && queryLocal.Y <= localBoundary.Bottom;
      //    }, out result);
      // }

      // public static bool IsInHole(this SectorSnapshotGeometryContext sectorSnapshotGeometryContext, IntVector3 query) {
      //    var punchedLandPolytree = sectorSnapshotGeometryContext.PunchedLand;
      //    punchedLandPolytree.AssertIsContourlessRootHolePunchResult();
      // 
      //    PolyNode pickedNode;
      //    bool isHole;
      //    punchedLandPolytree.PickDeepestPolynode(query.XY, out pickedNode, out isHole);
      // 
      //    return isHole;
      // }

      /// <summary>
      /// Note: Containment definition varies by hole vs terrain: Containment for holes
      /// does not include the hole edge, containment for terrain includes the terrain edge.
      /// This is important, else e.g. knockback + terrain push placing an entity on an edge
      /// would potentially infinite loop.
      /// </summary>
      public static bool FindNearestLandPointAndIsInHole(this LocalGeometryView localGeometryView, DoubleVector2 query, out DoubleVector2 nearestLandPoint) {
         var punchedLandPolytree = localGeometryView.PunchedLand;
         punchedLandPolytree.AssertIsContourlessRootHolePunchResult();

         PolyNode pickedNode;
         bool isHole;
         punchedLandPolytree.PickDeepestPolynode(query.LossyToIntVector2(), out pickedNode, out isHole);

         // If query point not in a hole, nearest land point is query point
         if (!isHole) {
            nearestLandPoint = query;
            return false;
         }

         // Else, two cases to consider: nearest point is on an island inside this hole, alternatively
         // and (only if the hole has a contour), nearest point is on the hole contour.
         nearestLandPoint = DoubleVector2.Zero;
         double bestDistance = double.PositiveInfinity;
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
         return true;
      }
   }
}