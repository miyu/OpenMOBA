using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using cInt = System.Int32;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Geometry {
   public struct DoubleVector2 {
      public cDouble X;
      public cDouble Y;

      [DebuggerStepThrough] public DoubleVector2(int x, int y) : this((cDouble)x, (cDouble)y) { }

      [DebuggerStepThrough] public DoubleVector2(cDouble x, cDouble y) { 
         X = x;
         Y = y;
      }

      public cDouble Dot(DoubleVector2 other) => X * other.X + Y * other.Y;

      public cDouble SquaredNorm2D() => Dot(this);
      public cDouble Norm2D() => CDoubleMath.Sqrt(SquaredNorm2D());

      [Pure] public DoubleVector2 To(DoubleVector2 other) => other - this;

      /// <summary>
      /// result * other ~= Proj(this onto other)
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public cDouble ProjectOntoComponentD(DoubleVector2 other) {
         return other.Dot(this) / other.SquaredNorm2D();
      }

      /// <summary>
      /// Projects this vector onto other vector.
      /// Note on overflow: Given p = proj x onto o,
      /// |p| lte |x| always.
      /// </summary>
      /// <param name="other">The vector being projected onto</param>
      /// <returns></returns>
      public DoubleVector2 ProjectOnto(DoubleVector2 other) {
         var numerator = other.Dot(this); // Max 2^29
         var denominator = other.SquaredNorm2D(); // Max 2^29
         return new DoubleVector2(
            other.X * (numerator / denominator),
            other.Y * (numerator / denominator));
      }

      public DoubleVector2 ToUnit() => this / Norm2D();

      // Up vector goes left.
      public DoubleVector2 PerpLeft() => new DoubleVector2(-this.Y, this.X);

      // Up vector goes right.
      public DoubleVector2 PerpRight() => new DoubleVector2(this.Y, -this.X);

      [DebuggerStepThrough] [Pure] public IntVector2 LossyToIntVector2() => new IntVector2((cInt)CDoubleMath.Floor(X), (cInt)CDoubleMath.Floor(Y));

      public override int GetHashCode() {
         unchecked {
            return (X.GetHashCode() * 397) ^ Y.GetHashCode();
         }
      }

      public static DoubleVector2 Zero => new DoubleVector2(CDoubleMath.c0, CDoubleMath.c0);
      public static DoubleVector2 UnitX => new DoubleVector2(CDoubleMath.c1, CDoubleMath.c0);
      public static DoubleVector2 UnitY => new DoubleVector2(CDoubleMath.c0, CDoubleMath.c1);

      public static DoubleVector2 operator -(DoubleVector2 a) => new DoubleVector2(-a.X, -a.Y);
      public static DoubleVector2 operator *(int a, DoubleVector2 b) => new DoubleVector2((cDouble)a * b.X, (cDouble)a * b.Y);
      public static DoubleVector2 operator *(DoubleVector2 a, int b) => new DoubleVector2((cDouble)b * a.X, (cDouble)b * a.Y);
      public static DoubleVector2 operator *(cDouble a, DoubleVector2 b) => new DoubleVector2(a * b.X, a * b.Y);
      public static DoubleVector2 operator *(DoubleVector2 a, cDouble b) => new DoubleVector2(b * a.X, b * a.Y);
      public static DoubleVector2 operator /(DoubleVector2 a, int b) => new DoubleVector2(a.X / (cDouble)b, a.Y / (cDouble)b);
      public static DoubleVector2 operator /(DoubleVector2 a, cDouble b) => new DoubleVector2(a.X / b, a.Y / b);
      public static DoubleVector2 operator +(DoubleVector2 a, DoubleVector2 b) => new DoubleVector2(a.X + b.X, a.Y + b.Y);
      public static DoubleVector2 operator -(DoubleVector2 a, DoubleVector2 b) => new DoubleVector2(a.X - b.X, a.Y - b.Y);
      public static bool operator ==(DoubleVector2 a, DoubleVector2 b) => a.X == b.X && a.Y == b.Y;
      public static bool operator !=(DoubleVector2 a, DoubleVector2 b) => a.X != b.X || a.Y != b.Y;
      public override bool Equals(object other) => other is DoubleVector2 && Equals((DoubleVector2)other);
      public bool Equals(DoubleVector2 other) => X == other.X && Y == other.Y;

      public static explicit operator DoubleVector2(DoubleVector3 v) => new DoubleVector2(v.X, v.Y);

      public static DoubleVector2 FromRadiusAngle(cDouble radius, cDouble radians) {
         var x = radius * CDoubleMath.Cos(radians);
         var y = radius * CDoubleMath.Sin(radians);
         return new DoubleVector2(x, y);
      }

      public void Deconstruct(out cDouble x, out cDouble y) {
         x = X;
         y = Y;
      }

      public override string ToString() => $"[{X:F5}, {Y:F5}]";
   }

   [StructLayout(LayoutKind.Sequential, Pack = 1)]
   public struct IntVector2 {
      public cInt X;
      public cInt Y;

      public const int Size = 2 * sizeof(cInt);

      [DebuggerStepThrough]
      public IntVector2(cInt x, cInt y) {
         X = x;
         Y = y;
      }

      [Pure] public long Dot(IntVector2 other) => (long)X * other.X + (long)Y * other.Y;

      [Pure] public long SquaredNorm2() => Dot(this);
      [Pure] public cDouble Norm2F() {
#if use_fixed
         // Given C = AB
         // SQRT(C) = SQRT(AB) = SQRT(A)SQRT(B)
         // N2 possibly reaches UINT32_MAX in OpenMOBA, can shift precision 32 bits to right
         var n2 = SquaredNorm2();
         var sqrtb = 16;
         var b = sqrtb * sqrtb;
         var a = n2 / b;
         return cDouble.Sqrt((cDouble)a) * (cDouble)sqrtb;
#else
         return Math.Sqrt(SquaredNorm2());
#endif
      }

      [Pure] public IntVector2 To(IntVector2 other) => other - this;

      /// <summary>
      /// result * other ~= Proj(this onto other)
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      [Pure] public double ProjectOntoComponentD(IntVector2 other) {
         return other.Dot(this) / (double)other.SquaredNorm2();
      }

      /// <summary>
      /// Projects this vector onto other vector.
      /// </summary>
      /// <param name="other">The vector being projected onto</param>
      /// <returns></returns>
      [Pure] public IntVector2 LossyProjectOnto(IntVector2 other) {
         var numerator = other.Dot(this);
         var denominator = other.SquaredNorm2();
         return new IntVector2(
            (cInt)((other.X * numerator) / denominator),
            (cInt)((other.Y * numerator) / denominator));
      }

      [Pure] public IntVector2 LossyScale(double scale) => new IntVector2((cInt)(X * scale), (cInt)(Y * scale));

      [DebuggerStepThrough] [Pure] public DoubleVector2 ToDoubleVector2() => new DoubleVector2((cDouble)X, (cDouble)Y);

      [Pure] public override int GetHashCode() => unchecked((int)((X * 397) ^ Y));

      public override string ToString() => $"({X}, {Y})";

      public static IntVector2 Zero => new IntVector2(0, 0);
      public static IntVector2 UnitX => new IntVector2(1, 0);
      public static IntVector2 UnitY => new IntVector2(0, 1);

      public static IntVector2 operator -(IntVector2 a) => new IntVector2(-a.X, -a.Y);
      public static IntVector2 operator *(int a, IntVector2 b) => new IntVector2(a * b.X, a * b.Y);
      public static IntVector2 operator *(IntVector2 a, int b) => new IntVector2(b * a.X, b * a.Y);
      public static IntVector2 operator +(IntVector2 a, IntVector2 b) => new IntVector2(a.X + b.X, a.Y + b.Y);
      public static IntVector2 operator -(IntVector2 a, IntVector2 b) => new IntVector2(a.X - b.X, a.Y - b.Y);
      public static bool operator ==(IntVector2 a, IntVector2 b) => a.X == b.X && a.Y == b.Y;
      public static bool operator !=(IntVector2 a, IntVector2 b) => a.X != b.X || a.Y != b.Y;
      public override bool Equals(object other) => other is IntVector2 && Equals((IntVector2)other);
      public bool Equals(IntVector2 other) => X == other.X && Y == other.Y;

      public static IntVector2 FromRadiusAngle(int radius, double radians) {
         var x = (cInt)(radius * Math.Cos(radians));
         var y = (cInt)(radius * Math.Sin(radians));
         return new IntVector2(x, y);
      }

      public void Deconstruct(out cInt x, out cInt y) {
         x = X;
         y = Y;
      }
   }
}

