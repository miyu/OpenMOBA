﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Commons.Pooling;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami;
using Dargon.Terragami.ThirdParty.ClipperLib;
using Poly2Tri.Triangulation;

namespace Dargon.Terragami {
   public static class ClipperAllocator {
      private static readonly TlsBackedObjectPool<ClipperObjectPools> clipperPoolPools = new TlsBackedObjectPool<ClipperObjectPools>(
         _ => new ClipperObjectPools());

      public static Clipper AllocClipper() => new Clipper { StrictlySimple = true, pools = clipperPoolPools.TakeObject(), ReverseSolution = true };
      public static void FreeClipper(ref Clipper c) {
         c.pools.ResetPools();
         clipperPoolPools.ReturnObject(c.pools);
         c = null;
      }

      public static ClipperOffset AllocClipperOffset() => new ClipperOffset { pools = clipperPoolPools.TakeObject(), IsReverseModeEnabled = true };

      public static void FreeClipperOffset(ref ClipperOffset c) {
         c.pools.ResetPools();
         clipperPoolPools.ReturnObject(c.pools);
         c = null;
      }
   }
   
   public static class PolygonOperations {
      public static DoubleVector2 ToOpenMobaPointD(this TriangulationPoint input) {
         // TODO: Determinism issues!
         return new DoubleVector2((Double)input.X, (Double)input.Y);
      }

      public static UnionOperation Union() => new UnionOperation();

      public static PunchOperation Punch() => new PunchOperation();

      public static OffsetOperation Offset() => new OffsetOperation();

      /// <summary>
      /// https://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman_algorithm#Pseudo_code
      /// Fails if result would be empty
      /// </summary>
      public static bool TryConvexClip(Polygon2 subject, Polygon2 clip, out Polygon2 result) {
         bool Inside(IntVector2 p, IntLineSegment2 edge) => GeometryOperations.Clockness(edge.First, edge.Second, p) != Clockness.CounterClockWise;

         List<IntVector2> outputList = subject.Points;
         for (var i = 0; i < clip.Points.Count; i++) {
            var clipEdge = new IntLineSegment2(clip.Points[i], clip.Points[i == clip.Points.Count - 1 ? 0 : i + 1]);
            List<IntVector2> inputList = outputList;
            outputList = new List<IntVector2>();

            var S = inputList[inputList.Count - 1];
            for (var j = 0; j < inputList.Count; j++) {
               var E = inputList[j];
               if (Inside(E, clipEdge)) {
                  if (!Inside(S, clipEdge)) {
                     var SE = new IntLineSegment2(S, E);
                     if (!GeometryOperations.TryFindLineLineIntersection(SE, clipEdge, out var intersection)) {
                        throw new NotImplementedException();
                     }

                     outputList.Add(intersection.LossyToIntVector2());
                  }

                  outputList.Add(E);
               } else if (Inside(S, clipEdge)) {
                  var SE = new IntLineSegment2(S, E);
                  if (!GeometryOperations.TryFindLineLineIntersection(SE, clipEdge, out var intersection)) {
                     throw new NotImplementedException();
                  }

                  outputList.Add(intersection.LossyToIntVector2());
               }

               S = E;
            }

            if (outputList.Count == 0) {
               result = null;
               return false;
            }

            outputList.Add(outputList[0]);
         }

         // enforce open poly
         // todo: fix bug where last point is added many times
         while (outputList[0] == outputList[^1]) {
            outputList.RemoveAt(outputList.Count - 1);
            if (outputList.Count == 0) {
               result = null;
               return false;
            }
         }
         result = new Polygon2(outputList);
         return true;
      }

      public static PolygonNode CleanPolygons(List<Polygon2> polygons) {
         return Offset().Include(polygons)
                        .Erode((Double)0.05)
                        .Dilate((Double)0.05)
                        .Execute();
      }

      public static List<IReadOnlyList<IntVector2>> FlattenToContours(this PolyNode polytree, bool includeOuterPolygon = true) {
         var results = new List<IReadOnlyList<IntVector2>>();
         var depthFilter = includeOuterPolygon ? 0 : 2; // 2 for outer void level and outer land poly level
         FlattenToContoursHelper(polytree, polytree.IsHole, results, depthFilter);
         return results;
      }

