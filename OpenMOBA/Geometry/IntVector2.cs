using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using cInt = System.Int32;

namespace OpenMOBA.Geometry {
   public struct DoubleVector2 {
      public double X;
      public double Y;

      [DebuggerStepThrough] public DoubleVector2(double x, double y) { 
         X = x;
         Y = y;
      }

      public double Dot(DoubleVector2 other) => X * other.X + Y * other.Y;

      public double SquaredNorm2D() => Dot(this);
      public double Norm2D() => Math.Sqrt(SquaredNorm2D());

      [Pure] public DoubleVector2 To(DoubleVector2 other) => other - this;

      /// <summary>
      /// result * other ~= Proj(this onto other)
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public double ProjectOntoComponentD(DoubleVector2 other) {
         return other.Dot(this) / other.SquaredNorm2D();
      }

      /// <summary>
      /// Projects this vector onto other vector.
      /// </summary>
      /// <param name="other">The vector being projected onto</param>
      /// <returns></returns>
      public DoubleVector2 ProjectOnto(DoubleVector2 other) {
         var numerator = other.Dot(this);
         var denominator = other.SquaredNorm2D();
         return new DoubleVector2(
            (other.X * numerator) / denominator,
            (other.Y * numerator) / denominator);
      }

      public DoubleVector2 ToUnit() => this / Norm2D();

      [DebuggerStepThrough] public IntVector2 LossyToIntVector2() => new IntVector2((cInt)Math.Floor(X), (cInt)Math.Floor(Y));

      public override int GetHashCode() {
         unchecked {
            return (X.GetHashCode() * 397) ^ Y.GetHashCode();
         }
      }

      public static DoubleVector2 Zero => new DoubleVector2(0, 0);
      public static DoubleVector2 UnitX => new DoubleVector2(1, 0);
      public static DoubleVector2 UnitY => new DoubleVector2(0, 1);

      public static DoubleVector2 operator *(int a, DoubleVector2 b) => new DoubleVector2(a * b.X, a * b.Y);
      public static DoubleVector2 operator *(DoubleVector2 a, int b) => new DoubleVector2(b * a.X, b * a.Y);
      public static DoubleVector2 operator *(double a, DoubleVector2 b) => new DoubleVector2(a * b.X, a * b.Y);
      public static DoubleVector2 operator *(DoubleVector2 a, double b) => new DoubleVector2(b * a.X, b * a.Y);
      public static DoubleVector2 operator /(DoubleVector2 a, int b) => new DoubleVector2(a.X / b, a.Y / b);
      public static DoubleVector2 operator /(DoubleVector2 a, double b) => new DoubleVector2(a.X / b, a.Y / b);
      public static DoubleVector2 operator +(DoubleVector2 a, DoubleVector2 b) => new DoubleVector2(a.X + b.X, a.Y + b.Y);
      public static DoubleVector2 operator -(DoubleVector2 a, DoubleVector2 b) => new DoubleVector2(a.X - b.X, a.Y - b.Y);
      public static bool operator ==(DoubleVector2 a, DoubleVector2 b) => a.X == b.X && a.Y == b.Y;
      public static bool operator !=(DoubleVector2 a, DoubleVector2 b) => a.X != b.X || a.Y != b.Y;
      public override bool Equals(object other) => other is DoubleVector2 && Equals((DoubleVector2)other);
      public bool Equals(DoubleVector2 other) => X == other.X && Y == other.Y;

      public static DoubleVector2 FromRadiusAngle(double radius, double radians) {
         var x = radius * Math.Cos(radians);
         var y = radius * Math.Sin(radians);
         return new DoubleVector2(x, y);
      }

      public void Deconstruct(out double x, out double y) {
         x = X;
         y = Y;
      }

      public override string ToString() => $"[{X}, {Y}]";
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

      [Pure] public cInt Dot(IntVector2 other) => X * other.X + Y * other.Y;

      [Pure] public cInt SquaredNorm2() => Dot(this);
      [Pure] public float Norm2F() => (float)Math.Sqrt(SquaredNorm2());

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
            (other.X * numerator) / denominator,
            (other.Y * numerator) / denominator);
      }

      [Pure] public IntVector2 LossyScale(double scale) => new IntVector2((cInt)(X * scale), (cInt)(Y * scale));

      [DebuggerStepThrough] [Pure] public DoubleVector2 ToDoubleVector2() => new DoubleVector2(X, Y);

      [Pure] public override int GetHashCode() => unchecked((int)((X * 397) ^ Y));

      public override string ToString() => $"({X}, {Y})";

      public static IntVector2 Zero => new IntVector2(0, 0);
      public static IntVector2 UnitX => new IntVector2(1, 0);
      public static IntVector2 UnitY => new IntVector2(0, 1);

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

