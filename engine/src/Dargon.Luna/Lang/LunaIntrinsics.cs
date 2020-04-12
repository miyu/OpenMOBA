using System;
using Dargon.Luna.Demos;
using static Dargon.Luna.Lang.LunaIntrinsics;

namespace Dargon.Luna.Lang {
   public static class LunaIntrinsics {
      [LunaIntrinsic] public static float dot(float3 a, float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
      [LunaIntrinsic] public static float3 normalize(float3 x) => x / length(x);
      [LunaIntrinsic] public static float4 normalize(float4 x) => x / length(x);
      [LunaIntrinsic] public static float length(float3 x) => sqrt(x.x * x.x + x.y * x.y + x.z * x.z);
      [LunaIntrinsic] public static float length(float4 x) => sqrt(x.x * x.x + x.y * x.y + x.z * x.z + x.w * x.w);
      [LunaIntrinsic] public static float4 float4(float3 xyz, float w) => new float4(xyz.x, xyz.y, xyz.z, w);

      [LunaIntrinsic] public static float4 mul(this float4x4 a, float4 b) => default;
      [LunaIntrinsic] public static float sqrt(float x) => (float)Math.Sqrt(x);
   }

   public abstract class Shader {
      [ConstantBuffer] public SceneBuffer Scene;
      [ConstantBuffer] public LightingBuffer Lighting;
      [ConstantBuffer] public CameraBuffer Camera;
      [ConstantBuffer] public TransformBuffer Transform;

      public float4 ObjectToClipPosition(float4 v) => Camera.MAT_PV * (Transform.MAT_W * v);
      public float4 ObjectToWorldNormal(float4 v) => Transform.MAT_W * float4(v.xyz, 0);
   }

   /// <summary>
   /// Transpiler-internal method.
   /// Either implemented by shader language or lowered to another operation.
   /// </summary>
   public class LunaIntrinsicAttribute : Attribute { }

   public class ConstantBufferAttribute : Attribute {
      public ConstantBufferAttribute(string name = null) {
         Name = name;
      }

      public string Name { get; set; }
   }

   [ConstantBuffer("Scene")]
   public struct SceneBuffer {
      public float4 Time;
   }

   [ConstantBuffer("Lighting")]
   public struct LightingBuffer {
      public DirectionalLight[] Directional;
   }

   [ConstantBuffer("Camera")]
   public struct CameraBuffer {
      public float4x4 MAT_V, MAT_P, MAT_PV;
   }

   [ConstantBuffer("Transform")]
   public struct TransformBuffer {
      public float4x4 MAT_W;
   }

   public class POSITIONAttribute : SlotAttribute {
      public POSITIONAttribute(int slot = 0) : base(slot) { }
   }

   public class NORMALAttribute : SlotAttribute {
      public NORMALAttribute(int slot = 0) : base(slot) { }
   }

   public class TEXCOORDAttribute : SlotAttribute {
      public TEXCOORDAttribute(int slot = 0) : base(slot) { }
   }

   public class COLORAttribute : SlotAttribute {
      public COLORAttribute(int slot = 0) : base(slot) { }
   }

   public class SlotAttribute : Attribute {
      public SlotAttribute(int slot = 0) { }
   }

   public class SV_POSITIONAttribute : Attribute { }

   public struct float4x4 {
      public static float4 operator *(float4x4 m, float4 other) => default;
   }

   public struct float3 {
      public float x, y, z;

      public float3(float x, float y, float z) {
         (this.x, this.y, this.z) = (x, y, z);
      }

      public float length() => LunaIntrinsics.length(this);
      public float3 normalize() => this / length();

      [LunaIntrinsic] public static float3 operator *(float a, float3 b) => default;
      [LunaIntrinsic] public static float3 operator *(float3 a, float b) => default;
      [LunaIntrinsic] public static float3 operator /(float a, float3 b) => default;
      [LunaIntrinsic] public static float3 operator /(float3 a, float b) => default;

      [LunaIntrinsic] public static float3 operator +(float3 a, float3 b) => default;
      [LunaIntrinsic] public static float3 operator -(float3 a, float3 b) => default;
      [LunaIntrinsic] public static float3 operator *(float3 a, float3 b) => default;
   }

   public struct float4 {
      public float4(float x, float y, float z, float w) {
         this.x = x;
         this.y = y;
         this.z = z;
         this.w = w;
      }

      public float x, y, z, w;
      public float3 xyz => new float3(x, y, z);

      public float length() => LunaIntrinsics.length(this);
      public float4 normalize() => this / length();

      public static float4 operator *(float a, float4 b) => default;
      public static float4 operator *(float4 a, float b) => default;
      public static float4 operator /(float a, float4 b) => default;
      public static float4 operator /(float4 a, float b) => default;

      public static float4 operator +(float4 a, float4 b) => default;
      public static float4 operator -(float4 a, float4 b) => default;
      public static float4 operator *(float4 a, float4 b) => default;
   }
}