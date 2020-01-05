using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Threading;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Dviz;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.ThirdParty.ClipperLib;
using Dargon.Terragami.Dviz;
using cInt = System.Int32;

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

   public class Portal {
      public IntLineSegment2 Segment;
      public Clockness InClockness;
   }

   public class GeometryInput {
      public SectorBlueprint Blueprint;
      public CoreTransform Transform;
   }

   public class HoleProjection {
      public PolygonNode Root;
      public CoreTransform Transform;
   }

   public interface ICoreHolePrimitive {
      AxisAlignedBoundingBox3 ComputeWorldAABB(CoreTransform holeTransform);
      HoleProjection Project(CoreTransform holeTransform, CoreTransform sectorTransform);
      bool TryFastPointDistance(CoreTransform holeTransform, DoubleVector3 pointWorld, out cDouble distance);
   }

   public class SphereHolePrimitive : ICoreHolePrimitive {
      private const int NumPoints = 16;

      public SphereHolePrimitive(float radius) {
         Radius = radius;
      }

      public float Radius { get; }

      public HoleProjection Project(CoreTransform holeTransform, CoreTransform sectorTransform) {
         var radius = CDoubleMath.Ceiling(Radius * holeTransform.Scale / sectorTransform.Scale);

         var n = NumPoints;
         var points = new IntVector2[n];
         var mul = CDoubleMath.c2 * CDoubleMath.Pi / (cDouble)n;
         for (var i = 0; i < n; i++) {
            var theta = -i * mul;
            points[i] = new IntVector2(
                (cInt)(radius * CDoubleMath.Sin(theta)),
               (cInt)(radius * CDoubleMath.Cos(theta))
               );
         }

         var root = PolygonNode.CreateRootHole(
            PolygonNode.Create(points, false)
            );

         return new HoleProjection {
            Root = root,
            Transform = CoreTransform.LocalToLocal(holeTransform, sectorTransform)
         };
      }

      public bool TryFastPointDistance(CoreTransform holeTransform, DoubleVector3 pointWorld, out double distance) {
         var holeOrigin = Vector3.Transform(Vector3.Zero, holeTransform.Matrix);
         distance = Vector3.Distance(pointWorld.ToDotNetVector(), holeOrigin);
         return true;
      }

      public AxisAlignedBoundingBox3 ComputeWorldAABB(CoreTransform holeTransform) {
         var holeOrigin = Vector3.Transform(Vector3.Zero, holeTransform.Matrix).ToOpenMobaVector();
         return AxisAlignedBoundingBox3.FromExtents(
            holeOrigin - DoubleVector3.One * Radius,
            holeOrigin + DoubleVector3.One * Radius
            );
      }
   }

   public class HoleInput {
      public ICoreHolePrimitive HolePrimitive;
      public CoreTransform Transform;
   }

   public class CompilationInput {
      public GeometryInput Land;
      public ExposedArrayList<Portal> Portals;
      public ExposedArrayList<HoleInput> Holes;
   }

   public class Program {
      public static void Main(string[] args) {
         var blueprint = SectorBlueprints.Test2D;
         var input = new CompilationInput {
            Land = new GeometryInput {
               Blueprint = blueprint,
               Transform = new CoreTransform(Matrix4x4.Identity, Matrix4x4.Identity, 2.5f / blueprint.LocalBoundary.Width),
            },
            Portals = new ExposedArrayList<Portal>(),
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
         while (true) {
            var ntrialiters = 1;
            var niters = 100;
            for (var i = 0; i < niters; i++) {
               var canvas = i < ntrialiters ? debugMultiCanvasHost.CreateAndAddCanvas(i) : null;
               Eval(input, canvas);

               if (i + 1 == ntrialiters) {
                  sw.Restart();
               }
            }
            Console.WriteLine(sw.ElapsedMilliseconds / (float)(niters - ntrialiters) + " ms");
         }
      }

      private static void Eval(CompilationInput input, IDebugCanvas debugCanvasOpt) {
         var punch = PolygonOperations.Punch();
         punch.Include(input.Land.Blueprint.Root);
         foreach (var hole in input.Holes) {
            var holeProjection = hole.HolePrimitive.Project(hole.Transform, input.Land.Transform);
            var holeTransform = holeProjection.Transform.Flatten();
            var mat = holeTransform.Matrix;
            
            var s = new Stack<PolygonNode>();
            var transformedContour = new List<IntVector2>();
            s.Push(holeProjection.Root);
            while (s.Count > 0) {
               var n = s.Pop();
               foreach (var succ in n.Children) {
                  s.Push(succ);
               }
               if (n.Contour == null) continue;

               transformedContour.Clear();
               foreach (var p in n.Contour) {
                  var q = Vector2.Transform(p.ToDotNetVector(), mat).ToOpenMobaVector().LossyToIntVector2();
                  transformedContour.Add(q);
                  Console.WriteLine(q);
               }
               punch.Exclude(transformedContour);
            }
         }

         var punchedLand = punch.Execute();
         if (debugCanvasOpt != null) {
            debugCanvasOpt.Transform = Matrix4x4.Identity;
            debugCanvasOpt.DrawPolygonNode(punchedLand);
         }
      }

      private static PolyTree X() {
         return null;
         Polygon2 ClipperExtentsHoleClipPolygon = Polygon2.CreateRect(
            -InternalTerrainCompilationConstants.SectorClipBounds,
            -InternalTerrainCompilationConstants.SectorClipBounds,
            InternalTerrainCompilationConstants.SectorClipBounds * 2 + 1,
            InternalTerrainCompilationConstants.SectorClipBounds * 2 + 1);

         Polygon2 ClipHoleContour(Polygon2 polygon) {
            PolygonOperations.TryConvexClip(polygon, ClipperExtentsHoleClipPolygon, out var result);
            if (result != null) {
               foreach (var p in result.Points) {
                  var useFullRange = true;
                  ClipperBase.RangeTest(p, ref useFullRange);
               }
            }
            return result;
         }

         // var dilatedHolesUnion =
         //    PolygonOperations.Offset()
         //                     .Include(job.SectorBlueprint.LocalExcludedContours)
         //                     .Include(job.DynamicHoles.Values.SelectMany(item => item.holeIncludedContours)
         //                                 .Select(ClipHoleContour)
         //                                 .Where(p => p != null))
         //                     .Include(job.DynamicHoles.Values.SelectMany(item =>
         //                        item.holeExcludedContours.Select(p => new Polygon2(((IReadOnlyList<IntVector2>)p.Points).Reverse().ToList()))
         //                        ).Select(ClipHoleContour).Where(p => p != null))
         //                     .Dilate(holeDilationRadius)
         //                     .Cleanup()
         //                     .Execute();
         //
         // var erodedOuterContour =
         //    PolygonOperations.Offset()
         //                     .Include(job.SectorBlueprint.LocalIncludedContours)
         //                     .Erode(holeDilationRadius)
         //                     .Cleanup()
         //                     .Execute();

         // var crossoverLandPolys =
         //    job.CrossoverSegments.Select(tuple => {
         //       var (segment, inClockness) = tuple;
         //       var firstToSecond = segment.First.To(segment.Second).ToDoubleVector2();
         //       var perp = new DoubleVector2(firstToSecond.Y, -firstToSecond.X);
         //       var extrusionMagnitude = holeDilationRadius + (cDouble)2;
         //       var inward = perp * (extrusionMagnitude / perp.Norm2D());
         //       var outward = perp * (CDoubleMath.cNeg2 / perp.Norm2D());
         //       if (inClockness == Clockness.CounterClockwise) {
         //          inward *= -1;
         //          outward *= -1;
         //       }
         //
         //       var shrink = firstToSecond * (holeDilationRadius / firstToSecond.Norm2D());
         //       var points = new List<IntVector2>(new[]{
         //          (segment.First.ToDoubleVector2() + outward + shrink).LossyToIntVector2(),
         //          (segment.First.ToDoubleVector2() + inward + shrink).LossyToIntVector2(),
         //          (segment.Second.ToDoubleVector2() + inward - shrink).LossyToIntVector2(),
         //          (segment.Second.ToDoubleVector2() + outward - shrink).LossyToIntVector2()
         //       });
         //
         //       if (inClockness == Clockness.CounterClockwise) {
         //          points.Reverse();
         //       }
         //
         //       return new Polygon2(points);
         //    }).ToArray();
         //
         // PolyTree PostProcessPunchedLand(PolyTree punchedLand) {
         //    void TagSectorSnapshotAndGeometryContext(PolyNode node) {
         //       // node.visibilityGraphNodeData.LocalGeometryView = this;
         //       node.Childs.ForEach(TagSectorSnapshotAndGeometryContext);
         //    }
         //
         //    void TagBoundingVolumeHierarchies(PolyNode node) {
         //       var contourEdges = node.Contour.Zip(node.Contour.RotateLeft(), IntLineSegment2.Create).ToArray();
         //       var bvh = BvhILS2.Build(contourEdges);
         //       node.visibilityGraphNodeData.ContourBvh = bvh;
         //       node.Childs.ForEach(TagBoundingVolumeHierarchies);
         //    }
         //
         //    punchedLand.Prune(holeDilationRadius);
         //    TagSectorSnapshotAndGeometryContext(punchedLand);
         //    TagBoundingVolumeHierarchies(punchedLand);
         //    return punchedLand;
         // }
         //
         // var punchedLand = PostProcessPunchedLand(
         //    PolygonOperations.Punch()
         //                     .Include(erodedOuterContour.FlattenToPolygonAndIsHoles())
         //                     .Include(crossoverLandPolys)
         //                     .Exclude(dilatedHolesUnion.FlattenToPolygonAndIsHoles())
         //                     .Execute()
         //    );
         //
         // return punchedLand;
      }
   }
}
