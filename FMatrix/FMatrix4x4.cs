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

      #region Mrc
      public float M11 {
         get => Row1.X;
         set => Row1.X = value;
      }

      public float M12 {
         get => Row1.Y;
         set => Row1.Y = value;
      }

      public float M13 {
         get => Row1.Z;
         set => Row1.Z = value;
      }

      public float M14 {
         get => Row1.W;
         set => Row1.W = value;
      }

      public float M21 {
         get => Row2.X;
         set => Row2.X = value;
      }

      public float M22 {
         get => Row2.Y;
         set => Row2.Y = value;
      }

      public float M23 {
         get => Row2.Z;
         set => Row2.Z = value;
      }

      public float M24 {
         get => Row2.W;
         set => Row2.W = value;
      }
      
      public float M31 {
         get => Row3.X;
         set => Row3.X = value;
      }

      public float M32 {
         get => Row3.Y;
         set => Row3.Y = value;
      }

      public float M33 {
         get => Row3.Z;
         set => Row3.Z = value;
      }

      public float M34 {
         get => Row3.W;
         set => Row3.W = value;
      }
      
      public float M41 {
         get => Row4.X;
         set => Row4.X = value;
      }

      public float M42 {
         get => Row4.Y;
         set => Row4.Y = value;
      }

      public float M43 {
         get => Row4.Z;
         set => Row4.Z = value;
      }

      public float M44 {
         get => Row4.W;
         set => Row4.W = value;
      }
      #endregion

      public FMatrix4x4 Transpose() {
         return new FMatrix4x4(
            new Vector4(Row1.X, Row2.X, Row3.X, Row4.X),
            new Vector4(Row1.Y, Row2.Y, Row3.Y, Row4.Y),
            new Vector4(Row1.Z, Row2.Z, Row3.Z, Row4.Z),
            new Vector4(Row1.W, Row2.W, Row3.W, Row4.W));
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public Vector3 Transform(Vector3 v) => (this * Vec4(v, 1)).XYZ();

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public Vector3 TransformNormal(Vector3 v) => (this * Vec4(v, 0)).XYZ();

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public FMatrix4x4 InvertOrThrow() => TryInvert(this, out var res) ? res : throw new MatrixNotInvertibleException(this);

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
      public string ToStringNewline() => $"{{ {{M11:{Row1.X} M12:{Row1.Y} M13:{Row1.Z} M14:{Row1.W}}}\r\n  {{M21:{Row2.X} M22:{Row2.Y} M23:{Row2.Z} M24:{Row2.W}}}\r\n  {{M31:{Row3.X} M32:{Row3.Y} M33:{Row3.Z} M34:{Row3.W}}}\r\n  {{M41:{Row4.X} M42:{Row4.Y} M43:{Row4.Z} M44:{Row4.W}}} }}";

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

      public static FMatrix4x4 ViewLookAtRH(Vector3 eye, Vector3 target, Vector3 up) {
         var z = (eye - target).Normalize();
         var x = Cross(up, z).Normalize();
         var y = Cross(z, x);

         return new FMatrix4x4(
            Vec4(x, -Dot(x, eye)),
            Vec4(y, -Dot(y, eye)),
            Vec4(z, -Dot(z, eye)),
            Vector4.UnitW);
      }

      public static FMatrix4x4 RotationLookAtRH(Vector3 eye, Vector3 target, Vector3 up) => RotationLookAtRH(target - eye, up);

      public static FMatrix4x4 RotationLookAtRH(Vector3 eyeToTarget, Vector3 up) {
         var z = eyeToTarget.Normalize();
         var x = Cross(up, z).Normalize();
         var y = Cross(z, x);
         return new FMatrix4x4(
            //Vec4(x, 0),
            //Vec4(y, 0),
            //Vec4(z, 0),
            Vec4(x.X, y.X, z.X, 0),
            Vec4(x.Y, y.Y, z.Y, 0),
            Vec4(x.Z, y.Z, z.Z, 0),
            Vector4.UnitW);
      }

      // See https://msdn.microsoft.com/en-us/library/windows/desktop/bb281728(v=vs.85).aspx
      public static FMatrix4x4 PerspectiveFovRH(float fov, float aspect, float znear, float zfar) {
         float h = 1.0f / MathF.Tan(fov * 0.5f);
         float zFarOverZSpan = zfar / (znear - zfar);
         return new FMatrix4x4(
            Vec4(h / aspect, 0, 0, 0),
            Vec4(0, h, 0, 0),
            Vec4(0, 0, zFarOverZSpan, znear * zFarOverZSpan),
            Vec4(0, 0, -1, 0));
      }

      // https://msdn.microsoft.com/en-us/library/windows/desktop/bb281724(v=vs.85).aspx
      [Obsolete]
      public static FMatrix4x4 OrthoOffCenterLH(float left, float right, float bottom, float top, float znear, float zfar) {
         float invZSpan = 1.0f / (zfar - znear);
         return new FMatrix4x4(
            Vec4(2.0f / (right - left), 0, 0, (left + right) / (left - right)),
            Vec4(0, 2.0f / (top - bottom), 0, (top + bottom) / (bottom - top)),
            Vec4(0, 0, invZSpan, -znear * invZSpan),
            Vec4(0, 0, 0, 1));
      }

      // https://msdn.microsoft.com/en-us/library/windows/desktop/bb281725(v=vs.85).aspx
      public static FMatrix4x4 OrthoOffCenterRH(float left, float right, float bottom, float top, float znear, float zfar) {
         float invZSpan = 1.0f / (zfar - znear);
         return new FMatrix4x4(
            Vec4(2.0f / (right - left), 0, 0, (left + right) / (left - right)),
            Vec4(0, 2.0f / (top - bottom), 0, (top + bottom) / (bottom - top)),
            Vec4(0, 0, -invZSpan, -znear * invZSpan),
            Vec4(0, 0, 0, 1));
      }

      // https://msdn.microsoft.com/en-us/library/windows/desktop/bb281723(v=vs.85).aspx
      [Obsolete]
      public static FMatrix4x4 OrthoLH(float width, float height, float znear, float zfar) {
         float invZSpan = 1.0f / (zfar - znear);
         return new FMatrix4x4(
            Vec4(2.0f / width, 0, 0, 0),
            Vec4(0, 2.0f / height, 0, 0),
            Vec4(0, 0, invZSpan, -znear * invZSpan),
            Vec4(0, 0, 0, 1));
      }

      // https://msdn.microsoft.com/en-us/library/windows/desktop/bb281726(v=vs.85).aspx
      public static FMatrix4x4 OrthoRH(float width, float height, float znear, float zfar) {
         float invZSpan = 1.0f / (zfar - znear);
         return new FMatrix4x4(
            Vec4(2.0f / width, 0, 0, 0),
            Vec4(0, 2.0f / height, 0, 0),
            Vec4(0, 0, -invZSpan, -znear * invZSpan),
            Vec4(0, 0, 0, 1));
      }


      // Via https://github.com/dotnet/corefx/blob/ca6babddf64e479d7a9ffa08bc279d42dac76494/src/System.Numerics.Vectors/src/System/Numerics/Matrix4x4.cs
      // Which is MIT Licensed - https://github.com/dotnet/corefx
      // This is vectorizable https://lxjk.github.io/2017/09/03/Fast-4x4-Matrix-Inverse-with-SSE-SIMD-Explained.html
      // However, we'll need .NET to support vec shuffles, which is being tracked in 
      //     https://github.com/dotnet/coreclr/issues/4356
      //     https://github.com/dotnet/corefx/issues/1168
      public static bool TryInvert(FMatrix4x4 matrix, out FMatrix4x4 result) {
         float a = matrix.M11, b = matrix.M12, c = matrix.M13, d = matrix.M14;
         float e = matrix.M21, f = matrix.M22, g = matrix.M23, h = matrix.M24;
         float i = matrix.M31, j = matrix.M32, k = matrix.M33, l = matrix.M34;
         float m = matrix.M41, n = matrix.M42, o = matrix.M43, p = matrix.M44;

         float kp_lo = k * p - l * o;
         float jp_ln = j * p - l * n;
         float jo_kn = j * o - k * n;
         float ip_lm = i * p - l * m;
         float io_km = i * o - k * m;
         float in_jm = i * n - j * m;

         float a11 = +(f * kp_lo - g * jp_ln + h * jo_kn);
         float a12 = -(e * kp_lo - g * ip_lm + h * io_km);
         float a13 = +(e * jp_ln - f * ip_lm + h * in_jm);
         float a14 = -(e * jo_kn - f * io_km + g * in_jm);

         float det = a * a11 + b * a12 + c * a13 + d * a14;

         if (MathF.Abs(det) < float.Epsilon) {
            result = new FMatrix4x4(float.NaN, float.NaN, float.NaN, float.NaN,
                                    float.NaN, float.NaN, float.NaN, float.NaN,
                                    float.NaN, float.NaN, float.NaN, float.NaN,
                                    float.NaN, float.NaN, float.NaN, float.NaN);
            return false;
         }

         float invDet = 1.0f / det;

         float gp_ho = g * p - h * o;
         float fp_hn = f * p - h * n;
         float fo_gn = f * o - g * n;
         float ep_hm = e * p - h * m;
         float eo_gm = e * o - g * m;
         float en_fm = e * n - f * m;

         float gl_hk = g * l - h * k;
         float fl_hj = f * l - h * j;
         float fk_gj = f * k - g * j;
         float el_hi = e * l - h * i;
         float ek_gi = e * k - g * i;
         float ej_fi = e * j - f * i;

         result = new FMatrix4x4(
            Vec4(a11 * invDet, -(b * kp_lo - c * jp_ln + d * jo_kn) * invDet, +(b * gp_ho - c * fp_hn + d * fo_gn) * invDet, -(b * gl_hk - c * fl_hj + d * fk_gj) * invDet),
            Vec4(a12 * invDet, +(a * kp_lo - c * ip_lm + d * io_km) * invDet, -(a * gp_ho - c * ep_hm + d * eo_gm) * invDet, +(a * gl_hk - c * el_hi + d * ek_gi) * invDet),
            Vec4(a13 * invDet, -(a * jp_ln - b * ip_lm + d * in_jm) * invDet, +(a * fp_hn - b * ep_hm + d * en_fm) * invDet, -(a * fl_hj - b * el_hi + d * ej_fi) * invDet),
            Vec4(a14 * invDet, +(a * jo_kn - b * io_km + c * in_jm) * invDet, -(a * fo_gn - b * eo_gm + c * en_fm) * invDet, +(a * fk_gj - b * ek_gi + c * ej_fi) * invDet)
            );
         return true;
      }


      // Via https://github.com/dotnet/corefx/blob/ca6babddf64e479d7a9ffa08bc279d42dac76494/src/System.Numerics.Vectors/src/System/Numerics/Matrix4x4.cs
      // Which is MIT Licensed - https://github.com/dotnet/corefx
      // This is vectorizable https://lxjk.github.io/2017/09/03/Fast-4x4-Matrix-Inverse-with-SSE-SIMD-Explained.html
      // However, we'll need .NET to support vec shuffles, which is being tracked in 
      //     https://github.com/dotnet/coreclr/issues/4356
      //     https://github.com/dotnet/corefx/issues/1168
      public static FMatrix4x4 FromQuaternion(Quaternion quaternion) {
         float xx = quaternion.X * quaternion.X;
         float yy = quaternion.Y * quaternion.Y;
         float zz = quaternion.Z * quaternion.Z;

         float xy = quaternion.X * quaternion.Y;
         float wz = quaternion.Z * quaternion.W;
         float xz = quaternion.Z * quaternion.X;
         float wy = quaternion.Y * quaternion.W;
         float yz = quaternion.Y * quaternion.Z;
         float wx = quaternion.X * quaternion.W;

         return new FMatrix4x4(
            Vec4(1.0f - 2.0f * (yy + zz), 2.0f * (xy - wz), 2.0f * (xz + wy), 0.0f),
            Vec4(2.0f * (xy + wz), 1.0f - 2.0f * (zz + xx), 2.0f * (yz - wx), 0.0f),
            Vec4(2.0f * (xz - wy), 2.0f * (yz + wx), 1.0f - 2.0f * (yy + xx), 0.0f),
            Vec4(0.0f, 0.0f, 0.0f, 1.0f));
      }
   }

   public class MatrixNotInvertibleException : Exception {
      public MatrixNotInvertibleException(FMatrix4x4 m) : base("The matrix was not invertible!: " + m) { }
   }
}