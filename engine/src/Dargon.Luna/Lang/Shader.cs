using System;
using Dargon.Luna.Demos;

namespace Dargon.Luna.Lang {
   public abstract class Shader {
      public SceneBuffer Scene;
      public LightingBuffer Lighting;
      public CameraBuffer Camera;
      public InstanceBuffer Instance;

      public float4 ObjectToClipPosition(float4 v) => Camera.MAT_PV * (Instance.MAT_W * v);
      public float4 ObjectToWorldNormal(float4 v) => Instance.MAT_W * float4(v.xyz, 0);

      [LunaIntrinsic] public float dot(float3 a, float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
      [LunaIntrinsic] public float3 normalize(float3 x) => x / length(x);
      [LunaIntrinsic] public float length(float3 x) => sqrt(x.x * x.x + x.y * x.y + x.z * x.z);
      [LunaIntrinsic] public float4 float4(float3 xyz, float w) => new float4(xyz.x, xyz.y, xyz.z, w);

      [LunaIntrinsic] public float4 mul(float4x4 a, float4 b) => default;
      [LunaIntrinsic] public float sqrt(float x) => (float)Math.Sqrt(x);
   }

   /// <summary>
   /// Transpiler-internal method.
   /// Either implemented by shader language or lowered to another operation.
   /// </summary>
   public class LunaIntrinsicAttribute : Attribute { }

   public struct SceneBuffer {
      public float4 Time;
   }

   public struct LightingBuffer {
      public DirectionalLight[] Directional;
   }

   public struct CameraBuffer {
      public float4x4 MAT_V, MAT_P, MAT_PV;
   }

   public struct InstanceBuffer {
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

      public static float3 operator *(float a, float3 b) => default;
      public static float3 operator *(float3 a, float b) => default;
      public static float3 operator /(float a, float3 b) => default;
      public static float3 operator /(float3 a, float b) => default;

      public static float3 operator +(float3 a, float3 b) => default;
      public static float3 operator -(float3 a, float3 b) => default;
      public static float3 operator *(float3 a, float3 b) => default;
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

      public static float4 operator +(float4 a, float4 b) => default;
      public static float4 operator -(float4 a, float4 b) => default;
      public static float4 operator *(float4 a, float4 b) => default;
      public static float4 operator /(float4 a, float4 b) => default;
   }
}