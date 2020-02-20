using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using FMatrix;
using static SharpSL.MathUtils;

namespace SharpSL {
   public struct vec2 {
      public float x;
      public float y;

      public vec2(float x, float y) {
         this.x = x;
         this.y = y;
		}

      public static vec2 operator +(vec2 a, vec2 b) {
         return new vec2(a.x + b.x, a.y + b.y);
		}

      public static vec2 operator -(vec2 a, vec2 b) {
         return new vec2(a.x - b.x, a.y - b.y);
      }

		public static vec2 operator *(vec2 a, vec2 b) {
         return new vec2(a.x * b.x, a.y * b.y);
      }


      public static vec2 operator *(float a, vec2 b) {
         return new vec2(a * b.x, a * b.y);
      }

      public static vec2 operator *(vec2 a, float b) {
         return new vec2(a.x * b, a.y * b);
      }
   }

   class Terrain {
      public static readonly PixelShader Pixel = new PixelShader();

      public struct PixelInput {
         public Vector2 UV;
         public FMatrix4x4 CameraProjViewInv;
         public float Time { get; set; }
      }

      public class PixelShader : Shader<PixelInput, Vector3> {
         public override Vector3 Compute(PixelInput input) {
            return computeColor(vec2(input.UV.X, input.UV.Y), input.Time);

         }

         public Shader<Vector2, Vector3> Configure(FMatrix4x4 projViewInv, float time) {
            return ProxyIn<Vector2>(uv => new PixelInput {
               UV = uv,
               CameraProjViewInv = projViewInv,
               Time = time,
            });
         }
         float random(vec2 p) {
            return fract(sin(dot(p, vec2(12.9898f, 78.233f))) * 43758.5453123f);
         }

         // Based on Morgan McGuire @morgan3d
         // https://www.shadertoy.com/view/4dS3Wd
         float noise(vec2 p) {
            vec2 i = floor(p);
            vec2 f = fract(p);

            // Four corners in 2D of a tile
            float a = random(i);
            float b = random(i + vec2(1.0f, 0.0f));
            float c = random(i + vec2(0.0f, 1.0f));
            float d = random(i + vec2(1.0f, 1.0f));

            vec2 u = f * f * (3.0f * vec2(1, 1) - 2.0f * f);

            return mix(a, b, u.x) +
                   (c - a) * u.y * (1.0f - u.x) +
                   (d - b) * u.x * u.y;
         }

         float fbm(vec2 p) {
            float v = 0.0f;
            float a = 0.5f;
            vec2 shift = vec2(100.0f, 100.0f);

            // Rotate to reduce axial bias
            vec2 rotr1 = vec2(cos(0.5f), sin(0.5f));
            vec2 rotr2 = vec2(-sin(0.5f), cos(0.5f));

            for (int i = 0; i < 9; ++i) {
               //v += a * noise(p);
               v += a * pow(abs(noise(p) * 2.0f - 1.0f), 1.3f);
               p = (rotr1 * p.x + rotr2 * p.y) * 2.0f + shift;
               if (i <= 2) {
                  a *= 0.8f;
               } else {
                  a *= 0.5f;
               }
            }
            return v;
         }

         Vector3 computeColor(vec2 uv, float iTime) {
            vec2 c = vec2(iTime * 100.0f, iTime * 20.0f);
            vec2 st = (uv * 2.0f - vec2(1.0f, 1.0f)) * 7.0f + c; //[-10, 10]^2
            // st += st * abs(sin(u_time*0.1)*3.0);
            var color = new Vector3();

            /*
          vec2 q = vec2(0.);
          q.x = fbm( st + 0.00*iTime);
          q.y = fbm( st + vec2(1.0));
   
          vec2 r = vec2(0.);
          r.x = fbm( st + 1.0*q + vec2(1.7,9.2)+ 0.15*iTime );
          r.y = fbm( st + 1.0*q + vec2(8.3,2.8)+ 0.126*iTime);
   
          float f = fbm(st+r);
          float h = (f*f)*4.0;
    */
            float h = fbm(st + vec2(0.15f, 0.15f)); // * iTime);
            h = h * h * 3.0f;
            h = h + 1.0f - 0.6f * pow(distance(st, c), 1.0f + 0.5f * fract(/*iTime*/ 0.45f));
            h /= 2.0f;

            //h = h > (fract(iTime)) ? 1.0f : 0.0f;
            float tl = 0.4f;
            float tm = 0.8f;
            float water = h < tl ? 1.0f : 0.0f;
            float land = h > tl && h < tm ? 1.0f : 0.0f;
            float mountain = h > tm ? 1.0f : 0.0f;

            color =
               (water * new Vector3(0.0f, 0.0f, 1.0f) +
                land * new Vector3(0.0f, 1.0f, 0.0f) +
                mountain * new Vector3(1.0f, 1.0f, 0.0f))
               * clamp(h, 0.0f, 1.0f);

            return color * color;
         }

         // void mainImage(out vec4 fragColor, in vec2 fragCoord) {
         //    vec2 uv = fragCoord / iResolution.xy; // [0,1]^2
         //    fragColor = vec4(computeColor(uv, iTime), 1.0f);
         // }
      }
   }

   public static class MathUtils {
      public static float abs(float a) => MathF.Abs(a);

      public static vec2 abs(vec2 a) => new vec2(abs(a.x), abs(a.y));

      public static float dot(vec2 a, vec2 b) => a.x * b.x + a.y * b.y;

      public static float distance(vec2 a, vec2 b) => MathF.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));

      public static float clamp(float x, float lo, float hi) => MathF.Max(MathF.Min(x, hi), lo);

      public static float sin(float f) => MathF.Sin(f);

      public static vec2 sin(vec2 v) => new vec2(MathF.Sin(v.x), MathF.Sin(v.y));

      public static float cos(float f) => MathF.Cos(f);

      public static vec2 cos(vec2 v) => new vec2(MathF.Cos(v.x), MathF.Cos(v.y));

      public static float fract(float f) => ((f % 1.0f) + 1.0f) % 1.0f;

      public static vec2 fract(vec2 v) => new vec2(fract(v.x), fract(v.y));

      public static float floor(float v) => MathF.Floor(v);

      public static vec2 floor(vec2 v) => new vec2(floor(v.x), floor(v.y));

      public static float mix(float x, float y, float a) => x * (1.0f - a) + y * a;

      public static vec2 mix(vec2 x, vec2 y, float a) => x * (1.0f - a) + y * a;

      public static vec2 vec2(float x, float y) => new vec2(x, y);

      public static float pow(float a, float p) => MathF.Pow(a, p);

      public static vec2 pow(vec2 v, float p) => new vec2(pow(v.x, p), pow(v.y, p));
	}
}
