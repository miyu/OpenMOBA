using System;
using System.Diagnostics;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.ECS {
   public class TriangulationWalker {
      public DoubleVector2 WalkTriangulation(TriangulationIsland island, int triangleIndex, DoubleVector2 p, DoubleVector2 preferredDirectionUnit, Double distanceRemaining) {
         var allowPushIntoTriangle = true;
         while (distanceRemaining > GeometryOperations.kEpsilon) {
            DoubleVector2 np;
            int nti;
            var walkResult = WalkTriangle(p, preferredDirectionUnit, distanceRemaining, island, triangleIndex, allowPushIntoTriangle, true, out np, out nti);
            switch (walkResult) {
               case WalkResult.Completion:
                  return np;
               case WalkResult.Progress:
                  distanceRemaining -= (p - np).Norm2D();
                  p = np;
                  triangleIndex = nti;
                  allowPushIntoTriangle = true;
                  continue;
               case WalkResult.PushInward:
                  distanceRemaining -= (p - np).Norm2D();
                  p = np;
                  triangleIndex = nti;
                  allowPushIntoTriangle = false;
                  break;
               case WalkResult.CanPushInward:
                  Console.WriteLine("Warning: Push inward didn't result in being in triangle?");
                  return np;
               case WalkResult.CanEdgeFollow:
                  throw new Exception("Impossible CanEdgeFollow state");
               default:
                  throw new Exception("Impossible state " + walkResult);
            }
         }
         return p;
      }

      private WalkResult WalkTriangle(
         DoubleVector2 position,
         DoubleVector2 preferredDirectionUnit,
         Double distanceRemaining,
         TriangulationIsland island,
         int triangleIndex,
         bool allowPushIntoTriangle,
         bool allowEdgeFollow,
         out DoubleVector2 nextPosition,
         out int nextTriangleIndex
         ) {
         Debug.Assert(GeometryOperations.IsReal(position));
         Debug.Assert(GeometryOperations.IsReal(preferredDirectionUnit));
         Debug.Assert(GeometryOperations.IsReal(distanceRemaining));

         // Make this a ref in C# 7.0 for minor perf gains
         ref var triangle = ref island.Triangles[triangleIndex];

         // Find the edge of our container triangle that we're walking towards 
         int opposingVertexIndex;
         if (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(position, preferredDirectionUnit, ref triangle, out opposingVertexIndex)) {
            // Resolve if we're not inside the triangle.
            if (!allowPushIntoTriangle) {
               Console.WriteLine("Warning: Pushed into triangle, but immediately not in triangle?");
               nextPosition = position;
               nextTriangleIndex = triangleIndex;
               return WalkResult.CanPushInward;
            }
            Console.WriteLine("Fix?");

            // If this fails, we're confused as to whether we're in the triangle or not, because we're on an
            // edge and floating point arithmetic error makes us confused. Simply push us slightly into the triangle
            // by pulling us towards its centroid
            // (A previous variant pulled based on perp of nearest edge, however the results are probably pretty similar)
            var offsetToCentroid = position.To(triangle.Centroid);
            if (offsetToCentroid.Norm2D() < InternalTerrainCompilationConstants.TriangleEdgeBufferRadius) {
               Console.WriteLine("Warning: Triangle width less than edge buffer radius!");
               nextPosition = triangle.Centroid;
               nextTriangleIndex = triangleIndex;
               return WalkResult.PushInward;
            } else {
               nextPosition = position + offsetToCentroid.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
               nextTriangleIndex = triangleIndex;
               return WalkResult.PushInward;
            }
         }

         // Let d = remaining "preferred" motion.
         var d = preferredDirectionUnit * distanceRemaining;

         // Project p-e0 onto perp(e0-e1) to find shortest vector from position to edge.
         // Intuitively an edge direction and the direction's perp form a vector
         // space. A point within the triangle's offset from a vertex (which has two edges)
         // is the sum of vector to point on nearest edge and vector from that point to the 
         // vertex. These vectors are orthogonal, so intuitively if we project onto the perp
         // we'll isolate the perp component.
         var e0 = triangle.Points[(opposingVertexIndex + 1) % 3];
         var e1 = triangle.Points[(opposingVertexIndex + 2) % 3];
         var e01 = e0.To(e1); // NOTE: triangle points are CCW.
         var e01Perp = new DoubleVector2(e01.Y, -e01.X); // points outside of current triangle, perp to edge we're crossing
         Trace.Assert(triangle.Centroid.To(e0).ProjectOntoComponentD(e01Perp) > CDoubleMath.c0);

         var pe0 = position.To(e0);
         var pToEdge = pe0.ProjectOnto(e01Perp); // perp to plane normal.

         // If we're sitting right on the edge, push us into the triangle before doing any work
         // Otherwise, it can be ambiguous as to what edge we're passing through on exit.
         // Don't delete this or we'll crash.
         if (pToEdge.Norm2D() < GeometryOperations.kEpsilon) {
            nextPosition = position - e01Perp.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            nextTriangleIndex = triangleIndex;
            return WalkResult.Progress; // is this the best result?
         }

         // Project d onto pToEdge to see if we're moving beyond edge boundary
         var pToEdgeComponentRemaining = d.ProjectOntoComponentD(pToEdge);
         Debug.Assert(GeometryOperations.IsReal(pToEdgeComponentRemaining));

         if (pToEdgeComponentRemaining < CDoubleMath.c1) {
            // Motion finishes within triangle.
            // TODO: Handle when this gets us very close to triangle edge e.g. cR = 0.99999.
            // (We don't want to fall close to the triangle edge but no longer in the triangle
            // due to floating point error)
            nextPosition = position + d;
            nextTriangleIndex = triangleIndex;
            return WalkResult.Completion;
         }

         // Proposed motion would finish outside the triangle
         var neighborTriangleIndex = triangle.NeighborOppositePointIndices[opposingVertexIndex];
         var dToEdge = d / pToEdgeComponentRemaining;
         Debug.Assert(GeometryOperations.IsReal(dToEdge));

         if (neighborTriangleIndex != Triangle3.NO_NEIGHBOR_INDEX) {
            // Move towards and past the edge between us and the other triangle.
            // There's a potential bug here where the other triangle is a sliver.
            // The edge buffer radius could potentially move us past TWO of its edges, out of it.
            // In practice, this bug happens OFTEN and is counteracted by the in-hole hack-fix.
            var dToAndPastEdge = dToEdge + dToEdge.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            nextPosition = position + dToAndPastEdge;
            nextTriangleIndex = neighborTriangleIndex;
            return WalkResult.Progress;
         } else {
            // We're running into an edge! First, place us as close to the edge as possible.
            var dToNearEdge = dToEdge - dToEdge.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            var pNearEdge = position + dToNearEdge;

            // We have this guard so if we're edge following, we don't start an inner loop that's also
            // edge following... which would probably lead to a stack overflow
            if (!allowEdgeFollow) {
               Console.WriteLine("Warning: Could edge follow, but was instructed not to?");
               nextPosition = pNearEdge;
               nextTriangleIndex = triangleIndex;
               return WalkResult.CanEdgeFollow;
            }

            // We want to follow the edge, potentially past it if possible.
            // Figure out which edge vertex we're walking towards
            var walkToEdgeVertex1 = d.ProjectOntoComponentD(e01) > CDoubleMath.c0;
            var vertexToWalkTowards = walkToEdgeVertex1 ? e1 : e0;
            var directionToWalkAlongEdge = walkToEdgeVertex1 ? e01 : CDoubleMath.cNeg1 * e01;
            var directionToWalkAlongEdgeUnit = directionToWalkAlongEdge.ToUnit();

            // start tracking p/drem independently.
            var p = pNearEdge;
            var ti = triangleIndex;
            var drem = dToNearEdge.Norm2D();
            var allowPushInward = true;
            while (drem > GeometryOperations.kEpsilon) {
               DoubleVector2 np;
               int nti;
               var wres = WalkTriangle(
                  pNearEdge,
                  directionToWalkAlongEdgeUnit,
                  distanceRemaining - dToNearEdge.Norm2D(),
                  island,
                  ti,
                  allowPushInward,
                  false,
                  out np,
                  out nti
                  );
               switch (wres) {
                  case WalkResult.Completion:
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Completion;
                  case WalkResult.CanEdgeFollow:
                     // This is an error, so we just finish
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Completion;
                  case WalkResult.Progress:
                     // Woohoo! Walking along edge brought us into another triangle
                     Trace.Assert(ti != nti);
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Progress;
                  case WalkResult.PushInward:
                     p = np; // HAHA
                     ti = nti;
                     allowPushInward = false;
                     continue;
                  case WalkResult.CanPushInward:
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Completion;
               }
            }

            nextPosition = p;
            nextTriangleIndex = ti;
            return WalkResult.Completion;
         }
      }
   }
}