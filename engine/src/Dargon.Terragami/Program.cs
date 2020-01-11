using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Commons.Pooling;
using Dargon.Dviz;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Dviz;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.ThirdParty.ClipperLib;
using Dargon.Terragami.Dviz;
using cInt = System.Int32;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.Terragami {
   /// <summary>
   /// T(R(S(input)))
   /// </summary>
   public class CoreTransform {
      public Matrix4x4 Matrix;
      public Matrix4x4 InverseMatrix;
      public cDouble Scale;

      public CoreTransform(Matrix4x4 matrix, Matrix4x4 inverseMatrix, cDouble scale) {
         Matrix = matrix;
         InverseMatrix = inverseMatrix;
         Scale = scale;
      }

      public LocalTransform Flatten() {
         var mat = new Matrix3x2(
            Matrix.M11, Matrix.M12,
            Matrix.M21, Matrix.M22,
            Matrix.M41, Matrix.M42);
         var matInv = new Matrix3x2(
            InverseMatrix.M11, InverseMatrix.M12,
            InverseMatrix.M21, InverseMatrix.M22,
            InverseMatrix.M41, InverseMatrix.M42);
         return new LocalTransform(
            mat,
            matInv,
            Scale);
      }

      public static CoreTransform CreateTRS(DoubleVector3 position, Quaternion rotation, cDouble scale) {
         var rot = Matrix4x4.CreateFromQuaternion(rotation);
         var rotInv = Matrix4x4.Transpose(rot);

         Matrix4x4 tra = Matrix4x4.CreateTranslation((float)position.X, (float)position.Y, (float)position.Z);
         Matrix4x4 traInv = Matrix4x4.CreateTranslation(-(float)position.X, -(float)position.Y, -(float)position.Z);

         // p*S*R*T
         var mat = Matrix4x4.Multiply(Matrix4x4.Multiply(Matrix4x4.CreateScale((float)scale), rot), tra);

         // (p*S*R*T) * (Tinv * RInv * SInv)
         var matInv = traInv * rotInv * Matrix4x4.CreateScale(1.0f / (float)scale);
         return new CoreTransform(mat, matInv, scale);
      }

      public static CoreTransform LocalToLocal(CoreTransform src, CoreTransform dest) {
         return new CoreTransform(
            src.Matrix * dest.InverseMatrix, // p * MSrc * MInvDest
            dest.Matrix * src.InverseMatrix, // (p * Msrc * MInvDest) * MDest * MInvSrc
            src.Scale / dest.Scale
            );
      }
   }

   public class LocalTransform {
      public Matrix3x2 Matrix;
      public Matrix3x2 InverseMatrix;
      public cDouble Scale;

      public LocalTransform(Matrix3x2 mat, Matrix3x2 matInv, cDouble scale) {
         Matrix = mat;
         InverseMatrix = matInv;
         Scale = scale;
      }
   }

   public class Portal {
      public int CrossoverPointsGenerated;
      public IntLineSegment2 Segment;
      public Clockness InClockness;
   }

   public class GeometryInput {
      public SectorBlueprint Blueprint;
      public CoreTransform Transform;
   }

   public class HoleProjection {
      public PolygonNode Root;
      public CoreTransform Transform;
   }

   public interface ICoreHolePrimitive {
      AxisAlignedBoundingBox3 ComputeWorldAABB(CoreTransform holeTransform);
      HoleProjection Project(CoreTransform holeTransform, CoreTransform sectorTransform);
      bool TryFastPointDistance(CoreTransform holeTransform, DoubleVector3 pointWorld, out cDouble distance);
   }

   public class SphereHolePrimitive : ICoreHolePrimitive {
      private const int NumPoints = 16;

      public SphereHolePrimitive(float radius) {
         Radius = radius;
      }

      public float Radius { get; }

      public HoleProjection Project(CoreTransform holeTransform, CoreTransform sectorTransform) {
         var radius = CDoubleMath.Ceiling(Radius * holeTransform.Scale / sectorTransform.Scale);

         var n = NumPoints;
         var points = new IntVector2[n];
         var mul = CDoubleMath.c2 * CDoubleMath.Pi / (cDouble)n;
         for (var i = 0; i < n; i++) {
            var theta = - i * mul;
            points[i] = new IntVector2(
                (cInt)(radius * CDoubleMath.Sin(theta)),
               (cInt)(radius * CDoubleMath.Cos(theta))
               );
         }

         var root = PolygonNode.CreateRootHole(
            PolygonNode.Create(points, false)
            );

         return new HoleProjection {
            Root = root,
            Transform = CoreTransform.LocalToLocal(holeTransform, sectorTransform)
         };
      }

      public bool TryFastPointDistance(CoreTransform holeTransform, DoubleVector3 pointWorld, out double distance) {
         var holeOrigin = Vector3.Transform(Vector3.Zero, holeTransform.Matrix);
         distance = Vector3.Distance(pointWorld.ToDotNetVector(), holeOrigin);
         return true;
      }

      public AxisAlignedBoundingBox3 ComputeWorldAABB(CoreTransform holeTransform) {
         var holeOrigin = Vector3.Transform(Vector3.Zero, holeTransform.Matrix).ToOpenMobaVector();
         return AxisAlignedBoundingBox3.FromExtents(
            holeOrigin - DoubleVector3.One * Radius,
            holeOrigin + DoubleVector3.One * Radius
            );
      }
   }

   public class HoleInput {
      public ICoreHolePrimitive HolePrimitive;
      public CoreTransform Transform;
   }

   public class CompilationInput {
      public GeometryInput Land;
      public HashSet<IntVector2> TraversableCorners;
      public HashSet<IntVector2> PinPoints;
      public ExposedArrayList<Portal> Portals;
      public ExposedArrayList<HoleInput> Holes;
   }

   public class Program {
      public static void Main(string[] args) {
         var blueprint = SectorBlueprints.FourSquares2D;
         var input = new CompilationInput {
            Land = new GeometryInput {
               Blueprint = blueprint,
               Transform = new CoreTransform(Matrix4x4.Identity, Matrix4x4.Identity, 2.5f / blueprint.LocalBoundary.Width),
            },
            TraversableCorners = new HashSet<IntVector2> { }
               .Concat(SectorBlueprints.FourSquares2D.Root.Children[0].Children[0].Contour)
               .Concat(SectorBlueprints.FourSquares2D.Root.Children[0].Children[1].Contour)
               .ToHashSet(),
            PinPoints = new HashSet<IntVector2> {
               blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 2, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 4, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 2, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 4, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 0, 1),
               blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 0, 1),
               blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 1, 1),
               blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 1, 1),
            },
            Portals = new ExposedArrayList<Portal> {
               new Portal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 2, 6),
                     blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 4, 6)),
                  CrossoverPointsGenerated = 10,
               },
               new Portal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 2, 6),
                     blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 4, 6)),
                  CrossoverPointsGenerated = 10,
               },
               new Portal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 0, 1),
                     blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 0, 1)),
                  CrossoverPointsGenerated = 10,
               },
               new Portal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 1, 1),
                     blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 1, 1)),
                  CrossoverPointsGenerated = 10,
               },
            },
            Holes = new ExposedArrayList<HoleInput> {
               // new HoleInput {
               //    HolePrimitive = new SphereHolePrimitive(0.5f),
               //    Transform = new CoreTransform(Matrix4x4.Identity, Matrix4x4.Identity, 1),
               // }
            },
         };

         var debugMultiCanvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(
            new Size(800, 600), new Point(200, 200), 
            new OrthographicXYProjector(0.02f, default, new Vector2(400, 300), true));
         var sw = new Stopwatch();
         var totalIters = 0;
         var ntrialiters = 1;
         while (true) {
            var niters = 1000;
            for (var i = 0; i < niters; i++, totalIters++) {
               var canvas = totalIters < ntrialiters ? debugMultiCanvasHost.CreateAndAddCanvas(i) : null;
               Eval(input, canvas);

               if (i + 1 == ntrialiters) {
                  sw.Restart();
               }
            }
            var ms = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine(niters + "iters " + ms + " => " + ms / niters + " ms");
         }
      }

      private static void Eval(CompilationInput input, IDebugCanvas debugCanvasOpt) {
         var punch = PolygonOperations.Punch();
         punch.Include(input.Land.Blueprint.Root);
         foreach (var hole in input.Holes) {
            var holeProjection = hole.HolePrimitive.Project(hole.Transform, input.Land.Transform);
            var holeTransform = holeProjection.Transform.Flatten();
            var mat = holeTransform.Matrix;
            
            var s = new Stack<PolygonNode>();
            var transformedContour = new List<IntVector2>();
            s.Push(holeProjection.Root);
            while (s.Count > 0) {
               var n = s.Pop();
               foreach (var succ in n.Children) {
                  s.Push(succ);
               }
               if (n.Contour == null) continue;

               transformedContour.Clear();
               foreach (var p in n.Contour) {
                  var q = Vector2.Transform(p.ToDotNetVector(), mat).ToOpenMobaVector().LossyToIntVector2();
                  transformedContour.Add(q);
               }
               punch.Exclude(transformedContour);
            }
         }

         var punchedLand = punch.Execute();
         if (debugCanvasOpt != null) {
            debugCanvasOpt.Transform = Matrix4x4.Identity;
            debugCanvasOpt.DrawPolygonNode(punchedLand);
            debugCanvasOpt.DrawPoints(input.PinPoints.ToArray(), StrokeStyle.RedThick25Solid);
            debugCanvasOpt.DrawPoints(input.TraversableCorners.ToArray(), StrokeStyle.LimeThick25Solid);
         }

         var portals = input.Portals;
         var portalPoints = new IntVector2[portals.Count][];
         for (var i = 0; i < portals.Count; i++) {
            var pi = portals[i];
            var points = portalPoints[i] = new IntVector2[pi.CrossoverPointsGenerated];
            for (var j = 0; j < points.Length; j++) {
               points[j] = pi.Segment.PointAtRatioLossy(j, points.Length - 1);
            }
         }

         var segs = new BarrierCalculator().CalculateContourAndChildHoleBarriers(punchedLand);
         var bvh = BvhILS2.Build(segs);
         foreach (var seg in segs) {
            // Console.WriteLine($"{seg.First.X},{seg.First.Y},{seg.Second.X},{seg.Second.Y}");
         }

         if (debugCanvasOpt != null) {
            debugCanvasOpt.DrawLineList(
               input.Portals.SelectMany(p => new[] { p.Segment.First, p.Segment.Second }).ToArray(),
               StrokeStyle.LimeThick5Solid);
            debugCanvasOpt.DrawLineList(segs, StrokeStyle.GrayHairLineSolid);
         }

         var pass = 0;
         var queries = new List<IntLineSegment2>();
         for (var a = 0; a < portals.Count; a++) {
            var portalA = portals[a];
            var pointsA = portalPoints[a];

            for (var b = a + 1; b < portals.Count; b++) {
               var portalB = portals[b];
               var pointsB = portalPoints[b];

               var linkStates = new LinkState[pointsA.Length * pointsB.Length];
               var linkStateIndex = 0;
               for (var i = 0; i < pointsA.Length; i++) {
                  var pa = pointsA[i];
                  for (var j = 0; j < pointsB.Length; j++) {
                     var pb = pointsB[j];
                     var occluded = pa == pb || bvh.Intersects(new IntLineSegment2(pa, pb), false);
                     linkStates[linkStateIndex] = new LinkState { 
                        Occluded = occluded,
                     };
                     if (!occluded) pass++;
                     queries.Add(new IntLineSegment2(pa, pb));
                     // Console.WriteLine($"{pa.X},{pa.Y},{pb.X},{pb.Y}");
                     // debugCanvasOpt?.DrawLine(pa, pb, occluded ? StrokeStyle.RedHairLineSolid : StrokeStyle.CyanHairLineSolid);
                  }
               }
            }
         }

         if (debugCanvasOpt != null) {
            Console.WriteLine(pass);
         }

         
         while (true) {
            var pazz = 0;
            var niters = 10000;
            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < niters; i++) {
               foreach (var query in queries) {
                  var occluded = anyIntersections(query, segs, false);
                  if (!occluded) pazz++;
               }
            }

            var ms = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine(ms + " " + (ms / niters));
         }
      }
      static bool anyIntersections(IntLineSegment2 query, IntLineSegment2[] segments, bool detectEndpointContainment) {
         int clk(int ax, int ay, int bx, int by) {
            // sign(ax * by - ay * bx);
            var v0 = ax * by;
            var v1 = ay * bx;
            return v0 > v1 ? 1 : v0 < v1 ? -1 : 0;
         }

         int ax = query.First.X;
         int ay = query.First.Y;
         int bx = query.Second.X;
         int by = query.Second.Y;

         int bax = bx - ax;
         int bay = by - ay;

         foreach (var seg in segments) {
            int cx = seg.First.X;
            int cy = seg.First.Y;
            int dx = seg.Second.X;
            int dy = seg.Second.Y;

            int bcx = bx - cx;
            int bcy = by - cy;
            int bdx = bx - dx;
            int bdy = by - dy;

            var o1 = clk(bax, bay, bcx, bcy);
            var o2 = clk(bax, bay, bdx, bdy);
            if (o1 == o2 && !detectEndpointContainment) continue;

            int dcx = dx - cx;
            int dcy = dy - cy;
            int dax = dx - ax;
            int day = dy - ay;
            int dbx = dx - bx;
            int dby = dy - by;

            var o3 = clk(dcx, dcy, dax, day);
            var o4 = clk(dcx, dcy, dbx, dby);
            if (o1 != o2 && o3 != o4) return true;

            if (detectEndpointContainment) {
            }
         }
         return false;
      }


      public struct LinkState {
         public bool Occluded;
      }

      public class BarrierCalculator {
         // To compute barriers, dilate polytrees (so hole regions are away from waypoints
         // then expand segments so they cross each other and are watertight
         private const int kBarrierPolyTreeDilationFactor = 5; // dilation to move holes inward
         private const int kBarrierSegmentExpansionFactor = 10; // expansion to make corners hit
         private const int kBarrierOverDilationFactor = 3;
         
         public static TlsBackedObjectPool<List<IntLineSegment2>> tlsFindContourAndChildHoleBarriersStore = TlsBackedObjectPool.Create<List<IntLineSegment2>>();

         public IntLineSegment2[] CalculateContourAndChildHoleBarriers(PolygonNode root, int exaggerationFactor = 10) {
            var results = tlsFindContourAndChildHoleBarriersStore.UnsafeTakeAndGive();
            results.Clear();
            foreach (var node in root.Dfs((cb, n) => n.Children.ForEach(cb))) {
               if (node.Contour == null) continue;

               var pointCount = node.Contour.Length;
               var isHole = node.IsHole;
               var dilationDirection = isHole ? 1 : 1; // Note: Same value, as hole clockness is opposite of land clockness.

               // TODO: This algo is far faster than dilating the polynode like I did before. However, it probably introduces
               // leaks for sharp corners. Consider working around that by instead dilating corners along their pointed direction
               // & then expanding segments after that.
               for (var i = 0; i < pointCount; i++) {
                  var a = node.Contour[i];
                  var b = node.Contour[(i + 1) % pointCount];

                  var dx = b.X - a.X;
                  var dy = b.Y - a.Y;
                  var mag = (cInt)Math.Sqrt(dx * dx + dy * dy); // normalizing on xy plane.

                  // Move segment toward outside of node.
                  var dilateOffsetX = exaggerationFactor * dilationDirection * dy * kBarrierPolyTreeDilationFactor / mag;
                  var dilateOffsetY = exaggerationFactor * dilationDirection * -dx * kBarrierPolyTreeDilationFactor / mag;

                  // Expand segment to fill leaks at endpoint.
                  var expandOffsetX = exaggerationFactor * dx * kBarrierSegmentExpansionFactor / mag;
                  var expandOffsetY = exaggerationFactor * dy * kBarrierSegmentExpansionFactor / mag;

                  var p1 = new IntVector2(a.X - expandOffsetX + dilateOffsetX, a.Y - expandOffsetY + dilateOffsetY);
                  var p2 = new IntVector2(b.X + expandOffsetX + dilateOffsetX, b.Y + expandOffsetY + dilateOffsetY);

                  results.Add(isHole ? new IntLineSegment2(p2, p1) : new IntLineSegment2(p1, p2));
               }
            }
            return results.ToArray();
         }
      }

      private static PolyTree X() {
         return null;
         Polygon2 ClipperExtentsHoleClipPolygon = Polygon2.CreateRect(
            -InternalTerrainCompilationConstants.SectorClipBounds,
            -InternalTerrainCompilationConstants.SectorClipBounds,
            InternalTerrainCompilationConstants.SectorClipBounds * 2 + 1,
            InternalTerrainCompilationConstants.SectorClipBounds * 2 + 1);

         Polygon2 ClipHoleContour(Polygon2 polygon) {
            PolygonOperations.TryConvexClip(polygon, ClipperExtentsHoleClipPolygon, out var result);
            if (result != null) {
               foreach (var p in result.Points) {
                  var useFullRange = true;
                  ClipperBase.RangeTest(p, ref useFullRange);
               }
            }
            return result;
         }

         // var dilatedHolesUnion =
         //    PolygonOperations.Offset()
         //                     .Include(job.SectorBlueprint.LocalExcludedContours)
         //                     .Include(job.DynamicHoles.Values.SelectMany(item => item.holeIncludedContours)
         //                                 .Select(ClipHoleContour)
         //                                 .Where(p => p != null))
         //                     .Include(job.DynamicHoles.Values.SelectMany(item =>
         //                        item.holeExcludedContours.Select(p => new Polygon2(((IReadOnlyList<IntVector2>)p.Points).Reverse().ToList()))
         //                        ).Select(ClipHoleContour).Where(p => p != null))
         //                     .Dilate(holeDilationRadius)
         //                     .Cleanup()
         //                     .Execute();
         //
         // var erodedOuterContour =
         //    PolygonOperations.Offset()
         //                     .Include(job.SectorBlueprint.LocalIncludedContours)
         //                     .Erode(holeDilationRadius)
         //                     .Cleanup()
         //                     .Execute();

         // var crossoverLandPolys =
         //    job.CrossoverSegments.Select(tuple => {
         //       var (segment, inClockness) = tuple;
         //       var firstToSecond = segment.First.To(segment.Second).ToDoubleVector2();
         //       var perp = new DoubleVector2(firstToSecond.Y, -firstToSecond.X);
         //       var extrusionMagnitude = holeDilationRadius + (cDouble)2;
         //       var inward = perp * (extrusionMagnitude / perp.Norm2D());
         //       var outward = perp * (CDoubleMath.cNeg2 / perp.Norm2D());
         //       if (inClockness == Clockness.CounterClockwise) {
         //          inward *= -1;
         //          outward *= -1;
         //       }
         //
         //       var shrink = firstToSecond * (holeDilationRadius / firstToSecond.Norm2D());
         //       var points = new List<IntVector2>(new[]{
         //          (segment.First.ToDoubleVector2() + outward + shrink).LossyToIntVector2(),
         //          (segment.First.ToDoubleVector2() + inward + shrink).LossyToIntVector2(),
         //          (segment.Second.ToDoubleVector2() + inward - shrink).LossyToIntVector2(),
         //          (segment.Second.ToDoubleVector2() + outward - shrink).LossyToIntVector2()
         //       });
         //
         //       if (inClockness == Clockness.CounterClockwise) {
         //          points.Reverse();
         //       }
         //
         //       return new Polygon2(points);
         //    }).ToArray();
         //
         // PolyTree PostProcessPunchedLand(PolyTree punchedLand) {
         //    void TagSectorSnapshotAndGeometryContext(PolyNode node) {
         //       // node.visibilityGraphNodeData.LocalGeometryView = this;
         //       node.Childs.ForEach(TagSectorSnapshotAndGeometryContext);
         //    }
         //
         //    void TagBoundingVolumeHierarchies(PolyNode node) {
         //       var contourEdges = node.Contour.Zip(node.Contour.RotateLeft(), IntLineSegment2.Create).ToArray();
         //       var bvh = BvhILS2.Build(contourEdges);
         //       node.visibilityGraphNodeData.ContourBvh = bvh;
         //       node.Childs.ForEach(TagBoundingVolumeHierarchies);
         //    }
         //
         //    punchedLand.Prune(holeDilationRadius);
         //    TagSectorSnapshotAndGeometryContext(punchedLand);
         //    TagBoundingVolumeHierarchies(punchedLand);
         //    return punchedLand;
         // }
         //
         // var punchedLand = PostProcessPunchedLand(
         //    PolygonOperations.Punch()
         //                     .Include(erodedOuterContour.FlattenToPolygonAndIsHoles())
         //                     .Include(crossoverLandPolys)
         //                     .Exclude(dilatedHolesUnion.FlattenToPolygonAndIsHoles())
         //                     .Execute()
         //    );
         //
         // return punchedLand;
      }
   }
}
