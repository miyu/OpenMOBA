using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using Vector3 = System.Numerics.Vector3;

namespace Canvas3D.LowLevel {
   public struct SHCoeffs1 {
      public float c0;
      public float c1, c2, c3;
      public float c4, c5, c6, c7, c8;

      public override string ToString() => $"[0]: {c0} [1]: {c1} [2]: {c2} [3]: {c3} [4]: {c4} [5]: {c5} [6]: {c6} [7]: {c7} [8]: {c8}";
   }

   public struct SHCoeffs3 {
      public Vector3 c0;
      public Vector3 c1, c2, c3;
      public Vector3 c4, c5, c6, c7, c8;

      public override string ToString() => $"[0]: {c0} [1]: {c1} [2]: {c2} [3]: {c3} [4]: {c4} [5]: {c5} [6]: {c6} [7]: {c7} [8]: {c8}";
   }

   public class SH {
      public static readonly float y0_c0 = (float)(Math.Sqrt(1.0 / (4 * Math.PI)));
      public static readonly float y1_c0 = (float)(Math.Sqrt(3.0 / (4 * Math.PI)));
      public static readonly float y2_c0 = (float)(Math.Sqrt(15 / (4 * Math.PI)));
      public static readonly float y2_c1 = (float)(Math.Sqrt(5 / (16 * Math.PI)));
      public static readonly float y2_c2 = (float)(Math.Sqrt(15 / (8 * Math.PI)));
      public static readonly float y2_c3 = (float)(Math.Sqrt(15 / (16 * Math.PI)));
      public static readonly float yn = y0_c0 + y1_c0 * 3 + y2_c0 * 3 + y2_c1 + y2_c3;

      public static Vector3 Evaluate(SHCoeffs3 c, Vector3 d) {
         var components = ShEval1(d);

         return c.c0 * components.c0 +
                c.c1 * components.c1 +
                c.c2 * components.c2 +
                c.c3 * components.c3 +
                c.c4 * components.c4 +
                c.c5 * components.c5 +
                c.c6 * components.c6 +
                c.c7 * components.c7 +
                c.c8 * components.c8;
      }

      public static Vector3 EvaluateCosineConvolved(SHCoeffs3 c, Vector3 d) {
         var components = ShEval1(d);

         var amp = 4; // TODO: Why is this necessary? https://computergraphics.stackexchange.com/questions/4997/spherical-harmonics-diffuse-cubemap-how-to-get-coefficients
         var a0 = amp * (float)Math.PI;
         var a1 = amp * (2.0f / 3.0f) * (float)Math.PI;
         var a2 = amp * (1.0f / 4.0f) * (float)Math.PI;

         return c.c0 * components.c0 * a0 +
                c.c1 * components.c1 * a1 +
                c.c2 * components.c2 * a1 +
                c.c3 * components.c3 * a1 +
                c.c4 * components.c4 * a2 +
                c.c5 * components.c5 * a2 +
                c.c6 * components.c6 * a2 +
                c.c7 * components.c7 * a2 +
                c.c8 * components.c8 * a2;
      }

      public static SHCoeffs3 ProjectSparse((Vector3 d, Vector3 v, float w)[] inputs) {
         var res = new SHCoeffs3();
         var wTotal = 0.0f;
         foreach (var (d, v, w) in inputs) {
            var coeffs = ShEval1(d);

            var vw = v * w;
            res.c0 += vw * coeffs.c0;
            res.c1 += vw * coeffs.c1;
            res.c2 += vw * coeffs.c2;
            res.c3 += vw * coeffs.c3;
            res.c4 += vw * coeffs.c4;
            res.c5 += vw * coeffs.c5;
            res.c6 += vw * coeffs.c6;
            res.c7 += vw * coeffs.c7;
            res.c8 += vw * coeffs.c8;

            wTotal += w;
         }

         // the sparse samples are a simplification of integration over r=1 sphere surface
         // if signal is 1, then total signal is 4Pi.
         // var normalizer = 4.0f * (float)Math.PI / wTotal;
         var normalizer = 1 / wTotal;
         res.c0 *= normalizer;
         res.c1 *= normalizer;
         res.c2 *= normalizer;
         res.c3 *= normalizer;
         res.c4 *= normalizer;
         res.c5 *= normalizer;
         res.c6 *= normalizer;
         res.c7 *= normalizer;
         res.c8 *= normalizer;
         return res;
      }

      public static SHCoeffs1 ShEval1(Vector3 v) {
         // // theta from 0 to pi, thus sin is never negative
         // var cosTheta = Vector3.Dot(Vector3.UnitZ, d); 
         // var sinTheta = (float)Math.Sqrt(1 - cosTheta * cosTheta); // Vector3.Cross(Vector3.UnitZ, d).Length();
         // 
         // // phi from 0 to 2pi, so both cos and sin can be negative.
         // // note <sin(theta)cos(phi), sin(theta)sin(phi), cos(theta)>
         // var clampedSinTheta = Math.Max(sinTheta, 1E-4); // prevent div0
         // var cosPhi = d.X / clampedSinTheta;
         // var sinPhi = d.Y / clampedSinTheta;

         float x = v.X, y = v.Y, z = v.Z;

         var res = new SHCoeffs1();
         res.c0 += y0_c0;
         res.c1 += y1_c0 * y;
         res.c2 += y1_c0 * z;
         res.c3 += y1_c0 * x;
         res.c4 += y2_c0 * x * y;
         res.c5 += y2_c0 * y * z;
         res.c6 += y2_c1 * (3 * z * z - 1);
         res.c7 += y2_c2 * x * z;
         res.c8 += y2_c3 * (x * x - y * y);
         return res;
      }
   }
}
