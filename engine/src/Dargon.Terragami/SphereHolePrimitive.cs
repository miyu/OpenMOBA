using System;
using System.Numerics;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
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
         var mul = CDoubleMath.c2 * CDoubleMath.Pi / (Double)n;
         for (var i = 0; i < n; i++) {
            var theta = i * mul;
            points[i] = new IntVector2(
               (Int32)(radius * CDoubleMath.Sin(theta)),
               (Int32)(radius * CDoubleMath.Cos(theta))
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
}
