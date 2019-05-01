using System;
using System.Diagnostics;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.ECS {
   public class TriangulationWalker2D {
      public struct Localization2D {
         public DoubleVector2 p;
         public TriangulationIsland triangulationIsland;
         public int triangleIndex;
      }

      public struct WalkResult {
         public Localization2D loc;
      }


      public WalkResult WalkTriangulation(Localization loc, DoubleVector2 direction, cDouble initialDistance, IDebugCanvas debugCanvas = null) {
         var p = loc.LocalPosition;
         var island = loc.TriangulationIsland;
         var currentTriangleIndex = loc.TriangleIndex;
         var distanceRemaining = initialDistance;
         var outEdgeOpposingVertexIndex = FindOutEdgeIndex(p, direction, in island.Triangles[currentTriangleIndex]);

         if (debugCanvas != null) debugCanvas.Transform = loc.TerrainOverlayNetworkNode.SectorNodeDescription.WorldTransform;
         debugCanvas?.DrawPoint(p, StrokeStyle.RedThick3Solid);
         for (var i = 0; i < loc.TriangulationIsland.Triangles.Length; i++) {
            var triangle = loc.TriangulationIsland.Triangles[i];
            debugCanvas.DrawText(i.ToString(), triangle.Centroid.LossyToIntVector2());
         }

         DoubleVector2 Advance(DoubleVector2 from, DoubleVector2 to, IDebugCanvas dc) {
            dc?.DrawLine(from, to, StrokeStyle.RedThick3Solid);
            return to;
         }

         while (true) {
            ref var currentTriangle = ref island.Triangles[currentTriangleIndex];

            // == step to edge of current triangle ==
            // Project p-e0 onto perp(e0-e1) to find shortest vector from position to edge.
            // Intuitively an edge direction and the direction's perp form a vector
            // space. A point within the triangle's offset from a vertex (which has two edges)
            // is the sum of vector to point on nearest edge and vector from that point to the 
            // vertex. These vectors are orthogonal, so intuitively if we project onto the perp
            // we'll isolate the perp component.
            var e0 = currentTriangle.Points[(outEdgeOpposingVertexIndex + 1) % 3];
            var e1 = currentTriangle.Points[(outEdgeOpposingVertexIndex + 2) % 3];
            debugCanvas.DrawLine(e0, e1, StrokeStyle.RedHairLineDashed5);
            var e0e1 = new DoubleLineSegment2(e0, e1);
            var e01 = e0.To(e1);
            if (!GeometryOperations.TryFindNonoverlappingRaySegmentIntersectionT(ref p, ref direction, ref e0e1, out var tForRay)) {
               throw new InvalidStateException();
            }
            var distanceToEdge = tForRay;

            // either can't reach edge or reached edge
            var pAtEdge = p + direction * distanceToEdge;
            debugCanvas.DrawPoint(p, StrokeStyle.BlackThick3Solid);
            if (distanceToEdge >= distanceRemaining) {
               p = Advance(p, p + direction * distanceRemaining, debugCanvas);
               break;
            }

            // advance to edge
            p = Advance(p, pAtEdge, debugCanvas);
            distanceRemaining -= distanceToEdge;

            // find next triangle to move into.
            var neighborTriangleIndex = currentTriangle.NeighborOppositePointIndices[outEdgeOpposingVertexIndex];

            // handle no neighbor
            if (neighborTriangleIndex != Triangle3.NO_NEIGHBOR_INDEX) {
               // move straight to neighbor
               ref var neighborTriangle = ref island.Triangles[neighborTriangleIndex];
               var sharedEdgeIndexInNeighborTriangle = currentTriangle.NeighborVertexIndexSharingEdgeOppositePointIndices[outEdgeOpposingVertexIndex];

               // prepare for next step
               debugCanvas.DrawLine(
                  neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 1) % 3], 
                  neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 2) % 3], 
                  StrokeStyle.LimeHairLineSolid);
               Console.WriteLine("Move into " + neighborTriangleIndex + " " + neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 1) % 3] + " AND " + neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 2) % 3]);
               debugCanvas.DrawPoint(p, StrokeStyle.MagentaThick3Solid);
               debugCanvas.DrawPoint(neighborTriangle.Centroid, StrokeStyle.OrangeThick3Solid);
               if (neighborTriangleIndex == 8) {
                  debugCanvas.DrawText("0", neighborTriangle.Points[0].LossyToIntVector2());
                  debugCanvas.DrawText("1", neighborTriangle.Points[1].LossyToIntVector2());
                  debugCanvas.DrawText("2", neighborTriangle.Points[2].LossyToIntVector2());
                  Console.WriteLine("SEI " + sharedEdgeIndexInNeighborTriangle);
               }
               currentTriangleIndex = neighborTriangleIndex;
               // if (neighborTriangleIndex == 8) return default;
               outEdgeOpposingVertexIndex = FindOutEdgeIndex(p, direction, in neighborTriangle, sharedEdgeIndexInNeighborTriangle);
            } else {
               // == Follow the edge, potentially past it across a corner (multiple edges!) or into another followed edge ==
               // Figure out which edge vertex we're walking towards
               var walkToEdgeVertex1 = direction.Dot(e01) > CDoubleMath.c0;
               var corner = walkToEdgeVertex1 ? e1 : e0;
               var pToCorner = p.To(corner);
               var pToCornerMag = pToCorner.Norm2D();
               var pToCornerDirection = pToCorner / pToCornerMag;

               debugCanvas.DrawPoint(corner, StrokeStyle.LimeThick5Solid);

               if (pToCornerMag >= distanceRemaining) {
                  p = Advance(p, p + pToCornerDirection * distanceRemaining, debugCanvas);
                  break;
               }
               
               // advance to corner
               p = Advance(p, corner, debugCanvas);
               distanceRemaining -= pToCornerMag;

               // round corner to determine next triangle.
               var cornerIndex = walkToEdgeVertex1 ? (outEdgeOpposingVertexIndex + 2) % 3 : (outEdgeOpposingVertexIndex + 1) % 3;
               var edgeToExit = walkToEdgeVertex1 ? (outEdgeOpposingVertexIndex + 1) % 3 : (outEdgeOpposingVertexIndex + 2) % 3;
               var wrapDirection = walkToEdgeVertex1 ? Clockness.Clockwise : Clockness.CounterClockwise;
               Console.WriteLine("Wrapping " + currentTriangleIndex + " " + cornerIndex + " " + edgeToExit);
               if (currentTriangleIndex == 11) {
                  Console.WriteLine("---");
                  debugCanvas.DrawPoint(corner, StrokeStyle.LimeThick25Solid);
                  debugCanvas.DrawPoint(currentTriangle.Points[edgeToExit], StrokeStyle.MagentaThick25Solid);
                  Console.WriteLine("=== " + corner);
                  //return default;
               }
               var res = FindExitTriangle(island, currentTriangleIndex, cornerIndex, edgeToExit, direction, wrapDirection, debugCanvas);
               debugCanvas.DrawPoint(island.Triangles[res.Item2].Centroid, StrokeStyle.OrangeThick35Solid);
               Console.WriteLine("Wrap to " + res + " " + wrapDirection);
               bool ok;
               (ok, currentTriangleIndex, outEdgeOpposingVertexIndex) = res;
               if (!ok) {
                  // caught in a corner!
                  debugCanvas.DrawPoint(corner, StrokeStyle.OrangeThick10Solid);
                  break;
               }
            }
         }

         return new WalkResult {
            loc = new Localization2D {
               p = p,
               triangleIndex = currentTriangleIndex,
               triangulationIsland = island
            }
         };
      }

      private static int FindOutEdgeIndex(DoubleVector2 p, DoubleVector2 direction, in Triangle3 triangle, int skippedEdge = -1) {
         int outEdgeOpposingVertexIndex;
         if (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(p, direction, in triangle, out outEdgeOpposingVertexIndex, skippedEdge)) {
            // fix - pull to centroid
            throw new NotImplementedException();
         }
         return outEdgeOpposingVertexIndex;
      }
      
      private static (bool, int, int) FindExitTriangle(TriangulationIsland island, int initialTriangleIndex, int initialCornerIndex, int initialEdgeToExit, DoubleVector2 direction, Clockness wrapDirection, IDebugCanvas debugCanvas = null) {
         var currentTriangleIndex = initialTriangleIndex;
         var currentCornerIndex = initialCornerIndex;
         var corner = island.Triangles[currentTriangleIndex].Points[currentCornerIndex];
         var edgeToExit = initialEdgeToExit;

         while (true) {
            Console.WriteLine("AT " + currentTriangleIndex + " " + currentCornerIndex);
            ref var currentTriangle = ref island.Triangles[currentTriangleIndex];
            var neighborIndex = currentTriangle.NeighborOppositePointIndices[edgeToExit];
            var neighborInEdge = currentTriangle.NeighborVertexIndexSharingEdgeOppositePointIndices[edgeToExit]; // index of point opposing in-edge

            if (neighborIndex == -1) {
               return (false, currentTriangleIndex, currentCornerIndex);
            }
            
            // find neighbor corner point index.
            var currentTriangleCornerOffset = currentCornerIndex - edgeToExit; // offset from edgeToExit to corner index (-1 or 1 mod 3)
            var neighborCornerIndex = ((neighborInEdge - currentTriangleCornerOffset) + 3) % 3; // corner index

            // 2 cases: ray goes into neighbor triangle or wrap further along corner.
            ref var neighborTriangle = ref island.Triangles[neighborIndex];
            var neighborOutEdge = (neighborInEdge + currentTriangleCornerOffset + 3) % 3; // point of edge we might wrap past.

            // Yes this seems flipped but it's correct. Note dual between edge index & point index.
            var neighborInEdgePoint = neighborTriangle.Points[neighborOutEdge]; 
            var neighborOutEdgePoint = neighborTriangle.Points[neighborInEdge]; 

            debugCanvas.DrawPoint(neighborOutEdgePoint, StrokeStyle.RedThick25Solid);
            debugCanvas.DrawPoint(neighborInEdgePoint, StrokeStyle.LimeThick25Solid);

            var directionCrossOutEdgeRay = GeometryOperations.Clockness(direction, corner.To(neighborOutEdgePoint));
            var directionCrossInEdgeRay = GeometryOperations.Clockness(direction, corner.To(neighborInEdgePoint));
            Console.WriteLine("CMP " + directionCrossOutEdgeRay + " " + wrapDirection + " " + directionCrossInEdgeRay);
            Console.WriteLine("DMP " + corner + " " + neighborOutEdgePoint + " " + neighborInEdgePoint + " " + direction);

            if ((int)directionCrossOutEdgeRay != (int)wrapDirection || (int)directionCrossInEdgeRay != -(int)wrapDirection) {
               currentTriangleIndex = neighborIndex;
               currentCornerIndex = neighborCornerIndex;
               edgeToExit = neighborOutEdge;
            } else {
               return (true, neighborIndex, neighborCornerIndex);
            }
         }
      }
   }
}