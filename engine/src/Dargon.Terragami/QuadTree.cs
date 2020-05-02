using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Dargon.PlayOn.Geometry;
using cInt = System.Int32;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.DataStructures {
   public class QuadTree<T> {
      private readonly int maxQuadDepth;
      private readonly int subdivisionItemCountThreshold;

      public QuadTree(int subdivisionItemCountThreshold, int maxQuadDepth, IntRect2 bounds) {
         this.subdivisionItemCountThreshold = subdivisionItemCountThreshold;
         this.maxQuadDepth = maxQuadDepth;

         Root = new Node(0, bounds);
      }

      public Node Root { get; }

      public ICollection<T> Query(Rectangle query) {
         var region = IntRect2.FromRectangle(query);

         var results = new HashSet<T>();
         var s = new Stack<Node>();
         s.Push(Root);
         while (s.Any()) {
            var node = s.Pop();
            if (node.Rect.IntersectsWith(region)) {
               if (node.TopLeft == null) {
                  foreach (var itemAndRect in node.ItemAndRects) {
                     results.Add(itemAndRect.Item);
                  }
               } else {
                  s.Push(node.TopLeft);
                  s.Push(node.TopRight);
                  s.Push(node.BottomLeft);
                  s.Push(node.BottomRight);
               }
            }
         }
         return results;
      }

      public void Insert(T item, IntRect2 regionRect) {
         var itemAndRect = new ItemAndRect { Item = item, Rect = regionRect };
         InsertToNode(Root, itemAndRect);
      }

      private void InsertToNode(Node node, ItemAndRect itemAndRect) {
         if (!node.Rect.IntersectsWith(itemAndRect.Rect)) return;

         //         Console.WriteLine("Insert into " + node.Rect + " " + itemAndRect.Rect);

         if (node.TopLeft == null) {
            node.ItemAndRects.Add(itemAndRect);
            if (node.ItemAndRects.Count >= subdivisionItemCountThreshold) {
               Subdivide(node);
            }
         } else {
            InsertToNode(node.TopLeft, itemAndRect);
            InsertToNode(node.TopRight, itemAndRect);
            InsertToNode(node.BottomLeft, itemAndRect);
            InsertToNode(node.BottomRight, itemAndRect);
         }
      }

      private void Subdivide(Node node) {
         if (node.Depth >= maxQuadDepth || node.ItemAndRects.Count < subdivisionItemCountThreshold) return;

         var rect = node.Rect;
         var cx = (rect.Left + rect.Right) / 2;
         var cy = (rect.Top + rect.Bottom) / 2;
         node.TopLeft = new Node(node.Depth + 1, new IntRect2 { Left = rect.Left, Top = rect.Top, Right = cx, Bottom = cy });
         node.TopRight = new Node(node.Depth + 1, new IntRect2 { Left = cx + 1, Top = rect.Top, Right = rect.Right, Bottom = cy });
         node.BottomLeft = new Node(node.Depth + 1, new IntRect2 { Left = rect.Left, Top = cy + 1, Right = cx, Bottom = rect.Bottom });
         node.BottomRight = new Node(node.Depth + 1, new IntRect2 { Left = cx + 1, Top = cy + 1, Right = rect.Right, Bottom = rect.Bottom });

         foreach (var itemAndRect in node.ItemAndRects) {
            InsertToNode(node, itemAndRect);
         }

         node.ItemAndRects.Clear();
         node.ItemAndRects.Capacity = 0;
      }

      public class Node {
         public Node(int depth, IntRect2 rect) {
            Depth = depth;
            Rect = rect;
         }

         public int Depth;
         public IntRect2 Rect;
         public List<ItemAndRect> ItemAndRects { get; } = new List<ItemAndRect>();

         public Node TopLeft { get; set; }
         public Node TopRight { get; set; }
         public Node BottomLeft { get; set; }
         public Node BottomRight { get; set; }
      }

      public class ItemAndRect {
         public T Item { get; set; }
         public IntRect2 Rect { get; set; }
      }
   }

   // Inclusive on top/left/bottom/right. Bottom has greater value than top.
   public struct IntRect2 {
      public cInt Left;
      public cInt Top;
      public cInt Right;
      public cInt Bottom;

      public IntRect2(cInt left, cInt top, cInt right, cInt bottom) {
         Left = left;
         Top = top;
         Right = right;
         Bottom = bottom;
      }

      public bool IntersectsWith(IntRect2 rect) {
         if (Right < rect.Left) return false;
         if (Left > rect.Right) return false;
         if (Bottom < rect.Top) return false;
         if (Top > rect.Bottom) return false;
         return true;
      }

      public static IntRect2 FromRectangle(Rectangle rect) {
         // Rect bottom and right are exclusive.
         return new IntRect2 {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right - 1,
            Bottom = rect.Bottom - 1
         };
      }

      public override string ToString() => $"Rect {Left} {Top} {Right} {Bottom}";

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static IntRect2 BoundingRectangles(IntRect2 first, IntRect2 second) {
         return new IntRect2 {
            Left = Math.Min(first.Left, second.Left),
            Top = Math.Min(first.Top, second.Top),
            Right = Math.Max(first.Right, second.Right),
            Bottom = Math.Max(first.Bottom, second.Bottom)
         };
      }

      public static IntRect2 BoundingPoints(IntVector2[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = points.Length;
         cInt minX = cInt.MaxValue, minY = cInt.MaxValue, maxX = cInt.MinValue, maxY = cInt.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (points[i].X < minX) minX = points[i].X;
            if (points[i].Y < minY) minY = points[i].Y;
            if (maxX < points[i].X) maxX = points[i].X;
            if (maxY < points[i].Y) maxY = points[i].Y;
         }
         return new IntRect2 { Left = minX, Top = minY, Right = maxX, Bottom = maxY };
      }

      public static IntRect2 BoundingSegments(IntLineSegment2[] subsegments, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = subsegments.Length;
         cInt minX = cInt.MaxValue, minY = cInt.MaxValue, maxX = cInt.MinValue, maxY = cInt.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (subsegments[i].First.X < minX) minX = subsegments[i].First.X;
            if (subsegments[i].Second.X < minX) minX = subsegments[i].Second.X;

            if (subsegments[i].First.Y < minY) minY = subsegments[i].First.Y;
            if (subsegments[i].Second.Y < minY) minY = subsegments[i].Second.Y;

            if (maxX < subsegments[i].First.X) maxX = subsegments[i].First.X;
            if (maxX < subsegments[i].Second.X) maxX = subsegments[i].Second.X;

            if (maxY < subsegments[i].First.Y) maxY = subsegments[i].First.Y;
            if (maxY < subsegments[i].Second.Y) maxY = subsegments[i].Second.Y;
         }
         return new IntRect2 { Left = minX, Top = minY, Right = maxX, Bottom = maxY };
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Contains(IntVector2 p) {
         return Left <= p.X && p.X <= Right && Top <= p.Y && p.Y <= Bottom;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Contains(ref IntVector2 p) {
         return Left <= p.X && p.X <= Right && Top <= p.Y && p.Y <= Bottom;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool FullyContains(IntLineSegment2 segment) {
         return Contains(segment.First) && Contains(segment.Second);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool FullyContains(ref IntLineSegment2 segment) {
         return Contains(segment.First) && Contains(segment.Second);
      }

      // Degenerates into segment-segment or point-segment intersection
      // for either or both left=right, top=bottom.
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool ContainsOrIntersects(IntLineSegment2 segment) {
         return ContainsOrIntersects(ref segment);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool ContainsOrIntersects(ref IntLineSegment2 segment) {
         var tl = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Left, Top);
         var tr = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Right, Top);
         var bl = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Left, Bottom);
         var br = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Right, Bottom);

         // If all on same side (and assuming assume not all collinear), then not intersecting!
         if (tl == tr && tr == bl && bl == br && br != Clockness.Neither) {
            return false;
         }

         // Some point (or cross-sectional segment!) of rect intersects with line formed by segment
         return (segment.X1 >= Left || segment.X2 >= Left) &&
                (segment.X1 <= Right || segment.X2 <= Right) &&
                (segment.Y1 >= Top || segment.Y2 >= Top) &&
                (segment.Y1 <= Bottom || segment.Y2 <= Bottom);
      }
   }

   public struct DoubleRect2 {
      public cDouble Left;
      public cDouble Top;
      public cDouble Right;
      public cDouble Bottom;

      public DoubleRect2(cDouble left, cDouble top, cDouble right, cDouble bottom) {
         Left = left;
         Top = top;
         Right = right;
         Bottom = bottom;
      }

      public bool IntersectsWith(DoubleRect2 rect) {
         if (Right < rect.Left) return false;
         if (Left > rect.Right) return false;
         if (Bottom < rect.Top) return false;
         if (Top > rect.Bottom) return false;
         return true;
      }

      public static DoubleRect2 FromRectangle(Rectangle rect) {
         // Rect bottom and right are exclusive.
         return new DoubleRect2 {
            Left = (cDouble)(rect.Left),
            Top = (cDouble)(rect.Top),
            Right = (cDouble)(rect.Right - 1),
            Bottom = (cDouble)(rect.Bottom - 1)
         };
      }

      public override string ToString() => $"Rect {Left} {Top} {Right} {Bottom}";

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static DoubleRect2 BoundingRectangles(DoubleRect2 first, DoubleRect2 second) {
         return new DoubleRect2 {
            Left = CDoubleMath.Min(first.Left, second.Left),
            Top = CDoubleMath.Min(first.Top, second.Top),
            Right = CDoubleMath.Max(first.Right, second.Right),
            Bottom = CDoubleMath.Max(first.Bottom, second.Bottom)
         };
      }

      public static DoubleRect2 BoundingPoints(DoubleVector2[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = points.Length;
         cDouble minX = cDouble.MaxValue, minY = cDouble.MaxValue, maxX = cDouble.MinValue, maxY = cDouble.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (points[i].X < minX) minX = points[i].X;
            if (points[i].Y < minY) minY = points[i].Y;
            if (maxX < points[i].X) maxX = points[i].X;
            if (maxY < points[i].Y) maxY = points[i].Y;
         }
         return new DoubleRect2 { Left = minX, Top = minY, Right = maxX, Bottom = maxY };
      }

      public static DoubleRect2 BoundingSegments(DoubleLineSegment2[] subsegments, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = subsegments.Length;
         cDouble minX = cDouble.MaxValue, minY = cDouble.MaxValue, maxX = cDouble.MinValue, maxY = cDouble.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (subsegments[i].First.X < minX) minX = subsegments[i].First.X;
            if (subsegments[i].Second.X < minX) minX = subsegments[i].Second.X;

            if (subsegments[i].First.Y < minY) minY = subsegments[i].First.Y;
            if (subsegments[i].Second.Y < minY) minY = subsegments[i].Second.Y;

            if (maxX < subsegments[i].First.X) maxX = subsegments[i].First.X;
            if (maxX < subsegments[i].Second.X) maxX = subsegments[i].Second.X;

            if (maxY < subsegments[i].First.Y) maxY = subsegments[i].First.Y;
            if (maxY < subsegments[i].Second.Y) maxY = subsegments[i].Second.Y;
         }
         return new DoubleRect2 { Left = minX, Top = minY, Right = maxX, Bottom = maxY };
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Contains(DoubleVector2 p) {
         return Left <= p.X && p.X <= Right && Top <= p.Y && p.Y <= Bottom;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Contains(ref DoubleVector2 p) {
         return Left <= p.X && p.X <= Right && Top <= p.Y && p.Y <= Bottom;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool FullyContains(DoubleLineSegment2 segment) {
         return Contains(segment.First) && Contains(segment.Second);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool FullyContains(ref DoubleLineSegment2 segment) {
         return Contains(segment.First) && Contains(segment.Second);
      }

      // Degenerates into segment-segment or point-segment intersection
      // for either or both left=right, top=bottom.
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool ContainsOrIntersects(DoubleLineSegment2 segment) {
         return ContainsOrIntersects(ref segment);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool ContainsOrIntersects(ref DoubleLineSegment2 segment) {
         var tl = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Left, Top);
         var tr = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Right, Top);
         var bl = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Left, Bottom);
         var br = GeometryOperations.Clockness(segment.X1, segment.Y1, segment.X2, segment.Y2, Right, Bottom);

         // If all on same side (and assuming assume not all collinear), then not intersecting!
         if (tl == tr && tr == bl && bl == br && br != Clockness.Neither) {
            return false;
         }

         // Some point (or cross-sectional segment!) of rect intersects with line formed by segment
         return (segment.X1 >= Left || segment.X2 >= Left) &&
                (segment.X1 <= Right || segment.X2 <= Right) &&
                (segment.Y1 >= Top || segment.Y2 >= Top) &&
                (segment.Y1 <= Bottom || segment.Y2 <= Bottom);
      }
   }
}
