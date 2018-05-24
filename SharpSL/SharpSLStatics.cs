using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SharpSL {
   public static class SharpSLStatics {
      public const float M_PI = MathF.PI;
      public const float M_TWO_PI = MathF.PI * 2.0f;

      public static Vector2 CMul(Vector2 a, Vector2 b) => new Vector2(a.X * b.X, a.Y * b.Y);

      public static Vector2 Vec2(float v) => new Vector2(v, v);
      public static Vector2 Vec2(float x, float y) => new Vector2(x, y);

      public static Vector3 Vec3(float v) => new Vector3(v, v, v);
      public static Vector3 Vec3(Vector2 v, float z) => new Vector3(v.X, v.Y, z);
      public static Vector3 Vec3(float x, float y, float z) => new Vector3(x, y, z);

      public static Vector4 Vec4(float v) => new Vector4(v, v, v, v);
      public static Vector4 Vec4(Vector2 v, float z, float w) => new Vector4(v.X, v.Y, z, w);
      public static Vector4 Vec4(float x, float y, float z, float w) => new Vector4(x, y, z, w);

      public static Vector3 Normalize(Vector3 v) => v / v.Length();
      public static float Dot(Vector3 v, Vector3 other) => Vector3.Dot(v, other);

      public static float Exp(float x) => MathF.Exp(x);
      public static Vector2 Exp(Vector2 v) => Vec2(Exp(v.X), Exp(v.Y));
      public static Vector3 Exp(Vector3 v) => Vec3(Exp(v.X), Exp(v.Y), Exp(v.Z));

      // m is column major
      public static Vector3 Mul(Matrix4x4 m, Vector3 v) => Vector3.Transform(v, Matrix4x4.Transpose(m));
      public static Vector4 Mul(Matrix4x4 m, Vector4 v) => Vector4.Transform(v, Matrix4x4.Transpose(m));

      public static Vector3 ComputeCameraRayDirection(Vector2 screenQuadUv, Matrix4x4 cameraViewInv) {
         var uv = CMul(screenQuadUv * 2.0f - Vec2(1.0f), Vec2(1.0f, -1.0f));
         var near = Vec3(uv, 0.1f).Project(cameraViewInv);
         var far = Vec3(uv, 1.0f).Project(cameraViewInv);
         return Normalize(far - near);
      }

      public static void Deconstruct(this Vector2 v, out float x, out float y) {
         x = v.X;
         y = v.Y;
      }
   }
   public static class SharpSLExtensions {
      public static Vector3 Normalize(this Vector3 v) => v / v.Length();
      public static Vector4 NormalizeByW(this Vector4 v) => v / v.W;
      public static float Dot(this Vector3 v, Vector3 other) => Vector3.Dot(v, other);

      public static Vector3 Project(this Vector3 v, Matrix4x4 m) => Vector4.Transform(new Vector4(v, 1.0f), Matrix4x4.Transpose(m)).NormalizeByW().XYZ();

      public static Vector3 XYZ(this Vector4 v) => new Vector3(v.X, v.Y, v.Z);
   }
}
