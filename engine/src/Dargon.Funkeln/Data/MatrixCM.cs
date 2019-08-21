using System;
using SharpDX;

namespace Canvas3D {
   // column-major matrices
   public static class MatrixCM {
      // Matrix that orients rest of scene to fit the given lookat params.
      // Use RotationLookAtRH to rotate objects to point to locations.
      public static Matrix ViewLookAtRH(Vector3 eye, Vector3 target, Vector3 up) {
         var matrix = Matrix.LookAtRH(eye, target, up);
         matrix.Transpose();
         return matrix;
      }

      // Matrix that transforms reference frame to orient y+/z+ in a certain way.
      // In contrast, LookAtRH is a matrix for transforming everyone else to fit these constraints.
      // That is the inverse of this behavior.
      public static Matrix RotationLookAtRH(Vector3 directionAkaDesiredYplus, Vector3 upAkaDesiredZPlus) {
         var orien = Quaternion.RotationLookAtRH(directionAkaDesiredYplus, upAkaDesiredZPlus);
         var matrix = Matrix.RotationQuaternion(orien);
         return matrix; // no transpose; orthogonal matrix, want inverse so transpose twice.
      }

      public static Matrix PerspectiveFovRH(float fov, float aspect, float znear, float zfar) {
         var matrix = Matrix.PerspectiveFovRH(fov, aspect, znear, zfar);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix OrthoRH(float width, float height, float znear, float zfar) {
         var matrix = Matrix.OrthoRH(width, height, znear, zfar);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix OrthoOffCenterRH(float left, float right, float bottom, float top, float znear, float zfar) {
         var matrix = Matrix.OrthoOffCenterRH(left, right, bottom, top, znear, zfar);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix Scaling(float scale) {
         var matrix = Matrix.Scaling(scale);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix Scaling(float x, float y, float z) {
         var matrix = Matrix.Scaling(x, y, z);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix Translation(Vector3 v) => Translation(v.X, v.Y, v.Z);

      public static Matrix Translation(float x, float y, float z) {
         var matrix = Matrix.Translation(x, y, z);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix RotationX(float rads) {
         var matrix = Matrix.RotationX(rads);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix RotationY(float rads) {
         var matrix = Matrix.RotationY(rads);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix RotationZ(float rads) {
         var matrix = Matrix.RotationZ(rads);
         matrix.Transpose();
         return matrix;
      }

      public static Matrix Invert(Matrix input) {
         Matrix inverse;
         input.Transpose();
         Matrix.Invert(ref input, out inverse);
         input.Transpose();
         inverse.Transpose();
         return inverse;
      }
   }
}
