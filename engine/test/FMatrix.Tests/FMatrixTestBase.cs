using System;
using System.Numerics;
using Xunit.Abstractions;

namespace FMatrix.Tests {
   public class FMatrixTestBase {
      private const float kDefaultTolerance = 0.001f;
      protected readonly ITestOutputHelper output;

      public FMatrixTestBase(ITestOutputHelper output) => this.output = output;

      public float RandomFloat(Random r) => r.Next() / (float)int.MaxValue;
      public (float, float) RandomFloatOrderedPair(Random r, float low, float high) {
         var (a, b) = (RandomFloat(r) * (high - low) + low, RandomFloat(r) * (high - low) + low);
         return (Math.Min(a, b), Math.Max(a, b));
      }

      public Vector3 RandomVector3(Random r) => new Vector3(RandomFloat(r), RandomFloat(r), RandomFloat(r));
      public Vector4 RandomVector4(Random r) => new Vector4(RandomFloat(r), RandomFloat(r), RandomFloat(r), RandomFloat(r));

      public FMatrix4x4 RandomMatrix(Random r) => new FMatrix4x4(RandomVector4(r), RandomVector4(r), RandomVector4(r), RandomVector4(r));
      public Matrix4x4 ToNumericsTransposed(FMatrix4x4 m) => new Matrix4x4(m.M11, m.M21, m.M31, m.M41, m.M12, m.M22, m.M32, m.M42, m.M13, m.M23, m.M33, m.M43, m.M14, m.M24, m.M34, m.M44);

      public void AssertAlike(FMatrix4x4 actual, Matrix4x4 expected, float tol = kDefaultTolerance) {
         expected = Matrix4x4.Transpose(expected);

         output.WriteLine($"actual: {actual}");
         output.WriteLine($"expected: {expected}");

         AssertAlike(actual.Row1.X, expected.M11, tol);
         AssertAlike(actual.Row1.Y, expected.M12, tol);
         AssertAlike(actual.Row1.Z, expected.M13, tol);
         AssertAlike(actual.Row1.W, expected.M14, tol);

         AssertAlike(actual.Row2.X, expected.M21, tol);
         AssertAlike(actual.Row2.Y, expected.M22, tol);
         AssertAlike(actual.Row2.Z, expected.M23, tol);
         AssertAlike(actual.Row2.W, expected.M24, tol);

         AssertAlike(actual.Row3.X, expected.M31, tol);
         AssertAlike(actual.Row3.Y, expected.M32, tol);
         AssertAlike(actual.Row3.Z, expected.M33, tol);
         AssertAlike(actual.Row3.W, expected.M34, tol);

         AssertAlike(actual.Row4.X, expected.M41, tol);
         AssertAlike(actual.Row4.Y, expected.M42, tol);
         AssertAlike(actual.Row4.Z, expected.M43, tol);
         AssertAlike(actual.Row4.W, expected.M44, tol);
      }

      public void AssertAlike(Vector3 actual, Vector3 expected, float tol = kDefaultTolerance) {
         AssertAlike(actual.X, expected.X, tol);
         AssertAlike(actual.Y, expected.Y, tol);
         AssertAlike(actual.Z, expected.Z, tol);
      }

      public void AssertAlike(float actual, float expected, float tol = kDefaultTolerance) {
         var delta = MathF.Abs(actual - expected);
         if (delta > MathF.Abs(tol)) {
            var percentError = MathF.Abs(delta / expected);
            if (tol < 0 && percentError < -tol) {
               return;
            }

            throw new Exception($"Expected {expected}, Actual {actual}, % Error: {percentError}");
         }
      }

      public void Trials(int n, Action<Random> cb) {
         var r = new Random(0);
         for (var i = 0; i < n; i++) {
            cb(r);
         }
      }
   }
}