using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenMOBA.Geometry;
using cInt = System.Int64;

namespace OpenMOBA.DataStructures {
   public class BvhILS2 {
      public readonly BvhILS2 First;
      public readonly BvhILS2 Second;
      public readonly IntLineSegment2[] Segments;
      public readonly int SegmentsStartIndexInclusive;
      public readonly int SegmentsEndIndexExclusive;
      public readonly IntRect2 Bounds;

      private BvhILS2(BvhILS2 first, BvhILS2 second, IntLineSegment2[] segments, int segmentsStartIndexInclusive, int segmentsEndIndexExclusive, IntRect2 bounds) {
         First = first;
         Second = second;
         Segments = segments;
         SegmentsStartIndexInclusive = segmentsStartIndexInclusive;
         SegmentsEndIndexExclusive = segmentsEndIndexExclusive;
         Bounds = bounds;
      }

      public bool Intersects(IntLineSegment2 segment) {
         return Intersects(ref segment);
      }

      public bool Intersects(ref IntLineSegment2 segment) {
         if (!Bounds.ContainsOrIntersects(ref segment)) {
            return false;
         }
         if (First != null) {
            return First.Intersects(ref segment) || Second.Intersects(ref segment);
         }
         for (var i = SegmentsStartIndexInclusive; i < SegmentsEndIndexExclusive; i++) {
            if (Segments[i].Intersects(ref segment)) {
               return true;
            }
         }
         return false;
      }

      public bool TryIntersect(IntLineSegment2 segment, out DoubleVector2 p) {
         return TryIntersect(ref segment, out p);
      }

      public bool TryIntersect(ref IntLineSegment2 segment, out DoubleVector2 p) {
         if (!Bounds.FullyContains(ref segment)) {
            p = default;
            return false;
         }
         if (First != null) {
            return First.TryIntersect(ref segment, out p) || Second.TryIntersect(ref segment, out p);
         }
         for (var i = SegmentsStartIndexInclusive; i < SegmentsEndIndexExclusive; i++) {
            if (GeometryOperations.TryFindSegmentSegmentIntersection(ref Segments[i], ref segment, out p)) {
               return true;
            }
         }
         p = default;
         return false;
      }

      public static BvhILS2 Build(IEnumerable<IntLineSegment2> segmentEnumerable) {
         var segments = segmentEnumerable.ToArray();
         var segmentAndMidpoints = segments.Map(s => (s, s.ComputeMidpoint()));
         var xComparer = Comparer<(IntLineSegment2, IntVector2)>.Create((a, b) => a.Item2.X.CompareTo(b.Item2.X));
         var yComparer = Comparer<(IntLineSegment2, IntVector2)>.Create((a, b) => a.Item2.Y.CompareTo(b.Item2.Y));

         IntRect2 BoundingSegments(int startIndexInclusive = 0, int endIndexExclusive = -1) {
            if (endIndexExclusive == -1) endIndexExclusive = segmentAndMidpoints.Length;
            cInt minX = cInt.MaxValue, minY = cInt.MaxValue, maxX = cInt.MinValue, maxY = cInt.MinValue;
            for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
               if (segmentAndMidpoints[i].Item1.First.X < minX) minX = segmentAndMidpoints[i].Item1.First.X;
               if (segmentAndMidpoints[i].Item1.Second.X < minX) minX = segmentAndMidpoints[i].Item1.Second.X;

               if (segmentAndMidpoints[i].Item1.First.Y < minY) minY = segmentAndMidpoints[i].Item1.First.Y;
               if (segmentAndMidpoints[i].Item1.Second.Y < minY) minY = segmentAndMidpoints[i].Item1.Second.Y;

               if (maxX < segmentAndMidpoints[i].Item1.First.X) maxX = segmentAndMidpoints[i].Item1.First.X;
               if (maxX < segmentAndMidpoints[i].Item1.Second.X) maxX = segmentAndMidpoints[i].Item1.Second.X;

               if (maxY < segmentAndMidpoints[i].Item1.First.Y) maxY = segmentAndMidpoints[i].Item1.First.Y;
               if (maxY < segmentAndMidpoints[i].Item1.Second.Y) maxY = segmentAndMidpoints[i].Item1.Second.Y;
            }
            return new IntRect2 { Left = minX, Top = minY, Right = maxX + 1, Bottom = maxY + 1};
         }

         BvhILS2 BuildInternal(int startInclusive, int endExclusive, bool splitXElseY) {
            if (endExclusive - startInclusive < 8) {
               return new BvhILS2(null, null, segments, startInclusive, endExclusive, BoundingSegments(startInclusive, endExclusive));
            }

            Array.Sort(segmentAndMidpoints, startInclusive, endExclusive - startInclusive, splitXElseY ? xComparer : yComparer);
            int midpoint = (startInclusive + endExclusive) / 2;
            var first = BuildInternal(startInclusive, midpoint, !splitXElseY);
            var second = BuildInternal(midpoint, endExclusive, !splitXElseY);
            var bounds = IntRect2.BoundingRectangles(first.Bounds, second.Bounds);
            return new BvhILS2(first, second, segments, startInclusive, endExclusive, bounds);
         }

         return BuildInternal(0, segmentAndMidpoints.Length, true);
      }
   }
}
