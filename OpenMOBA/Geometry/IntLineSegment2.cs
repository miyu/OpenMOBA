using System;
using System.CodeDom;
using System.Drawing;

using cInt = System.Int64;

namespace OpenMOBA.Geometry {
   public struct IntLineSegment2 {
      public IntLineSegment2(IntVector2 first, IntVector2 second) {
         First = first;
         Second = second;
      }

      public IntVector2 First;
      public cInt X1 => First.X;
      public cInt Y1 => First.Y;

      public IntVector2 Second;
      public cInt X2 => Second.X;
      public cInt Y2 => Second.Y;

      public IntVector2[] Points => new[] { First, Second };

      public bool Intersects(IntLineSegment2 other) {
         cInt ax = X1, ay = Y1, bx = X2, by = Y2;
         cInt cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;

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

         return new Rectangle((int)minX, (int)minY, (int)width, (int)height);
      }

      public override bool Equals(object obj) => obj is IntLineSegment2 && Equals((IntLineSegment2)obj);

      // Equality by endpoints, not line geometry
      public bool Equals(IntLineSegment2 other) => First == other.First && Second == other.Second;
      public static bool operator ==(IntLineSegment2 self, IntLineSegment2 other) => self.First == other.First && self.Second == other.Second;
      public static bool operator !=(IntLineSegment2 self, IntLineSegment2 other) => self.First != other.First || self.Second != other.Second;

      public override string ToString() => $"({First}, {Second})";

      public IntVector2 ComputeMidpoint() => new IntVector2((First.X + Second.X) / 2, (First.Y + Second.Y) / 2);
   }

   public struct IntLineSegment3 {
      public IntLineSegment3(IntVector3 first, IntVector3 second) {
         First = first;
         Second = second;
      }

      public IntVector3 First { get; }
      public cInt X1 => First.X;
      public cInt Y1 => First.Y;
      public cInt Z1 => First.Z;

      public IntVector3 Second { get; }
      public cInt X2 => Second.X;
      public cInt Y2 => Second.Y;
      public cInt Z2 => Second.Z;

      public IntVector3[] Points => new[] { First, Second };

      public bool IntersectsXY(IntLineSegment3 other) {
         cInt ax = X1, ay = Y1, bx = X2, by = Y2;
         cInt cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;

         // http://stackoverflow.com/questions/3838329/how-can-i-check-if-two-segments-intersect
         var tl = Math.Sign((ax - cx) * (by - cy) - (ay - cy) * (bx - cx));
         var tr = Math.Sign((ax - dx) * (by - dy) - (ay - dy) * (bx - dx));
         var bl = Math.Sign((cx - ax) * (dy - ay) - (cy - ay) * (dx - ax));
         var br = Math.Sign((cx - bx) * (dy - by) - (cy - by) * (dx - bx));

         return tl == -tr && bl == -br;
      }

      public Rectangle ToBoundingBoxXY2D() {
         var minX = Math.Min(X1, X2);
         var minY = Math.Min(Y1, Y2);
         var width = Math.Abs(X1 - X2) + 1;
         var height = Math.Abs(Y1 - Y2) + 1;

         return new Rectangle((int)minX, (int)minY, (int)width, (int)height);
      }

      public override bool Equals(object obj) => obj is IntLineSegment3 && Equals((IntLineSegment3)obj);

      // Equality by endpoints, not line geometry
      public bool Equals(IntLineSegment3 other) => First == other.First && Second == other.Second;

      public override string ToString() => $"({First}, {Second})";
   }
}
