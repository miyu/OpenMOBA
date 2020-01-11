using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using Dargon.Commons;
using Dargon.PlayOn.Geometry;
using cInt = System.Int32;

namespace Dargon.PlayOn.DataStructures {
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

      public bool Intersects(IntLineSegment2 segment, bool testSegmentEndpointContainment = true) {
         return Intersects(ref segment, testSegmentEndpointContainment);
      }

      public bool Intersects(ref IntLineSegment2 segment, bool testSegmentEndpointContainment = true) {
         if (!Bounds.ContainsOrIntersects(ref segment)) {
            return false;
         }
         if (First != null) {
            return First.Intersects(ref segment, testSegmentEndpointContainment) || 
                   Second.Intersects(ref segment, testSegmentEndpointContainment);
         }
         for (var i = SegmentsStartIndexInclusive; i < SegmentsEndIndexExclusive; i++) {
            var intersects = testSegmentEndpointContainment
               ? Segments[i].Intersects(ref segment)
               : Segments[i].OpenIntersects(ref segment);
            if (intersects) {
               return true;
            }
         }
         return false;
      }

      public bool TryIntersect(IntLineSegment2 segment, out DoubleVector2 p) {
         return TryIntersect(ref segment, out p);
      }

      public bool TryIntersect(ref IntLineSegment2 segment, out DoubleVector2 p) {
         if (!Bounds.ContainsOrIntersects(ref segment)) {
            p = default(DoubleVector2);
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
         p = default(DoubleVector2);
         return false;
      }

      public List<BvhILS2> FindPotentiallyIntersectingLeaves(IntLineSegment2 seg) {
         var res = new List<BvhILS2>();
         FindPotentiallyIntersectingLeavesInternal(ref seg, res);
         return res;
      }

      private void FindPotentiallyIntersectingLeavesInternal(ref IntLineSegment2 seg, List<BvhILS2> results) {
         if (!Bounds.ContainsOrIntersects(ref seg)) {
            return;
         }
         if (First != null) {
            First.FindPotentiallyIntersectingLeavesInternal(ref seg, results);
            Second.FindPotentiallyIntersectingLeavesInternal(ref seg, results);
         } else {
            results.Add(this);
         }
      }

      public void DumpToConsole(int indent) {
         Console.WriteLine(new string('\t', indent * 2) + Bounds);
         for (var i = SegmentsStartIndexInclusive; i < SegmentsEndIndexExclusive; i++) {
            Console.ForegroundColor = Bounds.FullyContains(Segments[i]) ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(new string('\t', indent * 2 + 1) + Segments[i]);
         }
         First?.DumpToConsole(indent + 1);
         Second?.DumpToConsole(indent + 1);
         Console.ForegroundColor = ConsoleColor.Gray;
      }

      public static BvhILS2 Build(IEnumerable<IntLineSegment2> segmentEnumerable) {
         return Build(segmentEnumerable.ToArray());
      }

      public static BvhILS2 Build(IntLineSegment2[] inputSegments) {
         var inputSegmentMidpoints = inputSegments.Map(s => s.ComputeMidpoint());
         var segmentIndices = Util.Generate(inputSegments.Length, i => i);
         var xComparer = Comparer<int>.Create((a, b) => inputSegmentMidpoints[a].X.CompareTo(inputSegmentMidpoints[b].X));
         var yComparer = Comparer<int>.Create((a, b) => inputSegmentMidpoints[a].Y.CompareTo(inputSegmentMidpoints[b].Y));
         var outputSegments = new IntLineSegment2[inputSegments.Length];

         unsafe {
            fixed (IntLineSegment2* pls2 = outputSegments) {

            }
         }

         IntRect2 BoundingSegments(int startIndexInclusive, int endIndexExclusive) {
            cInt minX = cInt.MaxValue, minY = cInt.MaxValue, maxX = cInt.MinValue, maxY = cInt.MinValue;
            for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
               if (inputSegments[segmentIndices[i]].First.X < minX) minX = inputSegments[segmentIndices[i]].First.X;
               if (inputSegments[segmentIndices[i]].Second.X < minX) minX = inputSegments[segmentIndices[i]].Second.X;

               if (inputSegments[segmentIndices[i]].First.Y < minY) minY = inputSegments[segmentIndices[i]].First.Y;
               if (inputSegments[segmentIndices[i]].Second.Y < minY) minY = inputSegments[segmentIndices[i]].Second.Y;

               if (maxX < inputSegments[segmentIndices[i]].First.X) maxX = inputSegments[segmentIndices[i]].First.X;
               if (maxX < inputSegments[segmentIndices[i]].Second.X) maxX = inputSegments[segmentIndices[i]].Second.X;

               if (maxY < inputSegments[segmentIndices[i]].First.Y) maxY = inputSegments[segmentIndices[i]].First.Y;
               if (maxY < inputSegments[segmentIndices[i]].Second.Y) maxY = inputSegments[segmentIndices[i]].Second.Y;
            }
            // ir2 is inclusive
            return new IntRect2 { Left = minX, Top = minY, Right = maxX, Bottom = maxY};
         }

         BvhILS2 BuildInternal(int startInclusive, int endExclusive, bool splitXElseY) {
            if (endExclusive - startInclusive < 16) {
               for (var i = startInclusive; i < endExclusive; i++) {
                  outputSegments[i] = inputSegments[segmentIndices[i]];
               }
               return new BvhILS2(null, null, outputSegments, startInclusive, endExclusive, BoundingSegments(startInclusive, endExclusive));
            }

            Array.Sort(segmentIndices, startInclusive, endExclusive - startInclusive, splitXElseY ? xComparer : yComparer);
            int midpoint = (startInclusive + endExclusive) / 2;
            var first = BuildInternal(startInclusive, midpoint, !splitXElseY);
            var second = BuildInternal(midpoint, endExclusive, !splitXElseY);
            var bounds = IntRect2.BoundingRectangles(first.Bounds, second.Bounds);

            return new BvhILS2(first, second, outputSegments, startInclusive, endExclusive, bounds);
         }
         return BuildInternal(0, inputSegments.Length, true);
      }
   }
}
