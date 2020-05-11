using System;
using Dargon.Commons;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   public static class MathUtils {
      /// <summary>
      /// https://math.stackexchange.com/questions/1098487/atan2-faster-approximation
      /// range: (-pi, pi]
      /// </summary>
      public static double FastAtan2(double y, double x) {
         var ax = Math.Abs(x);
         var ay = Math.Abs(y);

         // a:= min(| x |, | y |) / max(| x |, | y |)
         var a = ax < ay ? (ax / ay) : (ay / ax);

         // s:= a * a
         var s = a * a;

         // r:= ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a
         var r = ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a;

         // if | y | > | x | then r:= 1.57079637 - r
         if (ay > ax) r = 1.57079637 - r;

         // if x < 0 then r := 3.14159274 - r
         if (x < 0.0f) r = 3.14159274 - r;

         // if y < 0 then r := -r
         if (y < 0.0f) r = -r;

         return r;
      }

      /// <summary>
      /// https://math.stackexchange.com/questions/1098487/atan2-faster-approximation
      /// range: (-pi, pi]
      /// </summary>
      public static float FastAtan2(float y, float x) {
         var ax = Math.Abs(x);
         var ay = Math.Abs(y);

         // a:= min(| x |, | y |) / max(| x |, | y |)
         var a = ax < ay ? (ax / ay) : (ay / ax);

         // s:= a * a
         var s = a * a;

         // r:= ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a
         var r = ((-0.0464964749f * s + 0.15931422f) * s - 0.327622764f) * s * a + a;

         // if | y | > | x | then r:= 1.57079637 - r
         if (ay > ax) r = 1.57079637f - r;

         // if x < 0 then r := 3.14159274 - r
         if (x < 0.0f) r = 3.14159274f - r;

         // if y < 0 then r := -r
         if (y < 0.0f) r = -r;

         return r;
      }

      public static float FastSignedAngleBetweenVectorsF(IntVector2 v1, IntVector2 v2) {
         return FastAtan2((float)v1.Cross(v2), (float)v1.Dot(v2));
      }

      public static float SignedAngleBetweenVectorsF(IntVector2 v1, IntVector2 v2) {
         return MathF.Atan2((float)v1.Cross(v2), (float)v1.Dot(v2));
      }

      public static float SignedAngleBetweenVectorsF(DoubleVector2 v1, DoubleVector2 v2) {
         return MathF.Atan2((float)v1.Cross(v2), (float)v1.Dot(v2));
      }

      public static float RemapRadiansNegPiToPi_0To2Pi(this float radians) {
         if (radians < 0) {
            radians += MathF.PI * 2;
            Assert.IsGreaterThanOrEqualTo(radians, 0);
            return radians;
         }

         Assert.IsLessThan(radians, MathF.PI * 2);
         return radians;
      }
   }
}