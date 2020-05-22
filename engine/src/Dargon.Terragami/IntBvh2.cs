using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using Dargon.Commons;
using Dargon.PlayOn.Geometry;
using cInt = System.Int32;
using cDouble = System.Double;

namespace Dargon.PlayOn.DataStructures {
   public class BvhTreeAABB2<TValue> {
      public readonly BvhTreeAABB2<TValue> First;
      public readonly BvhTreeAABB2<TValue> Second;
      public readonly AxisAlignedBoundingBox2[] BoundingBoxes;
      public readonly TValue[] AllValues;
      public readonly int AllValuesStartIndexInclusive;
      public readonly int AllValuesEndIndexExclusive;
      public readonly AxisAlignedBoundingBox2 Bounds;

      // Populated after init
      public BvhTreeAABB2<TValue> Parent;
      public int Depth;
      public List<BvhTreeAABB2<TValue>> AllNodes; // ordered by bfs

      private BvhTreeAABB2(BvhTreeAABB2<TValue> first, BvhTreeAABB2<TValue> second, AxisAlignedBoundingBox2[] boundingBoxes, TValue[] allValues, int allValuesStartIndexInclusive, int allValuesEndIndexExclusive, AxisAlignedBoundingBox2 bounds) {
         First = first;
         Second = second;
         BoundingBoxes = boundingBoxes;
         AllValues = allValues;
         AllValuesStartIndexInclusive = allValuesStartIndexInclusive;
         AllValuesEndIndexExclusive = allValuesEndIndexExclusive;
         Bounds = bounds;
      }

      public Span<TValue> Values => new Span<TValue>(AllValues, AllValuesStartIndexInclusive, AllValuesEndIndexExclusive - AllValuesStartIndexInclusive);

      public bool TryIntersect(in DoubleVector2 p, out BvhTreeAABB2<TValue> node) {
         if (!Bounds.Contains(p)) {
            node = null;
            return false;
         }
         if (First != null) {
            return First.TryIntersect(p, out node) || Second.TryIntersect(p, out node);
         }
         for (var i = AllValuesStartIndexInclusive; i < AllValuesEndIndexExclusive; i++) {
            if (BoundingBoxes[i].Contains(p)) {
               node = this;
               return true;
            }
         }
         node = null;
         return false;
      }

      public List<BvhTreeAABB2<TValue>> FindIntersectingLeaves(in DoubleVector2 p) {
         var res = new List<BvhTreeAABB2<TValue>>();
         FindIntersectingLeavesInternal(p, res);
         return res;
      }

      private void FindIntersectingLeavesInternal(in DoubleVector2 p, List<BvhTreeAABB2<TValue>> results) {
         if (!Bounds.Contains(p)) {
            return;
         }
         if (First != null) {
            First.FindIntersectingLeavesInternal(p, results);
            Second.FindIntersectingLeavesInternal(p, results);
         } else {
            for (var i = AllValuesStartIndexInclusive; i < AllValuesEndIndexExclusive; i++) {
               if (BoundingBoxes[i].Contains(p)) {
                  results.Add(this);
               }
            }
         }
      }

      public static BvhTreeAABB2<TValue> Build(IEnumerable<KeyValuePair<AxisAlignedBoundingBox2, TValue>> kvpEnumerable) {
         var inputKvps = kvpEnumerable.ToArray();
         var inputBoxesCenters = inputKvps.Map(kvp => kvp.Key.Center);
         var segmentIndices = Arrays.Create(inputKvps.Length, i => i);
         var xComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].X.CompareTo(inputBoxesCenters[b].X));
         var yComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].Y.CompareTo(inputBoxesCenters[b].Y));
         var outputBoxes = new AxisAlignedBoundingBox2[inputKvps.Length];
         var outputValues = new TValue[inputKvps.Length];

         AxisAlignedBoundingBox2 BoundingBoxes(int startIndexInclusive, int endIndexExclusive) {
            cDouble minX = cDouble.MaxValue, minY = cDouble.MaxValue, maxX = cDouble.MinValue, maxY = cDouble.MinValue;
            for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
               var center = inputKvps[segmentIndices[i]].Key.Center;
               var extents = inputKvps[segmentIndices[i]].Key.Extents;

               if (center.X - extents.X < minX) minX = center.X - extents.X;
               if (center.X + extents.X > maxX) maxX = center.X + extents.X;

               if (center.Y - extents.Y < minY) minY = center.Y - extents.Y;
               if (center.Y + extents.Y > maxY) maxY = center.Y + extents.Y;
            }
            // ir2 is inclusive
            return AxisAlignedBoundingBox2.FromExtents(minX, minY, maxX, maxY);
         }

         BvhTreeAABB2<TValue> BuildInternal(int startInclusive, int endExclusive, int depth) {
            if (endExclusive - startInclusive < 4) {
               for (var i = startInclusive; i < endExclusive; i++) {
                  outputBoxes[i] = inputKvps[segmentIndices[i]].Key;
                  outputValues[i] = inputKvps[segmentIndices[i]].Value;
               }
               return new BvhTreeAABB2<TValue>(null, null, outputBoxes, outputValues, startInclusive, endExclusive, BoundingBoxes(startInclusive, endExclusive));
            }

            var mod = depth % 2;
            var comparer = mod == 0 ? xComparer : yComparer;
            Array.Sort(segmentIndices, startInclusive, endExclusive - startInclusive, comparer);
            int midpoint = (startInclusive + endExclusive) / 2;
            var first = BuildInternal(startInclusive, midpoint, depth + 1);
            var second = BuildInternal(midpoint, endExclusive, depth + 1);
            var bounds = AxisAlignedBoundingBox2.BoundingBoxes(first.Bounds, second.Bounds);

            return new BvhTreeAABB2<TValue>(first, second, outputBoxes, outputValues, startInclusive, endExclusive, bounds);
         }

         var res = BuildInternal(0, inputKvps.Length, 0);
         var q = new Queue<(int, BvhTreeAABB2<TValue>)>();
         q.Enqueue((0, res));

         var allNodes = new List<BvhTreeAABB2<TValue>>();
         while (q.Count > 0) {
            var (depth, cur) = q.Dequeue();
            allNodes.Add(cur);
            cur.Depth = depth;
            cur.AllNodes = allNodes;
            if (cur.First != null) {
               cur.First.Parent = cur;
               cur.Second.Parent = cur;

               q.Enqueue((depth + 1, cur.First));
               q.Enqueue((depth + 1, cur.Second));
            }
         }

         return res;
      }
   }
}
