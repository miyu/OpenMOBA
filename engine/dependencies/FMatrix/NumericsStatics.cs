using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace FMatrix {
   public static class NumericsStatics {
      public const float M_PI = MathF.PI;
      public const float M_TWO_PI = MathF.PI * 2.0f;

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector2 CMul(Vector2 a, Vector2 b) => new Vector2(a.X * b.X, a.Y * b.Y);

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector2 Vec2(float v) => new Vector2(v, v);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector2 Vec2(float x, float y) => new Vector2(x, y);

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Vec3(float v) => new Vector3(v, v, v);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Vec3(Vector2 v, float z) => new Vector3(v.X, v.Y, z);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Vec3(float x, float y, float z) => new Vector3(x, y, z);

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector4 Vec4(float v) => new Vector4(v, v, v, v);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector4 Vec4(Vector2 v, float z, float w) => new Vector4(v.X, v.Y, z, w);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector4 Vec4(Vector3 v, float w) => new Vector4(v.X, v.Y, v.Z, w);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector4 Vec4(float x, float y, float z, float w) => new Vector4(x, y, z, w);

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Normalize(Vector3 v) => v / v.Length();
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Dot(Vector3 v, Vector3 other) => Vector3.Dot(v, other);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Cross(Vector3 a, Vector3 b) => Vector3.Cross(a, b);

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Exp(float x) => MathF.Exp(x);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector2 Exp(Vector2 v) => Vec2(Exp(v.X), Exp(v.Y));
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Exp(Vector3 v) => Vec3(Exp(v.X), Exp(v.Y), Exp(v.Z));
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Pow(Vector3 v, Vector3 pow) => Vec3(MathF.Pow(v.X, pow.X), MathF.Pow(v.Y, pow.Y), MathF.Pow(v.Z, pow.Z));

      // m is column major
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector4 Mul(Matrix4x4 m, Vector4 v) => Vector4.Transform(v, Matrix4x4.Transpose(m));

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector4 Mul(FMatrix4x4 m, Vector4 v) => m * v;

      public static Vector3 ComputeCameraRayDirection(Vector2 screenQuadUv, FMatrix4x4 cameraViewInv) {
         var uv = CMul(screenQuadUv * 2.0f - Vec2(1.0f), Vec2(1.0f, -1.0f));
         var near = (cameraViewInv * Vec4(uv, 0.1f, 1.0f)).NormalizeByW().XYZ();
         var far = (cameraViewInv * Vec4(uv, 1.0f, 1.0f)).NormalizeByW().XYZ();
         return Normalize(far - near);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Deconstruct(this Vector2 v, out float x, out float y) {
         x = v.X;
         y = v.Y;
      }
   }

   public static class NumericsExtensions {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Normalize(this Vector3 v) => v * (1.0f / v.Length());
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector4 NormalizeByW(this Vector4 v) => v * (1.0f / v.W);
      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Dot(this Vector3 v, Vector3 other) => Vector3.Dot(v, other);

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Project(this Vector3 v, Matrix4x4 m) => Vector4.Transform(new Vector4(v, 1.0f), Matrix4x4.Transpose(m)).NormalizeByW().XYZ();

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 XYZ(this Vector4 v) => new Vector3(v.X, v.Y, v.Z);

      [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Quaternion Normalize(this Quaternion q) => Quaternion.Normalize(q);
   }
}
