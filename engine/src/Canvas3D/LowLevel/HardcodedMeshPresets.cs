using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SharpDX;

namespace Canvas3D.LowLevel {
   public static class HardcodedMeshPresets {
      public static VertexPositionNormalColorTexture[] ColoredCubeVertices { get; } = {
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)), // Front
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)), // BACK
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(0, 0)), // Top
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(1, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(0, 0)), // Bottom
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(1, 1)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(1, 1)), // Left
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(1, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(1, 1)), // Right
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(0, 0)),
      };

      public static VertexPositionNormalColorTexture[] PlaneXYVertices { get; } = {
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 1)), // Back
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 1)), // Front
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 0))
      };

      public static VertexPositionNormalColorTexture[] Sphere { get; } = ComputeSphere(12, 0.5f);

      private static VertexPositionNormalColorTexture[] ComputeSphere(int lod, float radius) {
         if (lod < 4) throw new ArgumentOutOfRangeException();
         var r = new Vector3(0, radius, 0);
         var transform = Matrix.RotationZ(-(float)Math.PI / lod);
         var vs = new float[lod + 1];
         var points = new Vector3[lod + 1];
         for (var i = 0; i < points.Length; i++) {
            vs[i] = i / (float)lod;
            points[i] = r;
            r = (Vector3)Vector3.Transform(r, transform);
         }
         return ComputeSurfaceOfYRevolution(points, points, vs, lod);
      }

      // Points in XY plane; Z = 0. Move clockwise looking at from Z+ toward origin.
      // Recall we use a right-hand coordinate system.
      public static VertexPositionNormalColorTexture[] ComputeSurfaceOfYRevolution(Vector3[] points, Vector3[] normals, float[] vs, int nSteps = 16) {
         var stepIterator = Enumerable.Range(0, nSteps + 1); // inclusive on both endpoints
         var us = stepIterator.Select(step => step / (float)nSteps).ToArray();
         var rotationMatrices = stepIterator.Select(step => Matrix.RotationY(MathUtil.TwoPi * step / nSteps)).ToArray();
         var revolvedPointsByStep = rotationMatrices.Select(m => points.Select(p => (Vector3)Vector4.Transform(new Vector4(p, 1), m)).ToArray()).ToArray();
         var revolvedNormalsByStep = rotationMatrices.Select(m => normals.Select(n => (Vector3)Vector4.Transform(new Vector4(n, 0), m)).ToArray()).ToArray();

         var result = new List<VertexPositionNormalColorTexture>();
         for (var step = 0; step < nSteps; step++) {
            var (lps, lns, lu) = (revolvedPointsByStep[step], revolvedNormalsByStep[step], us[step]);
            var (rps, rns, ru) = (revolvedPointsByStep[step + 1], revolvedNormalsByStep[step + 1], us[step + 1]);
            for (var i = 0; i < points.Length - 1; i++) {
               var (tlp, tln, blp, bln) = (lps[i], lns[i], lps[i + 1], lns[i + 1]);
               var (trp, trn, brp, brn) = (rps[i], rns[i], rps[i + 1], rns[i + 1]);
               var (tv, bv) = (vs[i], vs[i + 1]);

               // Triangle 1 [TL, TR, BL]
               result.Add(new VertexPositionNormalColorTexture(tlp, tln, Color.White, new Vector2(lu, tv)));
               result.Add(new VertexPositionNormalColorTexture(trp, trn, Color.White, new Vector2(ru, tv)));
               result.Add(new VertexPositionNormalColorTexture(blp, bln, Color.White, new Vector2(lu, bv)));

               // Triangle 2 [BL, TR, BR]
               result.Add(new VertexPositionNormalColorTexture(blp, bln, Color.White, new Vector2(lu, bv)));
               result.Add(new VertexPositionNormalColorTexture(trp, trn, Color.White, new Vector2(ru, tv)));
               result.Add(new VertexPositionNormalColorTexture(brp, brn, Color.White, new Vector2(ru, bv)));
            }
         }
         return result.ToArray();
      }
   }
}