      private static void FlattenToContoursHelper(PolyNode current, bool isHole, List<IReadOnlyList<IntVector2>> results, int depthFilter) {
         if (current.Contour.Count > 0 && depthFilter <= 0) {
            results.Add(current.Contour);
         }

         foreach (var child in current.Childs) {
            // We avoid node.isHole as that traverses upwards recursively and wastefully.
            FlattenToContoursHelper(child, !isHole, results, depthFilter - 1);
         }
      }

      public static List<(Polygon2 polygon, bool isHole)> FlattenToPolygonAndIsHoles(this PolygonNode polytree, bool includeOuterPolygon = true, bool flipIsHoleResult = false) {
         var results = new List<(Polygon2, bool)>();
         var depthFilter = includeOuterPolygon ? 0 : 2; // 2 for outer void level and outer land poly level
         FlattenPolyTreeToPolygonsHelper(polytree, polytree.IsHole, results, depthFilter, flipIsHoleResult);
         return results;
      }

      private static void FlattenPolyTreeToPolygonsHelper(PolygonNode current, bool isHole, List<(Polygon2, bool)> results, int depthFilter, bool flipIsHoleResult) {
         if (current.Contour != null && current.Contour.Length > 0 && depthFilter <= 0) {
            var contour = current.Contour.ToList();
            if (isHole) {
               contour.Reverse();
            }

            results.Add((new Polygon2(contour), isHole ^ flipIsHoleResult));
         }

         foreach (var child in current.Children) {
            // We avoid node.isHole as that traverses upwards recursively and wastefully.
            FlattenPolyTreeToPolygonsHelper(child, !isHole, results, depthFilter - 1, flipIsHoleResult);
         }
      }

      public class UnionOperation {
         private Clipper clipper = ClipperAllocator.AllocClipper();

         public UnionOperation Include(params Polygon2[] polygons) => Include((IReadOnlyList<Polygon2>)polygons);

         public UnionOperation Include(IReadOnlyList<Polygon2> polygons) {
            foreach (var polygon in polygons) {
               clipper.AddPath(polygon.Points, PolyType.ptSubject, polygon.IsClosed);
            }
            return this;
         }

         public PolygonNode Execute() {
            var polytree = new PolyTree();
            clipper.ReverseSolution = true;
            clipper.Execute(ClipType.ctUnion, polytree, PolyFillType.pftNegative, PolyFillType.pftNegative);
            ClipperAllocator.FreeClipper(ref clipper);
            return PolygonNode.FromClipperPolyTree(polytree);
         }
      }

      public class PunchOperation {
         private Clipper clipper = ClipperAllocator.AllocClipper();

         public PunchOperation IncludeOrExclude(params (Polygon2 polygon, bool isHole)[] polygonAndIsHoles) => IncludeOrExclude((IReadOnlyList<(Polygon2 polygon, bool isHole)>)polygonAndIsHoles);

         public PunchOperation IncludeOrExclude(IReadOnlyList<(Polygon2 polygon, bool isHole)> polygonAndIsHoles, bool includeHolesExcludeLand = false) {
            foreach (var (polygon, isHole) in polygonAndIsHoles) {
               if (isHole == includeHolesExcludeLand) {
                  Include(polygon);
               } else {
                  Exclude(polygon);
               }
            }

            return this;
         }

         public PunchOperation Include(PolygonNode node) {
            if (node.Contour != null) {
               Include(node.Contour);
            }

            foreach (var child in node.Children) {
               Include(child);
            }

            return this;
         }

         public PunchOperation Include<TPath>(TPath contour) where TPath : IList<IntVector2> {
            clipper.AddPath(contour, PolyType.ptSubject, true);
            return this;
         }

         public PunchOperation Include(params Polygon2[] polygons) => Include((IEnumerable<Polygon2>)polygons);

         public PunchOperation Include(IEnumerable<Polygon2> polygons) {
            foreach (var polygon in polygons) {
               clipper.AddPath(polygon.Points, PolyType.ptSubject, true);
            }
            return this;
         }

         public PunchOperation Include(IReadOnlyList<(Polygon2 polygon, bool isHole)> polygonAndIsHoles) {
            foreach (var (polygon, isHole) in polygonAndIsHoles) {
               var points = polygon.Points;
               if (isHole) {
                  points = points.ToList();
                  points.Reverse();
               }

               clipper.AddPath(points, PolyType.ptSubject, true);

            }

            return this;
         }

