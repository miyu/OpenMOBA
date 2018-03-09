using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;
using SharpDX;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;
using Rectangle = System.Drawing.Rectangle;

namespace OpenMOBA.Foundation.Terrain {
   public class HoleInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;

      public AxisAlignedBoundingBox WorldAABB = null;
   }

   public interface IHoleStaticMetadata {
      bool TryProjectOnto(HoleInstanceMetadata instanceMetadata, SectorNodeDescription sectorNodeDescription, out IReadOnlyList<Polygon2> projectedHoleIncludedContours, out IReadOnlyList<Polygon2> projectedHoleExcludedContours);
      AxisAlignedBoundingBox ComputeWorldAABB(Matrix4x4 worldTransform);
   }

   public class SphereHoleStaticMetadata : IHoleStaticMetadata {
      public float Radius;

      public bool TryProjectOnto(HoleInstanceMetadata instanceMetadata, SectorNodeDescription sectorNodeDescription, out IReadOnlyList<Polygon2> projectedHoleIncludedContours, out IReadOnlyList<Polygon2> projectedHoleExcludedContours) {
         Trace.Assert(Matrix4x4.Decompose(instanceMetadata.WorldTransform, out var scale, out var rotation, out var translation));
         Trace.Assert(Math.Abs(scale.X - scale.Y) < 1E-9 && Math.Abs(scale.X - scale.Z) < 1E-9);
         var effectiveRadiusWorld = scale.X * Radius;
         var normalSectorWorld = Vector3.TransformNormal(new Vector3(0, 0, 1), sectorNodeDescription.WorldTransform).ToOpenMobaVector();
         var normalSectorWorldUnit = normalSectorWorld / normalSectorWorld.Norm2D();
         var sectorOriginWorld = Vector3.Transform(new Vector3(0, 0, 0), sectorNodeDescription.WorldTransform).ToOpenMobaVector();
         var distanceWorld = Math.Abs((sectorOriginWorld - translation.ToOpenMobaVector()).Dot(normalSectorWorldUnit));
         var c2MinusA2 = effectiveRadiusWorld * effectiveRadiusWorld - distanceWorld * distanceWorld;
         if (c2MinusA2 <= 0) {
            projectedHoleIncludedContours = null;
            projectedHoleExcludedContours = null;
            return false;
         }

         var effectiveRadiusWorldOnSectorPlane = (float)Math.Sqrt(c2MinusA2);
         var effectiveRadiusSector = Vector3.TransformNormal(
            new Vector3(0, 0, effectiveRadiusWorldOnSectorPlane),
            sectorNodeDescription.InstanceMetadata.WorldTransformInv
         ).Length();

//         if (effectiveRadiusSector < 200) {
//            projectedHoleIncludedContours = null;
//            projectedHoleExcludedContours = null;
//            return false;
//         } else {
//            projectedHoleIncludedContours = new List<Polygon2> {
//               Polygon2.CreateCircle(0, 0, 20)
//            };
//            projectedHoleExcludedContours = new List<Polygon2>();
//            return true;
//         }

         var centerSector = Vector3.Transform(
            translation,
            sectorNodeDescription.InstanceMetadata.WorldTransformInv
         );

         projectedHoleIncludedContours = new List<Polygon2> {
            Polygon2.CreateCircle((int)centerSector.X, (int)centerSector.Y, (int)effectiveRadiusSector)
         };
         projectedHoleExcludedContours = new List<Polygon2>();
         return true;
      }

      public AxisAlignedBoundingBox ComputeWorldAABB(Matrix4x4 worldTransform) {
         // todo: can we do this more efficiently?
         var corners = new[] {
            new Vector3(-Radius, -Radius, -Radius),
            new Vector3(-Radius, -Radius,  Radius),
            new Vector3(-Radius,  Radius, -Radius),
            new Vector3(-Radius,  Radius,  Radius),
            new Vector3( Radius, -Radius, -Radius),
            new Vector3( Radius, -Radius,  Radius),
            new Vector3( Radius,  Radius, -Radius),
            new Vector3( Radius,  Radius,  Radius)
         };
         return AxisAlignedBoundingBox.BoundingPoints(corners.Map(p => Vector3.Transform(p, worldTransform).ToOpenMobaVector()));
      }
   }

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
         var intersects = boundsWorld.Any(p => sectorNodeDescription.StaticMetadata.LocalBoundary.Contains((int)p.X, (int)p.Y));
         var withinHeight = boundsWorld.Any(p => p.Z >= -1E-3 && p.Z <= Height);
         //         if (false && () !intersects || !withinHeight) {
         //            projectedHoleIncludedContours = null;
         //            projectedHoleExcludedContours = null;
         //            return false;
         //         }

         // Project rest of points into sector-local space.
         projectedHoleIncludedContours = LocalIncludedContours.Map(contour =>
            new Polygon2(
               contour.Points.Map(p => DotNetVector4ToIV2XY(Vector4.Transform(IV2ToDotNetVector4(p, Height), transformHoleToSector)))
                      .ToList(),
               true));
         projectedHoleExcludedContours = LocalExcludedContours.Map(contour =>
            new Polygon2(
               contour.Points.Map(p => DotNetVector4ToIV2XY(Vector4.Transform(IV2ToDotNetVector4(p, Height), transformHoleToSector)))
                      .ToList(),
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

   /// <summary>
   /// Considered internal to TerrainService
   /// </summary>
   public class DynamicTerrainHoleDescription {
      private readonly TerrainService terrainService;

      internal DynamicTerrainHoleDescription(TerrainService terrainService, IHoleStaticMetadata staticMetadata) {
         this.terrainService = terrainService;
         this.StaticStaticMetadata = staticMetadata;
      }

      // Internals touched by terrain service
      internal int Version;

      public IHoleStaticMetadata StaticStaticMetadata;
      internal HoleInstanceMetadata InstanceMetadata = new HoleInstanceMetadata();

      // Publics accessible by game logic
      public Matrix4x4 WorldTransform
      {
         get => InstanceMetadata.WorldTransform;
         set
         {
            if (InstanceMetadata.WorldTransform != value) {
               InstanceMetadata.WorldTransform = value;
               var inverted = Matrix4x4.Invert(WorldTransform, out InstanceMetadata.WorldTransformInv);
               if (!inverted) {
                  throw new InvalidOperationException("Unable to invert transformation matrix!?");
               }

               InstanceMetadata.WorldAABB = StaticStaticMetadata.ComputeWorldAABB(value);
               Version++;
            }
         }
      }

      public Matrix4x4 WorldTransformInv => InstanceMetadata.WorldTransformInv;

      public bool EnableDebugHighlight { get; set; }

      public AxisAlignedBoundingBox WorldBounds => InstanceMetadata.WorldAABB;

      public void EnhanceLocalGeometryJob(SectorNodeDescription sectorNodeDescription, ref LocalGeometryJob localGeometryRenderJob) {
         if (!WorldBounds.Intersects(sectorNodeDescription.WorldBounds)) {
            return;
         }

         IReadOnlyList<Polygon2> projectedHoleIncludedContours, projectedHoleExcludedContours;
         if (!StaticStaticMetadata.TryProjectOnto(InstanceMetadata, sectorNodeDescription, out projectedHoleIncludedContours, out projectedHoleExcludedContours)) {
            return;
         }

         localGeometryRenderJob.DynamicHoles[(this, Version)] = (projectedHoleIncludedContours, projectedHoleExcludedContours);
      }
   }
}