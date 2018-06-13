using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using cInt = System.Int32;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Geometry {
   public struct DoubleVector3 {
      public cDouble X;
      public cDouble Y;
      public cDouble Z;

      [DebuggerStepThrough] public DoubleVector3(DoubleVector2 v, cDouble z = default) : this(v.X, v.Y, z) { }

      [DebuggerStepThrough]
      public DoubleVector3(int x, int y, int z) : this((cDouble)x, (cDouble)y, (cDouble)z) { }

      [DebuggerStepThrough] public DoubleVector3(cDouble x, cDouble y, cDouble z) {
         X = x;
         Y = y;
         Z = z;
      }

      public DoubleVector2 XY => new DoubleVector2(X, Y);

      public cDouble Dot(DoubleVector3 other) => X * other.X + Y * other.Y + Z * other.Z;

      public cDouble SquaredNorm2D() => Dot(this);
      public cDouble Norm2D() => CDoubleMath.Sqrt(SquaredNorm2D());

      public DoubleVector3 Cross(DoubleVector3 other) {
         cDouble u1 = X, u2 = Y, u3 = Z,
                 v1 = other.X, v2 = other.Y, v3 = other.Z;
         return new DoubleVector3(
            u2 * v3 - u3 * v2, 
            u3 * v1 - u1 * v3, 
            u1 * v2 - u2 * v1);
      }

      [Pure]
      public DoubleVector3 To(DoubleVector3 other) => other - this;

      /// <summary>
      /// result * other ~= Proj(this onto other)
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public cDouble ProjectOntoComponentD(DoubleVector3 other) {
         return other.Dot(this) / other.SquaredNorm2D();
      }

      /// <summary>
      /// Projects this vector onto other vector.
      /// </summary>
      /// <param name="other">The vector being projected onto</param>
      /// <returns></returns>
      public DoubleVector3 ProjectOnto(DoubleVector3 other) {
         var numerator = other.Dot(this);
         var denominator = other.SquaredNorm2D();
         return new DoubleVector3(
            (other.X * numerator) / denominator,
            (other.Y * numerator) / denominator,
            (other.Z * numerator) / denominator);
      }

      public DoubleVector3 ToUnit() => this / Norm2D();
      public DoubleVector3 ToUnitXY() => this / XY.Norm2D();

      public IntVector3 LossyToIntVector3() => new IntVector3((cInt)CDoubleMath.Floor(X), (cInt)CDoubleMath.Floor(Y), (cInt)CDoubleMath.Floor(Z));

      public DoubleVector3 MinWith(DoubleVector3 o) => new DoubleVector3(CDoubleMath.Min(X, o.X), CDoubleMath.Min(Y, o.Y), CDoubleMath.Min(Z, o.Z));
      public DoubleVector3 MaxWith(DoubleVector3 o) => new DoubleVector3(CDoubleMath.Max(X, o.X), CDoubleMath.Max(Y, o.Y), CDoubleMath.Max(Z, o.Z));

      public static DoubleVector3 Zero => new DoubleVector3(CDoubleMath.c0, CDoubleMath.c0, CDoubleMath.c0);
      public static DoubleVector3 UnitX => new DoubleVector3(CDoubleMath.c1, CDoubleMath.c0, CDoubleMath.c0);
      public static DoubleVector3 UnitY => new DoubleVector3(CDoubleMath.c0, CDoubleMath.c1, CDoubleMath.c0);
      public static DoubleVector3 UnitZ => new DoubleVector3(CDoubleMath.c0, CDoubleMath.c0, CDoubleMath.c1);

      public static DoubleVector3 operator *(int a, DoubleVector3 b) => new DoubleVector3((cDouble)a * b.X, (cDouble)a * b.Y, (cDouble)a * b.Z);
      public static DoubleVector3 operator *(DoubleVector3 a, int b) => new DoubleVector3((cDouble)b * a.X, (cDouble)b * a.Y, (cDouble)b * a.Z);
      public static DoubleVector3 operator *(cDouble a, DoubleVector3 b) => new DoubleVector3(a * b.X, a * b.Y, a * b.Z);
      public static DoubleVector3 operator *(DoubleVector3 a, cDouble b) => new DoubleVector3(b * a.X, b * a.Y, b * a.Z);
      public static DoubleVector3 operator /(DoubleVector3 a, int b) => new DoubleVector3(a.X / (cDouble)b, a.Y / (cDouble)b, a.Z / (cDouble)b);
      public static DoubleVector3 operator /(DoubleVector3 a, cDouble b) => new DoubleVector3(a.X / b, a.Y / b, a.Z / b);
      public static DoubleVector3 operator +(DoubleVector3 a, DoubleVector3 b) => new DoubleVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
      public static DoubleVector3 operator -(DoubleVector3 a, DoubleVector3 b) => new DoubleVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
      public static bool operator ==(DoubleVector3 a, DoubleVector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
      public static bool operator !=(DoubleVector3 a, DoubleVector3 b) => a.X != b.X || a.Y != b.Y || a.Z != b.Z;
      public override bool Equals(object other) => other is DoubleVector3 && Equals((DoubleVector3)other);
      public bool Equals(DoubleVector3 other) => X == other.X && Y == other.Y && Z == other.Z;

      public override int GetHashCode() {
         unchecked {
            var hashCode = X.GetHashCode();
            hashCode = (hashCode * 397) ^ Y.GetHashCode();
            hashCode = (hashCode * 397) ^ Z.GetHashCode();
            return hashCode;
         }
      }

      public static DoubleVector3 FromRadiusAngleAroundZAxis(cDouble radius, cDouble radians) {
         var x = radius * CDoubleMath.Cos(radians);
         var y = radius * CDoubleMath.Sin(radians);
         return new DoubleVector3(x, y, CDoubleMath.c0);
      }

      // rule is rotation as if the axis of rotation is z... so start at x then y
      // (y is x, z is y according to RHR... so at theta = 0, y=r, theta=pi/2, z = r)
      public static DoubleVector3 FromRadiusAngleAroundXAxis(cDouble radius, cDouble radians) {
         var y = radius * CDoubleMath.Cos(radians);
         var z = radius * CDoubleMath.Sin(radians);
         return new DoubleVector3(CDoubleMath.c0, y, z);
      }

      public override string ToString() => $"[{X}, {Y}, {Z}]";
   }

   public struct IntVector3 {
      public cInt X;
      public cInt Y;
      public cInt Z;

      public IntVector3(cInt x, cInt y) : this(x, y, 0) { }

      public IntVector3(IntVector2 v, cInt z = 0) : this(v.X, v.Y, z) { }

      public IntVector3(cInt x, cInt y, cInt z) {
         X = x;
         Y = y;
         Z = z;
      }

      public IntVector2 XY => new IntVector2(X, Y);

      public cInt Dot(IntVector3 other) => X * other.X + Y * other.Y + Z * other.Z;

      public cInt SquaredNorm2() => Dot(this);
      public float Norm2F() => (float)Math.Sqrt(SquaredNorm2());

      public IntVector3 Cross(IntVector3 other) {
         cInt u1 = X, u2 = Y, u3 = Z,
            v1 = other.X, v2 = other.Y, v3 = other.Z;
         return new IntVector3(
            u2 * v3 - u3 * v2,
            u3 * v1 - u1 * v3,
            u1 * v2 - u2 * v1);
      }


      public IntVector3 To(IntVector3 other) => other - this;

      /// <summary>
      /// result * other ~= Proj(this onto other)
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public double ProjectOntoComponentD(IntVector3 other) {
         return other.Dot(this) / (double)other.SquaredNorm2();
      }

      /// <summary>
      /// Projects this vector onto other vector.
      /// </summary>
      /// <param name="other">The vector being projected onto</param>
      /// <returns></returns>
      public IntVector3 LossyProjectOnto(IntVector3 other) {
         var numerator = other.Dot(this);
         var denominator = other.SquaredNorm2();
         return new IntVector3(
            (other.X * numerator) / denominator,
            (other.Y * numerator) / denominator,
            (other.Z * numerator) / denominator);
      }

      public DoubleVector3 ToDoubleVector3() => new DoubleVector3((cDouble)X, (cDouble)Y, (cDouble)Z);

      public static IntVector3 Zero => new IntVector3(0, 0, 0);
      public static IntVector3 UnitX => new IntVector3(1, 0, 0);
      public static IntVector3 UnitY => new IntVector3(0, 1, 0);
      public static IntVector3 UnitZ => new IntVector3(0, 0, 1);

      public static IntVector3 operator *(int a, IntVector3 b) => new IntVector3(a * b.X, a * b.Y, a * b.Z);
      public static IntVector3 operator *(IntVector3 a, int b) => new IntVector3(b * a.X, b * a.Y, b * a.Z);
      public static IntVector3 operator +(IntVector3 a, IntVector3 b) => new IntVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
      public static IntVector3 operator -(IntVector3 a, IntVector3 b) => new IntVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
      public static bool operator ==(IntVector3 a, IntVector3 b) => a.X == b.X && a.Y == b.Y;
      public static bool operator !=(IntVector3 a, IntVector3 b) => a.X != b.X || a.Y != b.Y;
      public override bool Equals(object other) => other is IntVector3 && Equals((IntVector3)other);
      public bool Equals(IntVector3 other) => X == other.X && Y == other.Y && Z == other.Z;

      public override int GetHashCode() {
         unchecked {
            var hashCode = X.GetHashCode();
            hashCode = (hashCode * 397) ^ Y.GetHashCode();
            hashCode = (hashCode * 397) ^ Z.GetHashCode();
            return hashCode;
         }
      }

      public static IntVector3 FromRadiusAngleAroundZAxis(int radius, double radians) {
         var x = (cInt)(radius * Math.Cos(radians));
         var y = (cInt)(radius * Math.Sin(radians));
         return new IntVector3(x, y, 0);
      }

      public override string ToString() => $"[{X}, {Y}, {Z}]";
   }
}

