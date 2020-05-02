using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Commons.Pooling;
using Dargon.Dviz;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using Dargon.Terragami.Sectors;
using cInt = System.Int32;
using Point = System.Drawing.Point;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.Terragami {
   /// <summary>
   /// T(R(S(input)))
   /// </summary>
   public class CoreTransform {
      public Matrix4x4 Matrix;
      public Matrix4x4 InverseMatrix;
      public cDouble Scale;

      public CoreTransform(Matrix4x4 matrix, Matrix4x4 inverseMatrix, cDouble scale) {
         Matrix = matrix;
         InverseMatrix = inverseMatrix;
         Scale = scale;
      }

      public LocalTransform Flatten() {
         var mat = new Matrix3x2(
            Matrix.M11, Matrix.M12,
            Matrix.M21, Matrix.M22,
            Matrix.M41, Matrix.M42);
         var matInv = new Matrix3x2(
            InverseMatrix.M11, InverseMatrix.M12,
            InverseMatrix.M21, InverseMatrix.M22,
            InverseMatrix.M41, InverseMatrix.M42);
         return new LocalTransform(
            mat,
            matInv,
            Scale);
      }

      public static CoreTransform CreateTRS(DoubleVector3 position, Quaternion rotation, cDouble scale) {
         var rot = Matrix4x4.CreateFromQuaternion(rotation);
         var rotInv = Matrix4x4.Transpose(rot);

         Matrix4x4 tra = Matrix4x4.CreateTranslation((float)position.X, (float)position.Y, (float)position.Z);
         Matrix4x4 traInv = Matrix4x4.CreateTranslation(-(float)position.X, -(float)position.Y, -(float)position.Z);

         // p*S*R*T
         var mat = Matrix4x4.Multiply(Matrix4x4.Multiply(Matrix4x4.CreateScale((float)scale), rot), tra);

         // (p*S*R*T) * (Tinv * RInv * SInv)
         var matInv = traInv * rotInv * Matrix4x4.CreateScale(1.0f / (float)scale);
         return new CoreTransform(mat, matInv, scale);
      }

      public static CoreTransform LocalToLocal(CoreTransform src, CoreTransform dest) {
         return new CoreTransform(
            src.Matrix * dest.InverseMatrix, // p * MSrc * MInvDest
            dest.Matrix * src.InverseMatrix, // (p * Msrc * MInvDest) * MDest * MInvSrc
            src.Scale / dest.Scale
            );
      }
   }

   public class LocalTransform {
      public Matrix3x2 Matrix;
      public Matrix3x2 InverseMatrix;
      public cDouble Scale;

      public LocalTransform(Matrix3x2 mat, Matrix3x2 matInv, cDouble scale) {
         Matrix = mat;
         InverseMatrix = matInv;
         Scale = scale;
      }
   }

   public class Program {
      public static void Main(string[] args) {
         var blueprint = SectorBlueprints.FourSquares2D;
         var input = new SectorCompilationInput {
            Land = new GeometryInput {
               Blueprint = blueprint,
               Transform = new CoreTransform(Matrix4x4.Identity, Matrix4x4.Identity, 2.5f / blueprint.LocalBoundary.Width),
            },
            TraversableCorners = new HashSet<IntVector2> { }
               .Concat(SectorBlueprints.FourSquares2D.Root.Children[0].Children[0].Contour)
               .Concat(SectorBlueprints.FourSquares2D.Root.Children[0].Children[1].Contour)
               .ToHashSet(),
            PinPoints = new HashSet<IntVector2> {
               blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 2, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 4, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 2, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 4, 6),
               blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 0, 1),
               blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 0, 1),
               blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 1, 1),
               blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 1, 1),
            },
            Portals = new ExposedArrayList<SectorPortal> {
               new SectorPortal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 2, 6),
                     blueprint.LocalBoundary.LossyPointAtRatio(0, 1, 4, 6)),
                  CrossoverPointsGenerated = 10,
               },
               new SectorPortal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 2, 6),
                     blueprint.LocalBoundary.LossyPointAtRatio(1, 1, 4, 6)),
                  CrossoverPointsGenerated = 10,
               },
               new SectorPortal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 0, 1),
                     blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 0, 1)),
                  CrossoverPointsGenerated = 10,
               },
               new SectorPortal {
                  Segment = new IntLineSegment2(
                     blueprint.LocalBoundary.LossyPointAtRatio(2, 6, 1, 1),
                     blueprint.LocalBoundary.LossyPointAtRatio(4, 6, 1, 1)),
                  CrossoverPointsGenerated = 10,
               },
            },
            Holes = new ExposedArrayList<HoleInput> {
               new HoleInput {
                  HolePrimitive = new SphereHolePrimitive(0.5f),
                  Transform = new CoreTransform(Matrix4x4.Identity, Matrix4x4.Identity, 1),
               }
            },
         };

         var debugMultiCanvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(
            new Size(800, 600), new Point(200, 200), 
            new OrthographicXYProjector(0.02f, default, new Vector2(400, 300), true));

         var sw = new Stopwatch();
         var totalIters = 0;
         var ntrialiters = 1;
         while (true) {
            var niters = 10000;
            for (var i = 0; i < niters; i++, totalIters++) {
               var canvas = totalIters < ntrialiters ? debugMultiCanvasHost.CreateAndAddCanvas(i) : null;
               var compilation = new SectorCompiler().Compile(input, canvas);

               if (canvas != null) {
                  SectorArrangement.Create(
                     compilation.VisibilityBarriers.Map(
                        b => new DoubleLineSegment2(b.First.ToDoubleVector2(), b.Second.ToDoubleVector2())),
                     canvas);
               }
               // var vp = SectorVisibilityPolygon.Create(new DoubleVector2(0, 0), compilation.VisibilityBarriers);
               // if (canvas != null) {
               //    canvas.DrawVisibilityPolygon(vp);
               // }

               // var t1 = sw.Elapsed.TotalMilliseconds;
               // for (var it = 0; it < 10000; it++) {
               //    foreach (var node in compilation.PunchedLand.Dfs((push, n) => n.Children.ForEach(push))) {
               //       if (node.Contour == null) continue;
               //       foreach (var p in node.Contour) {
               //          SectorVisibilityPolygon.Create(p.ToDoubleVector2(), compilation.VisibilityBarriers);
               //       }
               //    }
               // }
               // var t2 = sw.Elapsed.TotalMilliseconds;
               // Console.WriteLine("VP 10000 " + (t2 -  t1));

               if (i + 1 == ntrialiters) {
                  sw.Restart();
               }
            }
            var ms = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine(niters + "iters " + ms + " => " + ms / niters + " ms");
         }
      }
   }
}
