using System;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace FMatrix.Tests {
   public class FMatrixLikeNumericsTests : FMatrixTestBase {
      public FMatrixLikeNumericsTests(ITestOutputHelper output) : base(output) { }

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
         AssertAlike(FMatrix4x4.ViewLookAtRH(a, b, c), Matrix4x4.CreateLookAt(a, b, c));
      });

      [Fact]
      public void RotationLookAt() => Trials(1000, r => {
         var a = RandomVector3(r);
         var b = RandomVector3(r);
         var c = RandomVector3(r);

         // Rotation lookat RH is lookat LH with flipped Z
         var la = Matrix4x4.CreateLookAt(b, a, c);

         // And additionally, no translation
         la.M41 = la.M42 = la.M43 = 0.0f;

         // LookAt is meant for camera. Does the opposite motion of what we want,
         // so invert (orthonormal matrix, so transpose equivalent)
         la = Matrix4x4.Transpose(la);

         AssertAlike(FMatrix4x4.RotationLookAtRH(a, b, c), la);
      });


      [Fact]
      public void PerspectiveFovRH() => Trials(1000, r => {
         var fov = RandomFloat(r) * 2;
         var aspect = RandomFloat(r) * 2;
         var (znear, zfar) = RandomFloatOrderedPair(r, 0, 10000);

         AssertAlike(FMatrix4x4.PerspectiveFovRH(fov, aspect, znear, zfar), Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, znear, zfar));
      });


      [Fact]
      public void OrthoOffCenterRH() => Trials(1000, r => {
         var (left, right) = RandomFloatOrderedPair(r, -1000, 1000);
         var (bottom, top) = RandomFloatOrderedPair(r, -1000, 1000);
         var (znear, zfar) = RandomFloatOrderedPair(r, 0, 10000);

         AssertAlike(FMatrix4x4.OrthoOffCenterRH(left, right, bottom, top, znear, zfar), Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, znear, zfar));
      });

      [Fact]
      public void OrthoRH() => Trials(1000, r => {
         var width = RandomFloat(r) * 1000;
         var height = RandomFloat(r) * 1000;
         var (znear, zfar) = RandomFloatOrderedPair(r, 0, 10000);

         AssertAlike(FMatrix4x4.OrthoRH(width, height, znear, zfar), Matrix4x4.CreateOrthographic(width, height, znear, zfar));
      });

      [Fact]
      public void TryInvert() => Trials(1000, r => {
         var fm = RandomMatrix(r);
         var snm = ToNumericsTransposed(fm);
         AssertAlike(fm, snm);

         var fmInvSuccess = FMatrix4x4.TryInvert(fm, out var fmInv);
         var snmInvSuccess = Matrix4x4.Invert(snm, out var snmInv);
         Assert.Equal(fmInvSuccess, snmInvSuccess);
         AssertAlike(fmInv, snmInv, -0.005f);
      });
   }
}
