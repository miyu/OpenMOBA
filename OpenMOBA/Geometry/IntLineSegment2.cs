using System;
using System.Drawing;

namespace OpenMOBA.Geometry {
   public struct IntLineSegment2 {
      public IntLineSegment2(IntVector2 first, IntVector2 second) {
         First = first;
         Second = second;
      }

      public IntVector2 First { get; }
      public int X1 => First.X;
      public int Y1 => First.Y;

      public IntVector2 Second { get; }
      public int X2 => Second.X;
      public int Y2 => Second.Y;

      public IntVector2[] Points => new[] { First, Second };

      public bool Intersects(IntLineSegment2 other) {
         int ax = X1, ay = Y1, bx = X2, by = Y2;
         int cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;

         // http://stackoverflow.com/questions/3838329/how-can-i-check-if-two-segments-intersect
         var tl = Math.Sign((ax - cx) * (by - cy) - (ay - cy) * (bx - cx));
         var tr = Math.Sign((ax - dx) * (by - dy) - (ay - dy) * (bx - dx));
         var bl = Math.Sign((cx - ax) * (dy - ay) - (cy - ay) * (dx - ax));
         var br = Math.Sign((cx - bx) * (dy - by) - (cy - by) * (dx - bx));

         return tl == -tr && bl == -br;
      }

      public Rectangle ToBoundingBox() {
         var minX = Math.Min(X1, X2);
         var minY = Math.Min(Y1, Y2);
         var width = Math.Abs(X1 - X2) + 1;
         var height = Math.Abs(Y1 - Y2) + 1;

         return new Rectangle(minX, minY, width, height);
      }
   }
}
