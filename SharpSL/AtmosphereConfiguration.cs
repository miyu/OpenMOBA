using System.Numerics;

namespace SharpSL {
   public struct AtmosphereConfiguration {
      // Note: This is probably NOT the world position of the actual camera!
      // This is the camera position used to compute the atmospheric scattering
      // effect.
      public Vector3 CameraPosition;

      public Vector3 SunDirectionUnit;
      public float SunIntensity;

      public float PlanetRadius;
      public float AtmosphereRadius;

      public Vector3 RayleighScatteringCoefficient;
      public float MieScatteringCoefficient;

      public float RayleighScaleHeight;
      public float MieScaleHeight;

      public float MiePreferredScatteringDirection; // "g"

      public static readonly AtmosphereConfiguration Earth = new AtmosphereConfiguration {
         CameraPosition = SharpSLStatics.Vec3(0.0f, 6372e3f, 0.0f),
         SunDirectionUnit = SharpSLStatics.Vec3(0.0f, 1.0f, 0.0f),
         SunIntensity = 22.0f,
         PlanetRadius = 6371e3f,
         AtmosphereRadius = 6471e3f,
         RayleighScatteringCoefficient = SharpSLStatics.Vec3(5.5e-6f, 13.0e-6f, 22.4e-6f),
         MieScatteringCoefficient = 21e-6f,
         RayleighScaleHeight = 32e3f,
         MieScaleHeight = 1.2e3f,
         MiePreferredScatteringDirection = 0.758f
      };
   }
}