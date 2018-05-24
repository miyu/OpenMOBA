using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using static System.Numerics.Vector4;

namespace MatrixMultiplicationPerformanceBenchmark {
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
         new Vector4(m11, m12, m13, m14),
         new Vector4(m21, m22, m23, m24),
         new Vector4(m31, m32, m33, m34),
         new Vector4(m41, m42, m43, m44)
      ) { }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector4 operator *(FMatrix4x4 m, Vector4 v) {
         return new Vector4(Dot(m.Row1, v), Dot(m.Row2, v), Dot(m.Row3, v), Dot(m.Row4, v));
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 operator *(FMatrix4x4 a, FMatrix4x4 b) {
         Vector4 b1 = b.Row1, b2 = b.Row2, b3 = b.Row3, b4 = b.Row4;
         return new FMatrix4x4(
            Multiply(new Vector4(a.Row1.X), b1) + Multiply(new Vector4(a.Row1.Y), b2) + Multiply(new Vector4(a.Row1.Z), b3) + Multiply(new Vector4(a.Row1.W), b4),
            Multiply(new Vector4(a.Row2.X), b1) + Multiply(new Vector4(a.Row2.Y), b2) + Multiply(new Vector4(a.Row2.Z), b3) + Multiply(new Vector4(a.Row2.W), b4),
            Multiply(new Vector4(a.Row3.X), b1) + Multiply(new Vector4(a.Row3.Y), b2) + Multiply(new Vector4(a.Row3.Z), b3) + Multiply(new Vector4(a.Row3.W), b4),
            Multiply(new Vector4(a.Row4.X), b1) + Multiply(new Vector4(a.Row4.Y), b2) + Multiply(new Vector4(a.Row4.Z), b3) + Multiply(new Vector4(a.Row4.W), b4)
         );
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static FMatrix4x4 operator +(FMatrix4x4 a, FMatrix4x4 b) {
         return new FMatrix4x4(a.Row1 + b.Row1, a.Row2 + b.Row2, a.Row3 + b.Row3, a.Row4 + b.Row4);
      }

      public override string ToString() => $"{{ {{M11:{Row1.X} M12:{Row1.Y} M13:{Row1.Z} M14:{Row1.W}}} {{M21:{Row2.X} M22:{Row2.Y} M23:{Row2.Z} M24:{Row2.W}}} {{M31:{Row3.X} M32:{Row3.Y} M33:{Row3.Z} M34:{Row3.W}}} {{M41:{Row4.X} M42:{Row4.Y} M43:{Row4.Z} M44:{Row4.W}}} }}";
   }
}