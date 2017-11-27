using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using cInt = System.Int32;

namespace OpenMOBA.Geometry {
   [StructLayout(LayoutKind.Sequential, Pack = 1)]
   public struct IntLineSegment2 {
      public IntLineSegment2(IntVector2 first, IntVector2 second) {
         First = first;
         Second = second;
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
      public static bool Intersects(cInt ax, cInt ay, cInt bx, cInt by, cInt cx, cInt cy, cInt dx, cInt dy) {
         // // http://stackoverflow.com/questions/3838329/how-can-i-check-if-two-segments-intersect
         // var tl = Math.Sign((ax - cx) * (by - cy) - (ay - cy) * (bx - cx));
         // var tr = Math.Sign((ax - dx) * (by - dy) - (ay - dy) * (bx - dx));
         // var bl = Math.Sign((cx - ax) * (dy - ay) - (cy - ay) * (dx - ax));
         // var br = Math.Sign((cx - bx) * (dy - by) - (cy - by) * (dx - bx));

         var axcx = ax - cx;
         var bycy = by - cy;
         var aycy = ay - cy;
         var bxcx = bx - cx;

         var axdx = ax - dx;
         var bydy = by - dy;
         var aydy = ay - dy;
         var bxdx = bx - dx;

         var tl = Math.Sign(axcx * bycy - aycy * bxcx);
         var tr = Math.Sign(axdx * bydy - aydy * bxdx);
         if (tl != -tr) return false;

         var bl = Math.Sign(axcx * aydy - aycy * axdx);
         var br = Math.Sign(bxcx * bydy - bycy * bxdx);
         return bl == -br;
      }

      public Rectangle ToBoundingBox() {
         var minX = Math.Min(X1, X2);
         var minY = Math.Min(Y1, Y2);
         var width = Math.Abs(X1 - X2) + 1;
         var height = Math.Abs(Y1 - Y2) + 1;

         return new Rectangle((int)minX, (int)minY, (int)width, (int)height);
      }

      public override bool Equals(object obj) {
         return obj is IntLineSegment2 && Equals((IntLineSegment2)obj);
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

      public DoubleVector2 PointAt(double t) {
         return First.ToDoubleVector2() * (1 - t) + Second.ToDoubleVector2() * t;
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
      public double X1 => First.X;
      public double Y1 => First.Y;

      public readonly DoubleVector2 Second;
      public double X2 => Second.X;
      public double Y2 => Second.Y;

      public DoubleVector2[] Points => new[] { First, Second };

      public bool Intersects(DoubleLineSegment2 other) {
         double ax = X1, ay = Y1, bx = X2, by = Y2;
         double cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;
         return Intersects(ax, ay, bx, by, cx, cy, dx, dy);
      }

      public static bool Intersects(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy) {
         // http://stackoverflow.com/questions/3838329/how-can-i-check-if-two-segments-intersect
         var tl = Math.Sign((ax - cx) * (by - cy) - (ay - cy) * (bx - cx));
         var tr = Math.Sign((ax - dx) * (by - dy) - (ay - dy) * (bx - dx));
         var bl = Math.Sign((cx - ax) * (dy - ay) - (cy - ay) * (dx - ax));
         var br = Math.Sign((cx - bx) * (dy - by) - (cy - by) * (dx - bx));

         return tl == -tr && bl == -br;
      }

      public RectangleF ToBoundingBox() {
         var minX = Math.Min(X1, X2);
         var minY = Math.Min(Y1, Y2);
         var width = Math.Abs(X1 - X2) + 1;
         var height = Math.Abs(Y1 - Y2) + 1;

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

      public DoubleVector2 PointAt(double t) {
         return First * (1 - t) + Second * t;
      }

      public DoubleVector2 ComputeMidpoint() {
         return new DoubleVector2((First.X + Second.X) / 2, (First.Y + Second.Y) / 2);
      }

      public void Deconstruct(out DoubleVector2 first, out DoubleVector2 second) {
         first = First;
         second = Second;
      }
   }
}
