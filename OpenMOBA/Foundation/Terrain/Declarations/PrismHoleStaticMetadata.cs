using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Declarations {
   public class PrismHoleStaticMetadata : IHoleStaticMetadata {
      public Rectangle LocalBoundary;
      public IReadOnlyList<Polygon2> LocalIncludedContours = new List<Polygon2>();
      public IReadOnlyList<Polygon2> LocalExcludedContours = new List<Polygon2>();
      public int Height = 5;

      private Vector3[] ComputeExtrudedBoundsLocal() => new[] {
         new Vector3(LocalBoundary.Left, LocalBoundary.Top, 0),
         new Vector3(LocalBoundary.Right, LocalBoundary.Top, 0),
         new Vector3(LocalBoundary.Right, LocalBoundary.Bottom, 0),
         new Vector3(LocalBoundary.Left, LocalBoundary.Bottom, 0),
         //         new Vector3(StaticMetadata.LocalBoundary.Left, StaticMetadata.LocalBoundary.Top, InstanceMetadata.Height),
         //         new Vector3(StaticMetadata.LocalBoundary.Right, StaticMetadata.LocalBoundary.Top, InstanceMetadata.Height),
         //         new Vector3(StaticMetadata.LocalBoundary.Right, StaticMetadata.LocalBoundary.Bottom, InstanceMetadata.Height),
         //         new Vector3(StaticMetadata.LocalBoundary.Left, StaticMetadata.LocalBoundary.Bottom, InstanceMetadata.Height)
      };

      public bool TryProjectOnto(HoleInstanceMetadata instanceMetadata, SectorNodeDescription sectorNodeDescription, out IReadOnlyList<Polygon2> projectedHoleIncludedContours, out IReadOnlyList<Polygon2> projectedHoleExcludedContours) {
         // transformation matrix from hole-local space to sector-local space
         var transformHoleToSector = instanceMetadata.WorldTransform * sectorNodeDescription.WorldTransformInv;

         // compute projected poly's bounds in sector-local space.
         var boundsWorld = ComputeExtrudedBoundsLocal().Map(p => Vector3.Transform(p, transformHoleToSector));

         // test if projected poly is within sector-local space.
         var intersects = Enumerable.Any<Vector3>(boundsWorld, p => sectorNodeDescription.StaticMetadata.LocalBoundary.Contains((int)p.X, (int)p.Y));
         var withinHeight = Enumerable.Any<Vector3>(boundsWorld, p => p.Z >= -1E-3 && p.Z <= Height);
         //         if (false && () !intersects || !withinHeight) {
         //            projectedHoleIncludedContours = null;
         //            projectedHoleExcludedContours = null;
         //            return false;
         //         }

         // Project rest of points into sector-local space.
         projectedHoleIncludedContours = LocalIncludedContours.Map(contour =>
            new Polygon2(
               Enumerable.ToList<IntVector2>(contour.Points.Map(p => DotNetVector4ToIV2XY(Vector4.Transform(IV2ToDotNetVector4(p, Height), transformHoleToSector)))),
               true));
         projectedHoleExcludedContours = LocalExcludedContours.Map(contour =>
            new Polygon2(
               Enumerable.ToList<IntVector2>(contour.Points.Map(p => DotNetVector4ToIV2XY(Vector4.Transform(IV2ToDotNetVector4(p, Height), transformHoleToSector)))),
               true));
         return true;
      }

      public AxisAlignedBoundingBox ComputeWorldAABB(Matrix4x4 worldTransform) {
         // compute projected poly's bounds in sector-local space.
         var boundsWorld = ComputeExtrudedBoundsLocal().Map(
            p => Vector3.Transform(p, worldTransform)
                        .ToOpenMobaVector());
         return AxisAlignedBoundingBox.BoundingPoints(boundsWorld);
      }


      private static Vector4 IV2ToDotNetVector4(IntVector2 v, int z) => new Vector4(v.X, v.Y, z, 1);
      private static IntVector2 DotNetVector4ToIV2XY(Vector4 v) => new IntVector2((Int32)v.X, (Int32)v.Y);
   }
}