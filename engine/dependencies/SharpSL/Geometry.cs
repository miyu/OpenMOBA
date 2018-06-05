using System;
using System.Numerics;
using FMatrix;

namespace SharpSL {
   using static NumericsStatics;

   public static class Geometry {
      // See https://gamedev.stackexchange.com/questions/96459/fast-ray-sphere-collision-code
      // See http://www-labs.iro.umontreal.ca/~sherknie/articles/faq_Divers/graphics-algorithms-faq.txt
      //     "Subject 5.07: How do I determine the intersection between a ray and a sphere"
      // Returns ray parameters <tnear, tfar> in order reached by ray.
      // If tnear > tfar, no intersection! If equal, then ray is tangent to sphere.
      public static (float tlow, float thigh) TryFindRayAndCenteredSphereIntersection(Vector3 rayOrigin, Vector3 rayDirection, float sphereRadius) {
         var a = Dot(rayDirection, rayDirection);
         var b = 2.0f * Dot(rayDirection, rayOrigin);
         var c = Dot(rayOrigin, rayOrigin) - sphereRadius * sphereRadius;
         var discriminant = b * b - 4.0f * a * c;

         // (-b +- sqrt(b^2 - 4ac)) / 2a
         if (discriminant < 0) return (1E30f, -1E30f);
         var rootDiscriminant = MathF.Sqrt(discriminant);
         var twoA = 2.0f * a;
         return ((-b - rootDiscriminant) / twoA, (-b + rootDiscriminant) / twoA);
      }
   }
}