using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using cInt = System.Int64;

namespace OpenMOBA.Utilities {
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

   public struct IntRect2 {
      public cInt Left;
      public cInt Top;
      public cInt Right;
      // below top, has greater value than top
      public cInt Bottom;

      public bool IntersectsWith(IntRect2 rect) {
         if (Right < rect.Left) return false;
         if (Left > rect.Right) return false;
         if (Bottom < rect.Top) return false;
         if (Top > rect.Bottom) return false;
         return true;
      }

      public static IntRect2 FromRectangle(Rectangle rect) {
         return new IntRect2 {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right,
            Bottom = rect.Bottom
         };
      }

      public override string ToString() => $"Rect {Left} {Top} {Right} {Bottom}";
   }
}
