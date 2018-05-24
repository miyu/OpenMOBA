using System;
using System.Numerics;
using static SharpSL.SharpSLStatics;

namespace SharpSL {
   public static class AtmosphericScatteringNaive {
      public static Vector3 Compute(Vector3 queryDirectionUnit, AtmosphereConfiguration c) {
         // Calculate the step size of the primary ray.
         var (tlow, thigh) = Geometry.TryFindRayAndCenteredSphereIntersection(c.CameraPosition, queryDirectionUnit, c.AtmosphereRadius);
         if (tlow > thigh) return Vector3.Zero;
         thigh = MathF.Min(thigh, Geometry.TryFindRayAndCenteredSphereIntersection(c.CameraPosition, queryDirectionUnit, c.PlanetRadius).tlow);
         const int iSteps = 3;
         var iStepSize = (thigh - tlow) / (float)(iSteps);

         // Initialize the primary ray time.
         var iTime = 0.0f;

         // Initialize accumulators for Rayleigh and Mie scattering.
         var totalRlh = Vector3.Zero;
         var totalMie = Vector3.Zero;

         // Initialize optical depth accumulators for the primary ray.
         var iOdRlh = 0.0f;
         var iOdMie = 0.0f;

         // Calculate the Rayleigh and Mie phases.
         var mu = Dot(queryDirectionUnit, c.SunDirectionUnit);
         var mumu = mu * mu;
         var gg = c.MiePreferredScatteringDirection * c.MiePreferredScatteringDirection;
         var pRlh = 3.0f / (16.0f * M_PI) * (1.0f + mumu);
         var pMie = 3.0f / (8.0f * M_PI) * ((1.0f - gg) * (mumu + 1.0f)) / (MathF.Pow(1.0f + gg - 2.0f * mu * c.MiePreferredScatteringDirection, 1.5f) * (2.0f + gg));

         // Sample the primary ray.
         for (int i = 0; i < iSteps; i++) {
            // Calculate the primary ray sample position.
            var iPos = c.CameraPosition + queryDirectionUnit * (iTime + iStepSize * 0.5f);

            // Calculate the height of the sample.
            var iHeight = iPos.Length() - c.PlanetRadius;

            // Calculate the optical depth of the Rayleigh and Mie scattering for this step.
            var odStepRlh = Exp(-iHeight / c.RayleighScaleHeight) * iStepSize;
            var odStepMie = Exp(-iHeight / c.MieScaleHeight) * iStepSize;

            // Accumulate optical depth.
            iOdRlh += odStepRlh;
            iOdMie += odStepMie;

            // Calculate the step size of the secondary ray.
            const int jSteps = 3;
            var jStepSize = Geometry.TryFindRayAndCenteredSphereIntersection(iPos, c.SunDirectionUnit, c.AtmosphereRadius).thigh / jSteps;

            // Initialize the secondary ray time.
            var jTime = 0.0f;

            // Initialize optical depth accumulators for the secondary ray.
            var jOdRlh = 0.0f;
            var jOdMie = 0.0f;

            // Sample the secondary ray.
            for (int j = 0; j < jSteps; j++) {
               // Calculate the secondary ray sample position.
               var jPos = iPos + c.SunDirectionUnit * (jTime + jStepSize * 0.5f);

               // Calculate the height of the sample.
               float jHeight = jPos.Length() - c.PlanetRadius;

               // Accumulate the optical depth.
               jOdRlh += Exp(-jHeight / c.RayleighScaleHeight) * jStepSize;
               jOdMie += Exp(-jHeight / c.MieScaleHeight) * jStepSize;

               // Increment the secondary ray time.
               jTime += jStepSize;
            }

            // Calculate attenuation.
            var attn = Exp(-(Vec3(c.MieScatteringCoefficient) * (iOdMie + jOdMie) + c.RayleighScatteringCoefficient * (iOdRlh + jOdRlh)));

            // Accumulate scattering.
            totalRlh += odStepRlh * attn;
            totalMie += odStepMie * attn;

            // Increment the primary ray time.
            iTime += iStepSize;
         }

         // Calculate and return the final color.
         return c.SunIntensity * (pRlh * c.RayleighScatteringCoefficient * totalRlh + pMie * c.MieScatteringCoefficient * totalMie);
      }
   }
}