         public PunchOperation Exclude(PolygonNode node) {
            if (node.Contour != null) {
               Exclude(node.Contour);
            }

            foreach (var child in node.Children) {
               Exclude(child);
            }

            return this;
         }

         public PunchOperation Exclude<TPath>(TPath contour) where TPath : IList<IntVector2> {
            clipper.AddPath(contour, PolyType.ptClip, true);
            return this;
         }

         public PunchOperation Exclude(params Polygon2[] polygons) => Exclude((IEnumerable<Polygon2>)polygons);

         public PunchOperation Exclude(IEnumerable<Polygon2> polygons) {
            foreach (var polygon in polygons) {
               clipper.AddPath(polygon.Points, PolyType.ptClip, true);
            }

            return this;
         }

         // excludes the polygon/isHole pairs of holes
         public PunchOperation Exclude(IReadOnlyList<(Polygon2 polygon, bool isHole)> polygonAndIsHoles) {
            foreach (var (polygon, isHole) in polygonAndIsHoles) {
               var points = polygon.Points;
               if (isHole) {
                  points = points.ToList();
                  points.Reverse();
               }

               clipper.AddPath(points, PolyType.ptClip, true);

            }

            return this;
         }


         public PolygonNode Execute(Double additionalErosionDilation = default, bool cleanupDegeneraciesWithOffset = true) {
            var polytree = new PolyTree();
            clipper.ReverseSolution = true;
            clipper.Execute(ClipType.ctDifference, polytree, PolyFillType.pftNegative, PolyFillType.pftNegative);
            // return PolygonNode.FromClipperPolyTree(polytree);

            if (!cleanupDegeneraciesWithOffset) {
               ClipperAllocator.FreeClipper(ref clipper);
               return PolygonNode.FromClipperPolyTree(polytree);
            }

            // Used to remove clipping artifacts where additionalErosion is 0.
            // Without this code, even Test2D gets an errant hairline on its island.
            // Can totally optimize this code with something faster than an erode/dliate (e.g. just walk the contours &
            // toss out obviouis artifacts like the follows:
            //
            // 3__2____4
            //    |    |
            //    |____|
            //   1      5

#if use_fixed
            cDouble baseErosion = (cDouble)0.05;
#else
            const double baseErosion = 0.05;
#endif
            var res = Offset().Include(FlattenToPolygonAndIsHoles(PolygonNode.FromClipperPolyTree(polytree)))
                              .Erode(baseErosion)
                              .Dilate(baseErosion)
                              .ErodeOrDilate(additionalErosionDilation)
                              .Execute();
            ClipperAllocator.FreeClipper(ref clipper);
            return res;
         }
      }

      public class OffsetOperation {
#if use_fixed
         private readonly cDouble kSpecialOffsetCleanup = cDouble.MinValue;
#else
         private readonly double kSpecialOffsetCleanup = double.NegativeInfinity;
#endif
         private readonly List<IReadOnlyList<IntVector2>> includedContours = new List<IReadOnlyList<IntVector2>>();
         private readonly List<Double> offsets = new List<Double>();

         /// <param name="delta">Positive dilates, negative erodes</param>
         public OffsetOperation ErodeOrDilate(Double delta) {
#if !use_fixed
            if (double.IsInfinity(delta) || double.IsNaN(delta)) {
               throw new ArgumentException();
            }
#endif
            offsets.Add(delta);
            return this;
         }

         public OffsetOperation Erode(Double delta) {
#if !use_fixed
            if (double.IsInfinity(delta) || double.IsNaN(delta)) {
               throw new ArgumentException();
            }
#endif
            if (delta < (Double)0) {
               throw new ArgumentOutOfRangeException();
            }

            offsets.Add(-delta);
            return this;
         }

         public OffsetOperation Dilate(Double delta) {
#if !use_fixed
            if (double.IsInfinity(delta) || double.IsNaN(delta)) {
               throw new ArgumentException();
            }
#endif
            if (delta < (Double)0) {
               throw new ArgumentOutOfRangeException();
            }

            offsets.Add(delta);
            return this;
         }

