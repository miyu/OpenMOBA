using System;
using System.Numerics;
using Xunit;

namespace FMatrix.Tests {
   public class FMatrixLikeNumericsTests {
      [Fact]
      public void Identity() => AssertAlike(FMatrix4x4.Identity, Matrix4x4.Identity);

      [Fact]
      public void RotationX() => Trials(1000, r => {
         var theta = (RandomFloat(r) - 0.5f) * 8.0f * MathF.PI;
         AssertAlike(FMatrix4x4.RotationX(theta), Matrix4x4.CreateRotationX(theta));
      });

      [Fact]
      public void RotationY() => Trials(1000, r => {
         var theta = (RandomFloat(r) - 0.5f) * 8.0f * MathF.PI;
         AssertAlike(FMatrix4x4.RotationY(theta), Matrix4x4.CreateRotationY(theta));
      });

      [Fact]
      public void RotationZ() => Trials(1000, r => {
         var theta = (RandomFloat(r) - 0.5f) * 8.0f * MathF.PI;
         AssertAlike(FMatrix4x4.RotationZ(theta), Matrix4x4.CreateRotationZ(theta));
      });

      [Fact]
      public void Translation() => Trials(1000, r => {
         var v = RandomVector3(r);
         AssertAlike(FMatrix4x4.Translation(v), Matrix4x4.CreateTranslation(v));
      });


      [Fact]
      public void Scale() => Trials(1000, r => {
         var v = RandomVector3(r);
         AssertAlike(FMatrix4x4.Scale(v), Matrix4x4.CreateScale(v));
      });

      [Fact]
      public void FromAxisAngle() => Trials(1000, r => {
         var axis = RandomVector3(r);
         var theta = (RandomFloat(r) - 0.5f) * 8.0f * MathF.PI;
         AssertAlike(FMatrix4x4.FromAxisAngle(axis, theta), Matrix4x4.CreateFromAxisAngle(axis, theta));
      });

      [Fact]
      public void LookAt() => Trials(1000, r => {
         var a = RandomVector3(r);
         var b = RandomVector3(r);
         var c = RandomVector3(r);
         AssertAlike(FMatrix4x4.LookAtRH(a, b, c), Matrix4x4.CreateLookAt(a, b, c));
      });

      private float RandomFloat(Random r) => r.Next() / (float)int.MaxValue;

      private Vector3 RandomVector3(Random r) => new Vector3(RandomFloat(r), RandomFloat(r), RandomFloat(r));
      private Vector4 RandomVector4(Random r) => new Vector4(RandomFloat(r), RandomFloat(r), RandomFloat(r), RandomFloat(r));

      private FMatrix4x4 RandomMatrix(Random r) => new FMatrix4x4(RandomVector4(r), RandomVector4(r), RandomVector4(r), RandomVector4(r));

      private void AssertAlike(FMatrix4x4 actual, Matrix4x4 expected) {
         expected = Matrix4x4.Transpose(expected);

         AssertAlike(actual.Row1.X, expected.M11);
         AssertAlike(actual.Row1.Y, expected.M12);
         AssertAlike(actual.Row1.Z, expected.M13);
         AssertAlike(actual.Row1.W, expected.M14);

         AssertAlike(actual.Row2.X, expected.M21);
         AssertAlike(actual.Row2.Y, expected.M22);
         AssertAlike(actual.Row2.Z, expected.M23);
         AssertAlike(actual.Row2.W, expected.M24);

         AssertAlike(actual.Row3.X, expected.M31);
         AssertAlike(actual.Row3.Y, expected.M32);
         AssertAlike(actual.Row3.Z, expected.M33);
         AssertAlike(actual.Row3.W, expected.M34);

         AssertAlike(actual.Row4.X, expected.M41);
         AssertAlike(actual.Row4.Y, expected.M42);
         AssertAlike(actual.Row4.Z, expected.M43);
         AssertAlike(actual.Row4.W, expected.M44);
      }

      private void AssertAlike(float actual, float expected) {
         if (MathF.Abs(actual - expected) > 0.001f) {
            throw new Exception($"Expected {expected}, Actual {actual}");
         }
      }

      private void Trials(int n, Action<Random> cb) {
         var r = new Random(0);
         for (var i = 0; i < n; i++) {
            cb(r);
         }
      }
   }
}
