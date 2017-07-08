using System.Linq;
using ClipperLib;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain {
   public static class TerrainQueryOperations {
      public static bool IsInHole(this SectorSnapshot sectorSnapshot, double holeDilationRadius, IntVector3 query) {
         var punchedLandPolytree = sectorSnapshot.ComputePunchedLand(holeDilationRadius);
         punchedLandPolytree.AssertIsContourlessRootHolePunchResult();

         PolyNode pickedNode;
         bool isHole;
         punchedLandPolytree.PickDeepestPolynode(query.XY, out pickedNode, out isHole);

         return isHole;
      }
      
      /// <summary>
      /// Note: Containment definition varies by hole vs terrain: Containment for holes
      /// does not include the hole edge, containment for terrain includes the terrain edge.
      /// This is important, else e.g. knockback + terrain push placing an entity on an edge
      /// would potentially infinite loop.
      /// </summary>
      public static bool FindNearestLandPointAndIsInHole(this SectorSnapshot sectorSnapshot, double holeDilationRadius, DoubleVector2 query, out DoubleVector2 nearestLandPoint) {
         var punchedLandPolytree = sectorSnapshot.ComputePunchedLand(holeDilationRadius);
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