using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;
using cInt = System.Int32;

namespace OpenMOBA.Foundation.Terrain {
   public class TerrainStaticMetadata {
      public Rectangle LocalBoundary;
      public IReadOnlyList<Polygon2> LocalIncludedContours = new List<Polygon2>();
      public IReadOnlyList<Polygon2> LocalExcludedContours = new List<Polygon2>();
   }

   public class SectorInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;

      public float WorldToLocalScalingFactor = 1.0f;
      public float LocalToWorldScalingFactor = 1.0f;

      public AxisAlignedBoundingBox WorldAABB = null;

      public int CachedSnapshotVersion;
   }

   public class SectorNodeDescription {
      private readonly TerrainService terrainService;

      internal SectorNodeDescription(TerrainService terrainService, TerrainStaticMetadata staticMetadata) {
         this.terrainService = terrainService;
         this.StaticMetadata = staticMetadata;

         RecomputeWorldAABB();
      }

      // Internals touched by terrain service
      internal int Version;
      public TerrainStaticMetadata StaticMetadata;
      internal SectorInstanceMetadata InstanceMetadata = new SectorInstanceMetadata();

      // Publics accessible by game logic
      public Matrix4x4 WorldTransform
      {
         get => InstanceMetadata.WorldTransform;
         set {
            if (InstanceMetadata.WorldTransform != value) {
               InstanceMetadata.WorldTransform = value;
               var inverted = Matrix4x4.Invert(WorldTransform, out InstanceMetadata.WorldTransformInv);
               if (!inverted) {
                  throw new InvalidOperationException("Unable to invert transformation matrix!?");
               }

               RecomputeWorldAABB();

               Version++;
            }
         }
      }

      public Matrix4x4 WorldTransformInv => InstanceMetadata.WorldTransformInv;

      public float WorldToLocalScalingFactor {
         get => InstanceMetadata.WorldToLocalScalingFactor;
         set {
            if (InstanceMetadata.WorldToLocalScalingFactor != value) {
               InstanceMetadata.WorldToLocalScalingFactor = value;
               InstanceMetadata.LocalToWorldScalingFactor = 1.0f / value;
               Version++;
            }
         }
      }

      public float LocalToWorldScalingFactor => InstanceMetadata.LocalToWorldScalingFactor;

      public bool EnableDebugHighlight { get; set; }
      //      public IReadOnlyCollection<DynamicTerrainHole> Holes => InstanceMetadata.Holes;

      public AxisAlignedBoundingBox WorldBounds => InstanceMetadata.WorldAABB;

      private void RecomputeWorldAABB() {
         var transform = InstanceMetadata.WorldTransform;
         //         transform = Matrix4x4.Transpose(transform);

         var origin = Vector3.Transform(Vector3.Zero, transform).ToOpenMobaVector();
         var basisX = Vector3.TransformNormal(Vector3.UnitX, transform).ToOpenMobaVector();
         var basisY = Vector3.TransformNormal(Vector3.UnitY, transform).ToOpenMobaVector();
         var basisZ = Vector3.TransformNormal(Vector3.UnitZ, transform).ToOpenMobaVector();
         var bounds = StaticMetadata.LocalBoundary;

         var bottomLeft = basisY * bounds.Bottom + basisX * bounds.Left;
         var bottomRight = basisY * bounds.Bottom + basisX * bounds.Right;
         var topLeft = basisY * bounds.Top + basisX * bounds.Left;
         var topRight = basisY * bounds.Top + basisX * bounds.Right;
         var boundingBoxExtrusionFactor = 1E-3 * basisZ / basisZ.Norm2D();

         var nearBottomLeft = bottomLeft + boundingBoxExtrusionFactor;
         var nearBottomRight = bottomRight + boundingBoxExtrusionFactor;
         var nearTopLeft = topLeft + boundingBoxExtrusionFactor;
         var nearTopRight = topRight + boundingBoxExtrusionFactor;

         var farBottomLeft = bottomLeft - boundingBoxExtrusionFactor;
         var farBottomRight = bottomRight - boundingBoxExtrusionFactor;
         var farTopLeft = topLeft - boundingBoxExtrusionFactor;
         var farTopRight = topRight - boundingBoxExtrusionFactor;

         var mins = origin + nearBottomLeft.MinWith(nearBottomRight).MinWith(nearTopLeft).MinWith(nearTopRight).MinWith(farBottomLeft).MinWith(farBottomRight).MinWith(farTopLeft).MinWith(farTopRight);
         var maxs = origin + nearBottomLeft.MaxWith(nearBottomRight).MaxWith(nearTopLeft).MaxWith(nearTopRight).MaxWith(farBottomLeft).MaxWith(farBottomRight).MaxWith(farTopLeft).MaxWith(farTopRight);
         InstanceMetadata.WorldAABB = AxisAlignedBoundingBox.FromExtents(mins, maxs);
      }
   }

   public class HoleInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;

      public readonly int Height = 5;

      public int CachedSnapshotVersion;
   }

   /// <summary>
   /// Considered internal to TerrainService
   /// </summary>
   public class DynamicTerrainHoleDescription {
      private readonly TerrainService terrainService;

      internal DynamicTerrainHoleDescription(TerrainService terrainService, TerrainStaticMetadata staticMetadata) {
         this.terrainService = terrainService;
         this.StaticMetadata = staticMetadata;
      }

      // Internals touched by terrain service
      internal int Version;

      internal TerrainStaticMetadata StaticMetadata;
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
               Version++;
            }
         }
      }

      public Matrix4x4 WorldTransformInv => InstanceMetadata.WorldTransformInv;

      public bool EnableDebugHighlight { get; set; }

      private Vector4[] ComputeExtrudedBoundsLocal() => new[] {
         new Vector4(StaticMetadata.LocalBoundary.Left, StaticMetadata.LocalBoundary.Top, 0, 1),
         new Vector4(StaticMetadata.LocalBoundary.Right, StaticMetadata.LocalBoundary.Top, 0, 1),
         new Vector4(StaticMetadata.LocalBoundary.Right, StaticMetadata.LocalBoundary.Bottom, 0, 1),
         new Vector4(StaticMetadata.LocalBoundary.Left, StaticMetadata.LocalBoundary.Bottom, 0, 1),
//         new Vector4(StaticMetadata.LocalBoundary.Left, StaticMetadata.LocalBoundary.Top, InstanceMetadata.Height, 1),
//         new Vector4(StaticMetadata.LocalBoundary.Right, StaticMetadata.LocalBoundary.Top, InstanceMetadata.Height, 1),
//         new Vector4(StaticMetadata.LocalBoundary.Right, StaticMetadata.LocalBoundary.Bottom, InstanceMetadata.Height, 1),
//         new Vector4(StaticMetadata.LocalBoundary.Left, StaticMetadata.LocalBoundary.Bottom, InstanceMetadata.Height, 1)
      };

      public bool TryProjectOnto(SectorNodeDescription sectorNodeDescription, out IReadOnlyList<Polygon2> projectedHoleIncludedContours, out IReadOnlyList<Polygon2> projectedHoleExcludedContours) {
         // transformation matrix from hole-local space to sector-local space
         var transformHoleToSector = InstanceMetadata.WorldTransform * sectorNodeDescription.WorldTransformInv;

         // compute projected poly's bounds in sector-local space.
         var boundsWorld = ComputeExtrudedBoundsLocal().Map(p => Vector4.Transform(p, transformHoleToSector));

         // test if projected poly is within sector-local space.
         var intersects = boundsWorld.Any(p => sectorNodeDescription.StaticMetadata.LocalBoundary.Contains((int)p.X, (int)p.Y));
         var withinHeight = boundsWorld.Any(p => p.Z >= -1E-3 && p.Z <= InstanceMetadata.Height);
         if (!intersects || !withinHeight) {
            projectedHoleIncludedContours = null;
            projectedHoleExcludedContours = null;
            return false;
         }

         // Project rest of points into sector-local space.
         projectedHoleIncludedContours = StaticMetadata.LocalIncludedContours.Map(contour =>
            new Polygon2(
               contour.Points.Map(p => DotNetVector4ToIV2XY(Vector4.Transform(IV2ToDotNetVector4(p, InstanceMetadata.Height), transformHoleToSector)))
                      .ToList(),
               true));
         projectedHoleExcludedContours = StaticMetadata.LocalExcludedContours.Map(contour =>
            new Polygon2(
               contour.Points.Map(p => DotNetVector4ToIV2XY(Vector4.Transform(IV2ToDotNetVector4(p, InstanceMetadata.Height), transformHoleToSector)))
                      .ToList(),
               true));
         return true;
      }

      public void EnhanceLocalGeometryJob(SectorNodeDescription sectorNodeDescription, ref LocalGeometryJob localGeometryRenderJob) {
         IReadOnlyList<Polygon2> projectedHoleIncludedContours, projectedHoleExcludedContours;
         if (!TryProjectOnto(sectorNodeDescription, out projectedHoleIncludedContours, out projectedHoleExcludedContours)) {
            return;
         }

         localGeometryRenderJob.DynamicHoles[(this, Version)] = (projectedHoleIncludedContours, projectedHoleExcludedContours);
      }

      private static Vector4 IV2ToDotNetVector4(IntVector2 v, int z) => new Vector4(v.X, v.Y, z, 1);
      private static IntVector2 DotNetVector4ToIV2XY(Vector4 v) => new IntVector2((cInt)v.X, (cInt)v.Y);
   }
}