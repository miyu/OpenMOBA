using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FMatrix {
   using static NumericsStatics;

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct FMatrix4x4 {
      public static readonly FMatrix4x4 Identity = new FMatrix4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

      public Vector4 Row1, Row2, Row3, Row4;

      static unsafe FMatrix4x4() {
         if (Marshal.SizeOf<FMatrix4x4>() != sizeof(float) * 16) {
            throw new Exception($"{nameof(FMatrix4x4)} doesn't marshal to 64-byte struct.");
         }
         if (Marshal.SizeOf<FMatrix4x4>() != sizeof(FMatrix4x4)) {
            throw new Exception($"{nameof(FMatrix4x4)} doesn't marshal to same size as sizeof.");
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public FMatrix4x4(Vector4 row1, Vector4 row2, Vector4 row3, Vector4 row4) {
         Row1 = row1;
         Row2 = row2;
         Row3 = row3;
         Row4 = row4;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public FMatrix4x4(
         float m11, float m12, float m13, float m14,
         float m21, float m22, float m23, float m24,
         float m31, float m32, float m33, float m34,
         float m41, float m42, float m43, float m44
      ) : this(
         Vec4(m11, m12, m13, m14),
         Vec4(m21, m22, m23, m24),
         Vec4(m31, m32, m33, m34),
         Vec4(m41, m42, m43, m44)
      ) { }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector4 operator *(FMatrix4x4 m, Vector4 v) {
         return new Vector4(Vector4.Dot(m.Row1, v), Vector4.Dot(m.Row2, v), Vector4.Dot(m.Row3, v), Vector4.Dot(m.Row4, v));
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 operator *(FMatrix4x4 a, FMatrix4x4 b) {
         Vector4 b1 = b.Row1, b2 = b.Row2, b3 = b.Row3, b4 = b.Row4;
         return new FMatrix4x4(
            Vector4.Multiply(new Vector4(a.Row1.X), b1) + Vector4.Multiply(new Vector4(a.Row1.Y), b2) + Vector4.Multiply(new Vector4(a.Row1.Z), b3) + Vector4.Multiply(new Vector4(a.Row1.W), b4),
            Vector4.Multiply(new Vector4(a.Row2.X), b1) + Vector4.Multiply(new Vector4(a.Row2.Y), b2) + Vector4.Multiply(new Vector4(a.Row2.Z), b3) + Vector4.Multiply(new Vector4(a.Row2.W), b4),
            Vector4.Multiply(new Vector4(a.Row3.X), b1) + Vector4.Multiply(new Vector4(a.Row3.Y), b2) + Vector4.Multiply(new Vector4(a.Row3.Z), b3) + Vector4.Multiply(new Vector4(a.Row3.W), b4),
            Vector4.Multiply(new Vector4(a.Row4.X), b1) + Vector4.Multiply(new Vector4(a.Row4.Y), b2) + Vector4.Multiply(new Vector4(a.Row4.Z), b3) + Vector4.Multiply(new Vector4(a.Row4.W), b4)
         );
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 operator +(FMatrix4x4 a, FMatrix4x4 b) {
         return new FMatrix4x4(a.Row1 + b.Row1, a.Row2 + b.Row2, a.Row3 + b.Row3, a.Row4 + b.Row4);
      }

      public override string ToString() => $"{{ {{M11:{Row1.X} M12:{Row1.Y} M13:{Row1.Z} M14:{Row1.W}}} {{M21:{Row2.X} M22:{Row2.Y} M23:{Row2.Z} M24:{Row2.W}}} {{M31:{Row3.X} M32:{Row3.Y} M33:{Row3.Z} M34:{Row3.W}}} {{M41:{Row4.X} M42:{Row4.Y} M43:{Row4.Z} M44:{Row4.W}}} }}";

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 RotationX(float theta) {
         var s = MathF.Sin(theta);
         var c = MathF.Cos(theta);
         return new FMatrix4x4(
            Vector4.UnitX, 
            Vec4(0, c, -s, 0),
            Vec4(0, s, c, 0),
            Vector4.UnitW);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 RotationY(float theta) {
         var s = MathF.Sin(theta);
         var c = MathF.Cos(theta);
         return new FMatrix4x4(
            Vec4(c, 0, s, 0),
            Vector4.UnitY,
            Vec4(-s, 0, c, 0),
            Vector4.UnitW);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 RotationZ(float theta) {
         var s = MathF.Sin(theta);
         var c = MathF.Cos(theta);
         return new FMatrix4x4(
            Vec4(c, -s, 0, 0),
            Vec4(s, c, 0, 0),
            Vector4.UnitZ,
            Vector4.UnitW);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 Translation(float x, float y, float z) {
         return new FMatrix4x4(
            Vec4(1, 0, 0, x),
            Vec4(0, 1, 0, y),
            Vec4(0, 0, 1, z),
            Vector4.UnitW);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 Translation(Vector3 v) => Translation(v.X, v.Y, v.Z);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 Scale(float x, float y, float z) {
         return new FMatrix4x4(
            Vec4(x, 0, 0, 0),
            Vec4(0, y, 0, 0),
            Vec4(0, 0, z, 0),
            Vector4.UnitW);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 Scale(Vector3 v) => Scale(v.X, v.Y, v.Z);

      public static FMatrix4x4 FromAxisAngle(Vector3 axis, float theta) {
         var s = MathF.Sin(theta);
         var c = MathF.Cos(theta);
         var oc = 1.0f - c;
         float ux = axis.X, uy = axis.Y, uz = axis.Z;
         return new FMatrix4x4(
            c + ux * ux * oc, ux * uy * oc - uz * s, ux * uz * oc + uy * s, 0,
            uy * ux * oc + uz * s, c + uy * uy * oc, uy * uz * oc - ux * s, 0,
            uz * ux * oc - uy * s, uz * uy * oc + ux * s, c + uz * uz * oc, 0,
            0, 0, 0, 1);
      }

      public static FMatrix4x4 LookAtRH(Vector3 eye, Vector3 target, Vector3 up) {
         var z = (eye - target).Normalize();
         var x = Cross(up, z).Normalize();
         var y = Cross(z, x);
         return new FMatrix4x4(
            Vec4(x, -Dot(x, eye)),
            Vec4(y, -Dot(y, eye)),
            Vec4(z, -Dot(z, eye)),
            Vector4.UnitW);
      }
   }
}