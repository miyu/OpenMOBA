using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using cInt = System.Int32;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Geometry {
   [StructLayout(LayoutKind.Sequential, Pack = 1)]
   public struct IntLineSegment2 {
      [DebuggerStepThrough]
      public IntLineSegment2(IntVector2 first, IntVector2 second) {
#if DEBUG
         if (first == second) {
            throw new InvalidStateException();
         }
#endif
         First = first;
         Second = second;
      }


      [DebuggerStepThrough]
      public IntLineSegment2(cInt ax, cInt ay, cInt bx, cInt by) {
#if DEBUG
         if (ax == bx && ay == by) {
            throw new InvalidStateException();
         }
#endif
         First = new IntVector2(ax, ay);
         Second = new IntVector2(bx, by);
      }

      public readonly IntVector2 First;
      public cInt X1 => First.X;
      public cInt Y1 => First.Y;

      public readonly IntVector2 Second;
      public cInt X2 => Second.X;
      public cInt Y2 => Second.Y;

      public IntVector2[] Points => new[] { First, Second };

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Intersects(IntLineSegment2 other) {
         cInt ax = X1, ay = Y1, bx = X2, by = Y2;
         cInt cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;
         return Intersects(ax, ay, bx, by, cx, cy, dx, dy);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Intersects(ref IntLineSegment2 other) {
         cInt ax = X1, ay = Y1, bx = X2, by = Y2;
         cInt cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;
         return Intersects(ax, ay, bx, by, cx, cy, dx, dy);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Contains(IntVector2 q) => Contains(ref q);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Contains(ref IntVector2 q) => Contains(First, Second, q);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool Contains(IntVector2 p1, IntVector2 p2, IntVector2 q) => Contains(ref p1, ref p2, ref q);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool Contains(ref IntVector2 p1, ref IntVector2 p2, ref IntVector2 q) {
         var p1p2 = p1.To(p2);
         var p1q = p1.To(q);

         // not on line between p1 p2
         if (GeometryOperations.Clockness(p1p2, p1q) != Clockness.Neither) return false;

         var a = p1p2.Dot(p1q);
         var b = p1p2.SquaredNorm2();

         // outside segment p1-p2 on the side of p1
         if (a < 0) return false;

         // outside segment p1-p2 on the side of p2
         if (a > b) return false;

         return true;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool Contains(cInt p1x, cInt p1y, cInt p2x, cInt p2y, cInt qx, cInt qy) {
         var p1p2x = p2x - p1x;
         var p1p2y = p2y - p1y;
         var p1qx = qx - p1x;
         var p1qy = qy - p1y;

         // not on line between p1 p2
         if (GeometryOperations.Clockness(p1p2x, p1p2y, p1qx, p1qy) != Clockness.Neither) return false;

         var a = (long)p1p2x * p1qx + (long)p1p2y * p1qy; //p1p2.Dot(p1q);
         var b = (long)p1p2x * p1p2x + (long)p1p2y * p1p2y; //p1p2.SquaredNorm2();
         
         // outside segment p1-p2 on the side of p1
         if (a < 0) return false;
         
         // outside segment p1-p2 on the side of p2
         if (a > b) return false;

         return true;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool Intersects(cInt ax, cInt ay, cInt bx, cInt by, cInt cx, cInt cy, cInt dx, cInt dy) {
         // https://www.cdn.geeksforgeeks.org/check-if-two-given-line-segments-intersect/
         // Note, didn't do SO variant because not robust to collinear segments
         // http://stackoverflow.com/questions/3838329/how-can-i-check-if-two-segments-intersect
         var o1 = GeometryOperations.Clockness(ax, ay, bx, by, cx, cy);
         var o2 = GeometryOperations.Clockness(ax, ay, bx, by, dx, dy);
         var o3 = GeometryOperations.Clockness(cx, cy, dx, dy, ax, ay);
         var o4 = GeometryOperations.Clockness(cx, cy, dx, dy, bx, by);

         if (o1 != o2 && o3 != o4) return true;

         // handle endpoint containment.
         if (o1 == 0 && Contains(ax, ay, bx, by, cx, cy)) return true;
         if (o2 == 0 && Contains(ax, ay, bx, by, dx, dy)) return true;
         if (o3 == 0 && Contains(cx, cy, dx, dy, ax, ay)) return true;
         if (o4 == 0 && Contains(cx, cy, dx, dy, bx, by)) return true;

         return false;
      }

      public Rectangle ToBoundingBox() {
         var minX = Math.Min(X1, X2);
         var minY = Math.Min(Y1, Y2);
         var width = Math.Abs(X1 - X2) + 1;
         var height = Math.Abs(Y1 - Y2) + 1;

         return new Rectangle((int)minX, (int)minY, (int)width, (int)height);
      }

      public override bool Equals(object obj) {
         return obj is IntLineSegment2 s && Equals(s);
      }

      public override int GetHashCode() {
         return First.GetHashCode() * 23 + Second.GetHashCode();
      }

      // Equality by endpoints, not line geometry
      public bool Equals(IntLineSegment2 other) {
         return First == other.First && Second == other.Second;
      }

      public static bool operator ==(IntLineSegment2 self, IntLineSegment2 other) {
         return self.First == other.First && self.Second == other.Second;
      }

      public static bool operator !=(IntLineSegment2 self, IntLineSegment2 other) {
         return self.First != other.First || self.Second != other.Second;
      }

      public override string ToString() {
         return $"({First}, {Second})";
      }

      public IntVector2 ComputeMidpoint() {
         return new IntVector2((First.X + Second.X) / 2, (First.Y + Second.Y) / 2);
      }

      public DoubleVector2 PointAt(cDouble t) {
         return First.ToDoubleVector2() * (CDoubleMath.c1 - t) + Second.ToDoubleVector2() * t;
      }

      public void Deconstruct(out IntVector2 first, out IntVector2 second) {
         first = First;
         second = Second;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static IntLineSegment2 Create(IntVector2 first, IntVector2 second) {
         return new IntLineSegment2(first, second);
      }
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

      public override bool Equals(object obj) {
         return obj is IntLineSegment3 && Equals((IntLineSegment3)obj);
      }

      // Equality by endpoints, not line geometry
      public bool Equals(IntLineSegment3 other) {
         return First == other.First && Second == other.Second;
      }

      public override string ToString() {
         return $"({First}, {Second})";
      }

      public void Deconstruct(out IntVector3 first, out IntVector3 second) {
         first = First;
         second = Second;
      }
   }

   public struct DoubleLineSegment2 {
      public DoubleLineSegment2(DoubleVector2 first, DoubleVector2 second) {
         First = first;
         Second = second;
      }

      public readonly DoubleVector2 First;
      public cDouble X1 => First.X;
      public cDouble Y1 => First.Y;

      public readonly DoubleVector2 Second;
      public cDouble X2 => Second.X;
      public cDouble Y2 => Second.Y;

      public DoubleVector2[] Points => new[] { First, Second };

      public bool Intersects(DoubleLineSegment2 other) {
         cDouble ax = X1, ay = Y1, bx = X2, by = Y2;
         cDouble cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;
         return Intersects(ax, ay, bx, by, cx, cy, dx, dy);
      }

      [DebuggerStepThrough] public IntLineSegment2 LossyToIntLineSegment2() => new IntLineSegment2(First.LossyToIntVector2(), Second.LossyToIntVector2());

      public static bool Intersects(cDouble ax, cDouble ay, cDouble bx, cDouble by, cDouble cx, cDouble cy, cDouble dx, cDouble dy) {
         // http://stackoverflow.com/questions/3838329/how-can-i-check-if-two-segments-intersect
         var tl = CDoubleMath.Sign((ax - cx) * (by - cy) - (ay - cy) * (bx - cx));
         var tr = CDoubleMath.Sign((ax - dx) * (by - dy) - (ay - dy) * (bx - dx));
         var bl = CDoubleMath.Sign((cx - ax) * (dy - ay) - (cy - ay) * (dx - ax));
         var br = CDoubleMath.Sign((cx - bx) * (dy - by) - (cy - by) * (dx - bx));

         return tl == -tr && bl == -br;
      }

      public RectangleF ToBoundingBox() {
         var minX = CDoubleMath.Min(X1, X2);
         var minY = CDoubleMath.Min(Y1, Y2);
         var width = CDoubleMath.Abs(X1 - X2) + CDoubleMath.c1;
         var height = CDoubleMath.Abs(Y1 - Y2) + CDoubleMath.c1;

         return new RectangleF((float)minX, (float)minY, (float)width, (float)height);
      }

      public override bool Equals(object obj) {
         return obj is DoubleLineSegment2 && Equals((DoubleLineSegment2)obj);
      }

      public override int GetHashCode() {
         return First.GetHashCode() * 23 + Second.GetHashCode();
      }

      // Equality by endpoints, not line geometry
      public bool Equals(DoubleLineSegment2 other) {
         return First == other.First && Second == other.Second;
      }

      public static bool operator ==(DoubleLineSegment2 self, DoubleLineSegment2 other) {
         return self.First == other.First && self.Second == other.Second;
      }

      public static bool operator !=(DoubleLineSegment2 self, DoubleLineSegment2 other) {
         return self.First != other.First || self.Second != other.Second;
      }

      public override string ToString() {
         return $"({First}, {Second})";
      }

      public DoubleVector2 PointAt(cDouble t) {
         return First * (CDoubleMath.c1 - t) + Second * t;
      }

      public DoubleVector2 ComputeMidpoint() {
         return new DoubleVector2((First.X + Second.X) / CDoubleMath.c2, (First.Y + Second.Y) / CDoubleMath.c2);
      }

      public void Deconstruct(out DoubleVector2 first, out DoubleVector2 second) {
         first = First;
         second = Second;
      }
   }
}
