using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace FMatrix.Tests {
   using static NumericsStatics;

   public class RotationLookAtAcceptanceTests : FMatrixTestBase {
      public RotationLookAtAcceptanceTests(ITestOutputHelper output) : base(output) { }

      [Fact]
      public void Trivial() {
         // Convention is UnitZ forward, UnitX right, UnitY up in local space.
         var mat = FMatrix4x4.RotationLookAtRH(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY);
         AssertAlike(mat.Transform(Vector3.UnitZ), Vector3.UnitZ);
         AssertAlike(mat.Transform(Vector3.UnitX), Vector3.UnitX);
         AssertAlike(mat.Transform(Vector3.UnitY), Vector3.UnitY);
      }

      [Fact]
      public void Turn111() {
         // Convention is UnitZ forward, UnitX right, UnitY up in local space.
         var mat = FMatrix4x4.RotationLookAtRH(Vector3.Zero, Vector3.One, Vector3.UnitY);
         output.WriteLine(mat.ToStringNewline());
         AssertAlike(mat.Transform(Vector3.UnitZ), Vec3(1, 1, 1).Normalize());

         output.WriteLine(mat.Transform(Vector3.UnitY).ToString());
         Assert.True(mat.Transform(Vector3.UnitY).Dot(Vector3.UnitX) < 0);
         Assert.True(mat.Transform(Vector3.UnitY).Dot(Vector3.UnitY) > 0);
         Assert.True(mat.Transform(Vector3.UnitY).Dot(Vector3.UnitZ) < 0);

         output.WriteLine(mat.Transform(Vector3.UnitX).ToString());
         Assert.True(mat.Transform(Vector3.UnitX).Dot(Vector3.UnitX) > 0);
         Assert.True(mat.Transform(Vector3.UnitX).Dot(Vector3.UnitY) >= 0);
         Assert.True(mat.Transform(Vector3.UnitX).Dot(Vector3.UnitZ) < 0);
      }
   }
}