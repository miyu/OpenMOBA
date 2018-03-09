using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;

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
}