         public OffsetOperation Cleanup() {
            offsets.Add(kSpecialOffsetCleanup);
            return this;
         }

         public OffsetOperation Include(params Polygon2[] polygons) => Include((IReadOnlyList<Polygon2>)polygons);

         public OffsetOperation Include(params IReadOnlyList<IntVector2>[] contours) {
            foreach (var contour in contours) {
               includedContours.Add(contour);
            }

            return this;
         }

         public OffsetOperation Include(IEnumerable<Polygon2> polygons) {
            return Include(polygons.Select(p => p.Points));
         }

         public OffsetOperation Include(IEnumerable<IReadOnlyList<IntVector2>> contours) {
            foreach (var contour in contours) {
               includedContours.Add(contour);
            }

            return this;
         }

         public OffsetOperation Include(params (Polygon2 polygon, bool isHole)[] polygons) => Include((IReadOnlyList<(Polygon2, bool)>)polygons);

         public OffsetOperation Include(params (IReadOnlyList<IntVector2>, bool isHole)[] contourAndIsHoles) {
            foreach (var (contour, isHole) in contourAndIsHoles) {
               includedContours.Add(contour);
            }

            return this;
         }

         public OffsetOperation Include(IEnumerable<(Polygon2 polygon, bool isHole)> polygonAndIsHoles) {
            return Include(polygonAndIsHoles.Select(pair => ReverseIfIsHole(pair.polygon.Points, pair.isHole)));
         }

         private Polygon2 ReverseIfIsHole(IReadOnlyList<IntVector2> points, bool isHole) {
            if (isHole) {
               var copy = new List<IntVector2>(points);
               copy.Reverse();
               return new Polygon2(copy);
            }

            return new Polygon2(points.ToList());
         }

         public OffsetOperation Include(IEnumerable<(IReadOnlyList<IntVector2> polygon, bool isHole)> polygonAndIsHoles) {
            foreach (var (polygon, isHole) in polygonAndIsHoles) {
               includedContours.Add(ReverseIfIsHole(polygon, isHole).Points);
            }

            return this;
         }

         public PolygonNode Execute() {
            var currentContours = includedContours;
            for (var i = 0; i < offsets.Count; i++) {
               var offset = offsets[i];

               // ReSharper disable once CompareOfFloatsByEqualityOperator
               if (offset == kSpecialOffsetCleanup) {
                  continue;
               }

               var polytree = new PolyTree();
               var clipper = ClipperAllocator.AllocClipperOffset();
               clipper.IsReverseModeEnabled = true;

               try {
                  foreach (var contour in currentContours) {
                     clipper.AddPath(contour, JoinType.jtMiter, EndType.etClosedPolygon);
                  }

                  // flip offset, because we're working in reverse mode.
                  clipper.Execute(ref polytree, -offset, Clipper.ioStrictlySimple);

                  // hack: cleanup
                  while (i + 1 != offsets.Count && offsets[i + 1] == kSpecialOffsetCleanup) {
                     i++;
                     polytree.Prune(clipper, CDoubleMath.c0);
                  }

                  if (i + 1 == offsets.Count) {
                     // clipper offset (presumably at mitering) can create slightly self-intersecting
                     // polygons like 282, 554; 285, 557; 261, 576; 272, 557; 283, 554
                     // This in turn will break p2t, which wants simple polygons.
                     // As a workaround, do a click clean polygon pass.
                     // TODO: Avoid gcalloc by editing list in-place?
                     var s = new AddOnlyOrderedHashSet<PolyNode> { polytree };
                     for (var j = 0; j < s.Count; j++) {
                        var initialContour = s[j].Contour;
                        var cleanedContour = clipper.CleanPolygon(initialContour);
                        if (initialContour.Count != 0 && cleanedContour.Count == 0) {
                           s[j].Parent.Childs.Remove(s[j]);
                           continue;
                        }

                        s[j].Contour = cleanedContour;
                        foreach (var child in s[j].Childs) s.Add(child);
                     }

                     return PolygonNode.FromClipperPolyTree(polytree);
                  } else {
                     currentContours = polytree.FlattenToContours();
                  }
               } finally {
                  ClipperAllocator.FreeClipperOffset(ref clipper);
               }
            }

            throw new ArgumentException("Must specify some polygons to include!");
         }
      }
   }
}
