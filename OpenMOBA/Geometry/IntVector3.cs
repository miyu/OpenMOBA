using System;

using cInt = System.Int64;

namespace OpenMOBA.Geometry {
   public struct DoubleVector3 {
      public double X;
      public double Y;
      public double Z;

      public DoubleVector3(double x, double y, double z) {
         X = x;
         Y = y;
         Z = z;
      }

      public DoubleVector2 XY => new DoubleVector2(X, Y);

      public double Dot(DoubleVector3 other) => X * other.X + Y * other.Y + Z * other.Z;

      public double SquaredNorm2D() => Dot(this);
      public double Norm2D() => Math.Sqrt(SquaredNorm2D());

      public DoubleVector3 Cross(DoubleVector3 other) {
         double u1 = X, u2 = Y, u3 = Z,
                v1 = other.X, v2 = other.Y, v3 = other.Z;
         return new DoubleVector3(
            u2 * v3 - u3 * v2, 
            u3 * v1 - u1 * v3, 
            u1 * v2 - u2 * v1);
      }

      public DoubleVector3 To(DoubleVector3 other) => other - this;

      /// <summary>
      /// result * other ~= Proj(this onto other)
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public double ProjectOntoComponentD(DoubleVector3 other) {
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

      public IntVector3 LossyToIntVector3() => new IntVector3((cInt)Math.Floor(X), (cInt)Math.Floor(Y), (cInt)Math.Floor(Z));

      public static DoubleVector3 Zero => new DoubleVector3(0, 0, 0);
      public static DoubleVector3 UnitX => new DoubleVector3(1, 0, 0);
      public static DoubleVector3 UnitY => new DoubleVector3(0, 1, 0);
      public static DoubleVector3 UnitZ => new DoubleVector3(0, 0, 1);

      public static DoubleVector3 operator *(int a, DoubleVector3 b) => new DoubleVector3(a * b.X, a * b.Y, a * b.Z);
      public static DoubleVector3 operator *(DoubleVector3 a, int b) => new DoubleVector3(b * a.X, b * a.Y, b * a.Z);
      public static DoubleVector3 operator *(double a, DoubleVector3 b) => new DoubleVector3(a * b.X, a * b.Y, a * b.Z);
      public static DoubleVector3 operator *(DoubleVector3 a, double b) => new DoubleVector3(b * a.X, b * a.Y, b * a.Z);
      public static DoubleVector3 operator /(DoubleVector3 a, int b) => new DoubleVector3(a.X / b, a.Y / b, a.Z / b);
      public static DoubleVector3 operator /(DoubleVector3 a, double b) => new DoubleVector3(a.X / b, a.Y / b, a.Z / b);
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

      public static DoubleVector3 FromRadiusAngleAroundZAxis(int radius, double radians) {
         var x = radius * Math.Cos(radians);
         var y = radius * Math.Sin(radians);
         return new DoubleVector3(x, y, 0);
      }

      public override string ToString() => $"[{X}, {Y}, {Z}]";
   }

   public struct IntVector3 {
      public cInt X;
      public cInt Y;
      public cInt Z;

      public IntVector3(cInt x, cInt y) : this(x, y, 0) { }

      public IntVector3(cInt x, cInt y, cInt z) {
         X = x;
         Y = y;
         Z = z;
      }

      public IntVector2 XY => new IntVector2(X, Y);

      public cInt Dot(IntVector3 other) => X * other.X + Y * other.Y + Z * other.Z;

      public cInt SquaredNorm2() => Dot(this);
      public float Norm2F() => (float)Math.Sqrt(SquaredNorm2());

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

      public DoubleVector3 ToDoubleVector3() => new DoubleVector3(X, Y, Z);

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

