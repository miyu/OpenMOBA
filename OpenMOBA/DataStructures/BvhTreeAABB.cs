using System;
using System.Collections.Generic;
using System.Linq;
using OpenMOBA.Geometry;

namespace OpenMOBA.DataStructures {
   public class BvhTreeAABB {
      public readonly BvhTreeAABB First;
      public readonly BvhTreeAABB Second;
      public readonly AxisAlignedBoundingBox[] BoundingBoxes;
      public readonly int BoundingBoxesStartIndexInclusive;
      public readonly int BoundingBoxesEndIndexExclusive;
      public readonly AxisAlignedBoundingBox Bounds;

      private BvhTreeAABB(BvhTreeAABB first, BvhTreeAABB second, AxisAlignedBoundingBox[] boundingBoxes, int boundingBoxesStartIndexInclusive, int boundingBoxesEndIndexExclusive, AxisAlignedBoundingBox bounds) {
         First = first;
         Second = second;
         BoundingBoxes = boundingBoxes;
         BoundingBoxesStartIndexInclusive = boundingBoxesStartIndexInclusive;
         BoundingBoxesEndIndexExclusive = boundingBoxesEndIndexExclusive;
         Bounds = bounds;
      }

      public bool TryIntersect(DoubleVector3 p, out BvhTreeAABB node) {
         return TryIntersect(ref p, out node);
      }

      public bool TryIntersect(ref DoubleVector3 p, out BvhTreeAABB node) {
         if (!Bounds.Contains(ref p)) {
            node = null;
            return false;
         }
         if (First != null) {
            return First.TryIntersect(ref p, out node) || Second.TryIntersect(ref p, out node);
         }
         for (var i = BoundingBoxesStartIndexInclusive; i < BoundingBoxesEndIndexExclusive; i++) {
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
         if (TryIntersect(ref p, out BvhTreeAABB node)) {
            box = node.Bounds;
            return true;
         }
         box = null;
         return false;
      }

      public List<BvhTreeAABB> FindIntersectingLeaves(DoubleVector3 p) {
         var res = new List<BvhTreeAABB>();
         FindIntersectingLeavesInternal(p, res);
         return res;
      }

      private void FindIntersectingLeavesInternal(DoubleVector3 p, List<BvhTreeAABB> results) {
         if (!Bounds.Contains(ref p)) {
            return;
         }
         if (First != null) {
            First.FindIntersectingLeavesInternal(p, results);
            Second.FindIntersectingLeavesInternal(p, results);
         } else {
            for (var i = BoundingBoxesStartIndexInclusive; i < BoundingBoxesEndIndexExclusive; i++) {
               if (BoundingBoxes[i].Contains(ref p)) {
                  results.Add(this);
               }
            }
         }
      }

      public static BvhTreeAABB Build(IEnumerable<AxisAlignedBoundingBox> boundingBoxesEnumerable) {
         var inputBoxes = boundingBoxesEnumerable.ToArray();
         var inputBoxesCenters = inputBoxes.Map(s => s.Center);
         var segmentIndices = Util.Generate(inputBoxes.Length, i => i);
         var xComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].X.CompareTo(inputBoxesCenters[b].X));
         var yComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].Y.CompareTo(inputBoxesCenters[b].Y));
         var zComparer = Comparer<int>.Create((a, b) => inputBoxesCenters[a].Z.CompareTo(inputBoxesCenters[b].Z));
         var outputBoxes = new AxisAlignedBoundingBox[inputBoxes.Length];

         AxisAlignedBoundingBox BoundingBoxes(int startIndexInclusive, int endIndexExclusive) {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
               var center = inputBoxes[segmentIndices[i]].Center;
               var extents = inputBoxes[segmentIndices[i]].Extents;

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

         BvhTreeAABB BuildInternal(int startInclusive, int endExclusive, int depth) {
            if (endExclusive - startInclusive < 16) {
               for (var i = startInclusive; i < endExclusive; i++) {
                  outputBoxes[i] = inputBoxes[segmentIndices[i]];
               }
               return new BvhTreeAABB(null, null, outputBoxes, startInclusive, endExclusive, BoundingBoxes(startInclusive, endExclusive));
            }

            var mod = depth % 3;
            var comparer = mod == 0 ? xComparer : (mod == 1 ? yComparer : zComparer);
            Array.Sort(segmentIndices, startInclusive, endExclusive - startInclusive, comparer);
            int midpoint = (startInclusive + endExclusive) / 2;
            var first = BuildInternal(startInclusive, midpoint, depth + 1);
            var second = BuildInternal(midpoint, endExclusive, depth + 1);
            var bounds = AxisAlignedBoundingBox.BoundingBoxes(first.Bounds, second.Bounds);

            return new BvhTreeAABB(first, second, outputBoxes, startInclusive, endExclusive, bounds);
         }

         return BuildInternal(0, inputBoxes.Length, 0);
      }
   }
}