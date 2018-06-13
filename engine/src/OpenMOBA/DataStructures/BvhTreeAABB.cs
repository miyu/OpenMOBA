using System;
using System.Collections.Generic;
using System.Linq;
using OpenMOBA.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.DataStructures {
   public class BvhTreeAABB<TValue> {
      public readonly BvhTreeAABB<TValue> First;
      public readonly BvhTreeAABB<TValue> Second;
      public readonly AxisAlignedBoundingBox[] BoundingBoxes;
      public readonly TValue[] Values;
      public readonly int StartIndexInclusive;
      public readonly int EndIndexExclusive;
      public readonly AxisAlignedBoundingBox Bounds;

      private BvhTreeAABB(BvhTreeAABB<TValue> first, BvhTreeAABB<TValue> second, AxisAlignedBoundingBox[] boundingBoxes, TValue[] values, int startIndexInclusive, int endIndexExclusive, AxisAlignedBoundingBox bounds) {
         First = first;
         Second = second;
         BoundingBoxes = boundingBoxes;
         Values = values;
         StartIndexInclusive = startIndexInclusive;
         EndIndexExclusive = endIndexExclusive;
         Bounds = bounds;
      }

      public bool TryIntersect(DoubleVector3 p, out BvhTreeAABB<TValue> node) {
         return TryIntersect(ref p, out node);
      }

      public bool TryIntersect(ref DoubleVector3 p, out BvhTreeAABB<TValue> node) {
         if (!Bounds.Contains(ref p)) {
            node = null;
            return false;
         }
         if (First != null) {
            return First.TryIntersect(ref p, out node) || Second.TryIntersect(ref p, out node);
         }
         for (var i = StartIndexInclusive; i < EndIndexExclusive; i++) {
            if (BoundingBoxes[i].Contains(ref p)) {
               node = this;
               return true;
            }
         }
         node = null;
         return false;
      }

      public bool TryIntersect(DoubleVector3 p, out AxisAlignedBoundingBox box) {
         return TryIntersect(ref p, out box);
      }

      public bool TryIntersect(ref DoubleVector3 p, out AxisAlignedBoundingBox box) {
         if (TryIntersect(ref p, out BvhTreeAABB<TValue> node)) {
            box = node.Bounds;
            return true;
         }
         box = null;
         return false;
      }

      public List<BvhTreeAABB<TValue>> FindIntersectingLeaves(DoubleVector3 p) {
         var res = new List<BvhTreeAABB<TValue>>();
         FindIntersectingLeavesInternal(p, res);
         return res;
      }

      private void FindIntersectingLeavesInternal(DoubleVector3 p, List<BvhTreeAABB<TValue>> results) {
         if (!Bounds.Contains(ref p)) {
            return;
         }
         if (First != null) {
            First.FindIntersectingLeavesInternal(p, results);
            Second.FindIntersectingLeavesInternal(p, results);
         } else {
            for (var i = StartIndexInclusive; i < EndIndexExclusive; i++) {
               if (BoundingBoxes[i].Contains(ref p)) {
                  results.Add(this);
               }
            }
         }
      }

      public static BvhTreeAABB<TValue> Build(IEnumerable<KeyValuePair<AxisAlignedBoundingBox, TValue>> kvpEnumerable) {
         var inputKvps = kvpEnumerable.ToArray();
         var inputBoxesCenters = inputKvps.Map(kvp => kvp.Key.Center);
         var segmentIndices = Util.Generate(inputKvps.Length, i => i);
         var xComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].X.CompareTo(inputBoxesCenters[b].X));
         var yComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].Y.CompareTo(inputBoxesCenters[b].Y));
         var zComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].Z.CompareTo(inputBoxesCenters[b].Z));
         var outputBoxes = new AxisAlignedBoundingBox[inputKvps.Length];
         var outputValues = new TValue[inputKvps.Length];

         AxisAlignedBoundingBox BoundingBoxes(int startIndexInclusive, int endIndexExclusive) {
            cDouble minX = cDouble.MaxValue, minY = cDouble.MaxValue, minZ = cDouble.MaxValue, maxX = cDouble.MinValue, maxY = cDouble.MinValue, maxZ = cDouble.MinValue;
            for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
               var center = inputKvps[segmentIndices[i]].Key.Center;
               var extents = inputKvps[segmentIndices[i]].Key.Extents;

               if (center.X - extents.X < minX) minX = center.X - extents.X;
               if (center.X + extents.X > maxX) maxX = center.X + extents.X;

               if (center.Y - extents.Y < minY) minY = center.Y - extents.Y;
               if (center.Y + extents.Y > maxY) maxY = center.Y + extents.Y;

               if (center.Z - extents.Z < minZ) minZ = center.Z - extents.Z;
               if (center.Z + extents.Z > maxZ) maxZ = center.Z + extents.Z;
            }
            // ir2 is inclusive
            return AxisAlignedBoundingBox.FromExtents(minX, minY, minZ, maxX, maxY, maxZ);
         }

         BvhTreeAABB<TValue> BuildInternal(int startInclusive, int endExclusive, int depth) {
            if (endExclusive - startInclusive < 4) {
               for (var i = startInclusive; i < endExclusive; i++) {
                  outputBoxes[i] = inputKvps[segmentIndices[i]].Key;
                  outputValues[i] = inputKvps[segmentIndices[i]].Value;
               }
               return new BvhTreeAABB<TValue>(null, null, outputBoxes, outputValues, startInclusive, endExclusive, BoundingBoxes(startInclusive, endExclusive));
            }

            var mod = depth % 3;
            var comparer = mod == 0 ? xComparer : (mod == 1 ? yComparer : zComparer);
            Array.Sort(segmentIndices, startInclusive, endExclusive - startInclusive, comparer);
            int midpoint = (startInclusive + endExclusive) / 2;
            var first = BuildInternal(startInclusive, midpoint, depth + 1);
            var second = BuildInternal(midpoint, endExclusive, depth + 1);
            var bounds = AxisAlignedBoundingBox.BoundingBoxes(first.Bounds, second.Bounds);

            return new BvhTreeAABB<TValue>(first, second, outputBoxes, outputValues, startInclusive, endExclusive, bounds);
         }

         return BuildInternal(0, inputKvps.Length, 0);
      }
   }
}