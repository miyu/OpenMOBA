using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dargon.Commons;
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
         public cDouble distanceRemaining;
         public DoubleLineSegment2? haltingSegment;
      }

      public Logger logger = new Logger(0);
      private IDebugCanvas debugCanvas;
      private StrokeStyle debugStroke;

      private DoubleVector2 p, direction;
      private TriangulationIsland island;
      private int currentTriangleIndex;
      private double distanceRemaining;
      private int outEdgeOpposingVertexIndex;
      private List<(DoubleLineSegment2, Clockness)> haltSegments;
      private DoubleLineSegment2? haltingSegment;
      private bool nextIterationShouldMovePAlongDirectionToEdge;

      public void SetDebug(IDebugCanvas debugCanvas, StrokeStyle stroke) {
         this.debugCanvas = debugCanvas;
         this.debugStroke = stroke;
      }

      public WalkResult WalkTriangulation(Localization loc, DoubleVector2 dir, double initialDistance, List<(DoubleLineSegment2, Clockness)> haltSegments = null) {
         // initial state
         p = loc.LocalPosition;
         direction = dir;
         island = loc.TriangulationIsland;
         currentTriangleIndex = loc.TriangleIndex;
         distanceRemaining = initialDistance;
         outEdgeOpposingVertexIndex = FindOutEdgeIndex(ref p, direction, in island.Triangles[currentTriangleIndex], -1, logger);
         this.haltSegments = haltSegments;
         haltingSegment = (DoubleLineSegment2?)null;
         nextIterationShouldMovePAlongDirectionToEdge = true;

         logger.Debug?.WriteLine($"START TI {loc.TriangleIndex} OEOVI {outEdgeOpposingVertexIndex}");

         // prepare debug render state
         if (debugCanvas != null) {
            debugCanvas.Transform = loc.TerrainOverlayNetworkNode.SectorNodeDescription.WorldTransform;
            debugCanvas.DrawPoint(p, debugStroke);

            // for (var i = 0; i < loc.TriangulationIsland.Triangles.Length; i++) {
            //    var triangle = loc.TriangulationIsland.Triangles[i];
            //    debugCanvas.DrawText(i.ToString(), triangle.Centroid.LossyToIntVector2());
            // }
         }

         // exec
         return Execute();
      }

      WalkResult Execute() {
         while (true) {
            ref var currentTriangle = ref island.Triangles[currentTriangleIndex];

            logger.Debug?.WriteLine($"ITER {currentTriangleIndex} OEOVI {outEdgeOpposingVertexIndex}");

            // == step to edge of current triangle ==
            // Project p-e0 onto perp(e0-e1) to find shortest vector from position to edge.
            // Intuitively an edge direction and the direction's perp form a vector
            // space. A point within the triangle's offset from a vertex (which has two edges)
            // is the sum of vector to point on nearest edge and vector from that point to the 
            // vertex. These vectors are orthogonal, so intuitively if we project onto the perp
            // we'll isolate the perp component.
            var e0 = currentTriangle.Points[(outEdgeOpposingVertexIndex + 1) % 3];
            var e1 = currentTriangle.Points[(outEdgeOpposingVertexIndex + 2) % 3];
            // debugCanvas?.DrawLine(e0, e1, StrokeStyle.RedHairLineDashed5);
            var e0e1 = new DoubleLineSegment2(e0, e1);
            var e01 = e0.To(e1);

            var distanceToEdge = CDoubleMath.c0;
            var pAtEdge = p;

            if (nextIterationShouldMovePAlongDirectionToEdge) {
               var line = new DoubleLineSegment2(p, p + direction);
               GeometryOperations.TryFindNonoverlappingLineSegmentIntersectionT(in line, in e0e1, out var tForRay);
               if (tForRay <= -0.001f) {
                  logger.Warn?.WriteLine((tForRay < -0.1f ? "!!! " : "") + "L-S INT T<=-0.001f: " + tForRay + " " + line + " " + e0e1);
               }
               // var it = 0;
               // while(it < 10000) {
               //    if (GeometryOperations.TryFindNonoverlappingRaySegmentIntersectionT(in p, in direction, in e0e1, out tForRay)) {
               //       break;
               //    } else {
               //       if (it < 5) logger.Warn?.WriteLine("ITER " + p + " " + direction + " " + e0e1);
               //       // proj p onto segment.
               //       p -= direction * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
               //       it++;
               //
               //       // tForRay = 0;
               //       // break;
               //       // PullToCentroid(ref p, in currentTriangle);
               //       // if (it == 3) throw new InvalidStateException("Couldn't find r-s intersect t.");
               //    }
               // }
               // if (it != 0) logger.Warn?.WriteLine("FIX ITERS " + it);
               distanceToEdge = tForRay;

               // either can't reach edge or reached edge
               pAtEdge = p + direction * distanceToEdge;
               // debugCanvas?.DrawPoint(p, StrokeStyle.BlackThick3Solid);

               // see if we run out of steam
               if (distanceToEdge >= distanceRemaining) {
                  var pCompletion = p + direction * distanceRemaining;
                  if (haltSegments != null && HaltCheck(pCompletion, distanceRemaining)) break;
                  p = Advance("ToEdgeCompletion", p, pCompletion);
                  distanceRemaining = 0;
                  break;
               }

               // advance to edge
               if (haltSegments != null && HaltCheck(pAtEdge, distanceToEdge)) break;
               p = Advance("ToEdge", p, pAtEdge);
               distanceRemaining -= distanceToEdge;
            }


            // find next triangle to move into.
            var neighborTriangleIndex = currentTriangle.NeighborOppositePointIndices[outEdgeOpposingVertexIndex];

            if (neighborTriangleIndex != Triangle3.NO_NEIGHBOR_INDEX) {
               // move straight to neighbor
               ref var neighborTriangle = ref island.Triangles[neighborTriangleIndex];
               var sharedEdgeIndexInNeighborTriangle = currentTriangle.NeighborVertexIndexSharingEdgeOppositePointIndices[outEdgeOpposingVertexIndex];

               // prepare for next step
               // debugCanvas?.DrawLine(
               //    neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 1) % 3], 
               //    neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 2) % 3], 
               //    StrokeStyle.LimeHairLineSolid);
               logger.Debug?.WriteLine("Move into " + neighborTriangleIndex + " " + neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 1) % 3] + " AND " + neighborTriangle.Points[(sharedEdgeIndexInNeighborTriangle + 2) % 3]);
               // debugCanvas?.DrawPoint(p, StrokeStyle.MagentaThick3Solid);
               // debugCanvas?.DrawPoint(neighborTriangle.Centroid, StrokeStyle.OrangeThick3Solid);
               // if (neighborTriangleIndex == 8) {
                  // debugCanvas?.DrawText("0", neighborTriangle.Points[0].LossyToIntVector2());
                  // debugCanvas?.DrawText("1", neighborTriangle.Points[1].LossyToIntVector2());
                  // debugCanvas?.DrawText("2", neighborTriangle.Points[2].LossyToIntVector2());
                  // logger.Verbose?.WriteLine("SEI " + sharedEdgeIndexInNeighborTriangle);
               // }
               currentTriangleIndex = neighborTriangleIndex;
               // if (neighborTriangleIndex == 8) return default;
               outEdgeOpposingVertexIndex = FindOutEdgeIndex(ref p, direction, in neighborTriangle, sharedEdgeIndexInNeighborTriangle, logger);
               nextIterationShouldMovePAlongDirectionToEdge = true;
            } else {
               // == Follow the edge, potentially past it across a corner (multiple edges!) or into another followed edge ==
               // Figure out which edge vertex we're walking towards
               var walkToEdgeVertex1 = direction.Dot(e01) > CDoubleMath.c0;
               var corner = walkToEdgeVertex1 ? e1 : e0;
               var pToCorner = p.To(corner);
               var pToCornerMag = pToCorner.Norm2D();
               var pToCornerDirection = pToCorner / pToCornerMag;

               // debugCanvas?.DrawPoint(corner, StrokeStyle.LimeThick5Solid);

               if (pToCornerMag >= distanceRemaining) {
                  var pTowardCorner = p + pToCornerDirection * distanceRemaining;
                  if (haltSegments != null && HaltCheck(pTowardCorner, distanceRemaining)) break;
                  p = Advance("ToCornerCompletion", p, pTowardCorner);
                  distanceRemaining = 0;
                  break;
               }

               // advance to corner
               if (haltSegments != null && HaltCheck(corner, distanceToEdge)) break;
               p = Advance("ToCorner", p, corner);
               distanceRemaining -= pToCornerMag;

               // round corner to determine next triangle.
               var cornerIndex = walkToEdgeVertex1 ? (outEdgeOpposingVertexIndex + 2) % 3 : (outEdgeOpposingVertexIndex + 1) % 3;
               var edgeToExit = walkToEdgeVertex1 ? (outEdgeOpposingVertexIndex + 1) % 3 : (outEdgeOpposingVertexIndex + 2) % 3;
               var wrapDirection = walkToEdgeVertex1 ? Clockness.Clockwise : Clockness.CounterClockwise;
               logger.Debug?.WriteLine("Wrapping " + currentTriangleIndex + " " + cornerIndex + " " + edgeToExit + " " + wrapDirection);

               var res = FindExitTriangle(island, currentTriangleIndex, cornerIndex, edgeToExit, direction, wrapDirection, debugCanvas);
               logger.Debug?.WriteLine("Wrap to " + res + " " + wrapDirection);

               if (res.ok) {
                  currentTriangleIndex = res.triangleIndex;
                  outEdgeOpposingVertexIndex = res.cornerIndex;
                  nextIterationShouldMovePAlongDirectionToEdge = true;
                  // debugCanvas?.DrawPoint(island.Triangles[res.triangleIndex].Points[res.cornerIndex], StrokeStyle.CyanThick10Solid);
                  // debugCanvas?.DrawPoint(island.Triangles[res.triangleIndex].Centroid, StrokeStyle.CyanThick10Solid);
               } else { 
                  // Failed to wrap corner & exit at desired direction.
                  // We're either hitting a dead-end corner or need to follow edge to the next wrap.
                  var wrapEndTi = res.triangleIndex;
                  var wrapEndCi = res.cornerIndex;
                  var otherVertOfFollowedEdge = (wrapEndCi - (int)wrapDirection + 3) % 3;
                  var vertToSlideToward = island.Triangles[wrapEndTi].Points[otherVertOfFollowedEdge];
                  // debugCanvas?.DrawPoint(vertToSlideToward, StrokeStyle.CyanThick10Solid);
                  // debugCanvas?.DrawPoint(corner, StrokeStyle.OrangeThick10Solid);
                  if (corner.To(vertToSlideToward).Dot(direction) > 0) {
                     var edgeToWalkIndex = (wrapEndCi + (int)wrapDirection + 3) % 3;
                     logger.Debug?.WriteLine("Can slide " + wrapEndTi + " " + wrapEndCi + " " + edgeToWalkIndex);
                     currentTriangleIndex = wrapEndTi;
                     outEdgeOpposingVertexIndex = edgeToWalkIndex;
                     nextIterationShouldMovePAlongDirectionToEdge = false;
                     // debugCanvas?.DrawPoint(island.Triangles[wrapEndTi].Points[wrapEndCi], StrokeStyle.BlackThick25Solid);
                     // debugCanvas?.DrawPoint(island.Triangles[wrapEndTi].Points[edgeToWalkIndex], StrokeStyle.OrangeThick35Solid);
                  } else {
                     logger.Debug?.WriteLine("That's a snag! Can't slide.");
                     break;
                  }
               }
            }
         }

         return new WalkResult {
            loc = new Localization2D {
               p = p,
               triangleIndex = currentTriangleIndex,
               triangulationIsland = island
            },
            distanceRemaining = distanceRemaining,
            haltingSegment = haltingSegment
         };
      }

      private bool HaltCheck(DoubleVector2 next, double distanceToEdge) {
         if (p == next) return false;

         var direction = p.To(next).ToUnit();
         var haltRes = TestHaltSegments(p, direction, haltSegments);
         if (haltRes.hit && haltRes.distance <= distanceToEdge) {
            var pAtHaltEdge = p + direction * haltRes.distance;
            p = Advance("HALT", p, pAtHaltEdge);
            distanceRemaining -= haltRes.distance;
            haltingSegment = haltRes.seg;
            // debugCanvas?.DrawPoint(pAtHaltEdge, StrokeStyle.MagentaThick10Solid);
            return true;
         }
         return false;
      }

      private DoubleVector2 Advance(string op, DoubleVector2 @from, DoubleVector2 to) {
         logger.Debug?.WriteLine($"[{op}] Advance {@from} => {to}");
         debugCanvas?.DrawLine(@from, to, debugStroke);
         return to;
      }

      private (bool hit, DoubleLineSegment2 seg, double distance) TestHaltSegments(DoubleVector2 p, DoubleVector2 direction, List<(DoubleLineSegment2, Clockness)> haltSegments) {
         var nearest = (hit: false, seg: default(DoubleLineSegment2), distance: double.PositiveInfinity);
         foreach (var (s, clockReq) in haltSegments) {
            var seg = s;
            var clock = (Clockness)(-(int)GeometryOperations.Clockness(seg.First, seg.Second, p));
            logger.Debug?.WriteLine($"TEST {s}/{clockReq} vs {p} ({direction}/{clock})");
            if (clock == clockReq && GeometryOperations.TryFindNonoverlappingRaySegmentIntersectionT(in p, in direction, in seg, out var tForRay) &&
                nearest.distance > tForRay) {
               nearest = (true, s, tForRay);
            }
         }
         return nearest;
      }

      private static int FindOutEdgeIndex(ref DoubleVector2 p, DoubleVector2 direction, in Triangle3 triangle, int skippedEdge = -1, Logger logger = null) {
         int outEdgeOpposingVertexIndex;
         var it = 0;
         var offset = DoubleVector2.Zero;
         while (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(p + offset, direction, in triangle, out outEdgeOpposingVertexIndex, skippedEdge)) {
            // fix - pull to centroid
            //
            offset -= direction * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            it++;
            if (it % 5 == 4) {
               PullToCentroid(ref p, in triangle);
            }
            // if (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(p, direction, in triangle, out outEdgeOpposingVertexIndex, skippedEdge)) {
            //    throw new InvalidStateException($"Unable to localize p={p} in {triangle}");
            // }
         }
         if (it != 0) logger?.Warn?.WriteLine("FIX OEI ITERS " + it);
         return outEdgeOpposingVertexIndex;
      }

      private static void PullToCentroid(ref DoubleVector2 p, in Triangle3 triangle) {
         // If this fails, we're confused as to whether we're in the triangle or not, because we're on an
         // edge and floating point arithmetic error makes us confused. Simply push us slightly into the triangle
         // by pulling us towards its centroid
         // (A previous variant pulled based on perp of nearest edge, however the results are probably pretty similar)
         var offsetToCentroid = p.To(triangle.Centroid);
         if (offsetToCentroid.Norm2D() < InternalTerrainCompilationConstants.TriangleEdgeBufferRadius) {
            Console.WriteLine("Warning: Triangle width less than edge buffer radius!");
            p = triangle.Centroid;
         } else {
            p += offsetToCentroid.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
         }
      }

      private (bool ok, int triangleIndex, int cornerIndex) FindExitTriangle(TriangulationIsland island, int initialTriangleIndex, int initialCornerIndex, int initialEdgeToExit, DoubleVector2 direction, Clockness wrapDirection, IDebugCanvas debugCanvas = null) {
         var currentTriangleIndex = initialTriangleIndex;
         var currentCornerIndex = initialCornerIndex;
         var corner = island.Triangles[currentTriangleIndex].Points[currentCornerIndex];
         var edgeToExit = initialEdgeToExit;

         while (true) {
            ref var currentTriangle = ref island.Triangles[currentTriangleIndex];
            var neighborIndex = currentTriangle.NeighborOppositePointIndices[edgeToExit];
            var neighborInEdge = currentTriangle.NeighborVertexIndexSharingEdgeOppositePointIndices[edgeToExit]; // index of point opposing in-edge
            logger.Verbose?.WriteLine("AT " + currentTriangleIndex + " " + currentCornerIndex + " NI " + neighborIndex);

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

            // if (currentTriangleIndex == 38) {
            //    debugCanvas?.DrawPoint(neighborOutEdgePoint, StrokeStyle.RedThick25Solid);
            //    debugCanvas?.DrawPoint(neighborInEdgePoint, StrokeStyle.LimeThick25Solid);
            // }

            var directionCrossOutEdgeRay = GeometryOperations.Clockness(direction, corner.To(neighborOutEdgePoint));
            var directionCrossInEdgeRay = GeometryOperations.Clockness(direction, corner.To(neighborInEdgePoint));
            logger.Verbose?.WriteLine("CMP " + directionCrossOutEdgeRay + " " + wrapDirection + " " + directionCrossInEdgeRay);
            logger.Verbose?.WriteLine("DMP " + corner + " " + neighborOutEdgePoint + " " + neighborInEdgePoint + " " + direction);

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

   public class Logger {
      public ILog Error { get; }
      public ILog Warn { get; }
      public ILog Info { get; }
      public ILog Debug { get; }
      public ILog Verbose { get; }

      public Logger(int lv, ILog log = null) {
         log = log ?? new SystemConsoleLog();
         Error = lv >= 0 ? log : null;
         Warn = lv >= 0 ? log : null;
         Info = lv >= 1 ? log : null;
         Debug = lv >= 2 ? log : null;
         Verbose = lv >= 3 ? log : null;
      }

      public class SystemConsoleLog : ILog {
         public void WriteLine(string s) => Console.WriteLine(s);
      }
   }

   public interface ILog {
      void WriteLine(string s); 
   }

   public class TriangulationWalker3D {
      private readonly TriangulationWalker2D triangulationWalker2D;
      private readonly Logger logger = new Logger(0);

      public TriangulationWalker3D(TriangulationWalker2D triangulationWalker2D) {
         this.triangulationWalker2D = triangulationWalker2D;
      }

      public (Localization, double) WalkTriangulation(Localization initialLoc, DoubleVector3 initialDirection, double initialDistance, IDebugCanvas debugCanvas = null, StrokeStyle stroke = null) {
         var currentLoc = initialLoc;
         var currentDirection = initialDirection;
         var distanceRemaining = initialDistance;
         while (distanceRemaining > 0) {
            var currentTonn = currentLoc.TerrainOverlayNetworkNode;
            var currentSnd = currentTonn.SectorNodeDescription;
            var localDirection = currentSnd.WorldToLocalNormal(initialDirection).XY.ToUnit();
            var localDistanceRemaining = distanceRemaining * currentSnd.WorldToLocalScalingFactor;
            triangulationWalker2D.SetDebug(debugCanvas, stroke ?? StrokeStyle.RedThick3Solid);
            var walkResult = triangulationWalker2D.WalkTriangulation(currentLoc, localDirection, localDistanceRemaining, currentTonn.OutboundEdgeSegments);
            var localDistanceWalked = localDistanceRemaining - walkResult.distanceRemaining;
            var worldDistanceWalked = localDistanceWalked * currentSnd.LocalToWorldScalingFactor;

            currentLoc.LocalPosition = walkResult.loc.p;
            currentLoc.LocalPositionIv2 = walkResult.loc.p.LossyToIntVector2();
            currentLoc.TriangleIndex = walkResult.loc.triangleIndex;

            distanceRemaining -= worldDistanceWalked;

            if (walkResult.haltingSegment == null) {
               if (walkResult.distanceRemaining > 0) {
                  logger.Debug?.WriteLine("Exit because snag");
                  break; // snagged a corner
               } else {
                  distanceRemaining = 0;
                  break;
               }
            } else {
               // We're crossing a halting segment of a crossover edge
               var haltingSegment = walkResult.haltingSegment.Value;
               var outboundEdgeGroup = currentTonn.OutboundEdgeGroupsBySegment[haltingSegment];
               var nextTonn = outboundEdgeGroup.Destination;
               var haltingSegmentT = haltingSegment.First.To(currentLoc.LocalPosition).ProjectOntoComponentD(haltingSegment.First.To(haltingSegment.Second));
               Assert.IsTrue(CDoubleMath.c0 <= haltingSegmentT && haltingSegmentT <= CDoubleMath.c1);

               // compute location on the other side
               var newLocalPosition = outboundEdgeGroup.EdgeJob.DestinationSegment.PointAt(haltingSegmentT);
               if (!nextTonn.LocalGeometryView.Triangulation.TryIntersect(newLocalPosition.X, newLocalPosition.Y, out var island, out var triangleIndex)) {
                  logger.Warn?.WriteLine("[gd] Fail to localize");
                  throw new InvalidStateException("Failed to localize");
               }
               currentLoc.TerrainOverlayNetworkNode = nextTonn;
               currentLoc.LocalPosition = newLocalPosition;
               currentLoc.LocalPositionIv2 = newLocalPosition.LossyToIntVector2();
               currentLoc.TriangulationIsland = island;
               currentLoc.TriangleIndex = triangleIndex;
            }
         }
         return (currentLoc, distanceRemaining);
      }
   }
}