using System;
using System.Numerics;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Foundation.Terrain.Declarations {
   public class SectorNodeDescription {
      private readonly Guid guid = Guid.NewGuid();
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

      public cDouble WorldToLocalScalingFactor {
         get => InstanceMetadata.WorldToLocalScalingFactor;
         set {
            if (InstanceMetadata.WorldToLocalScalingFactor != value) {
               InstanceMetadata.WorldToLocalScalingFactor = value;
               InstanceMetadata.LocalToWorldScalingFactor = CDoubleMath.c1 / value;
               Version++;
            }
         }
      }

      public cDouble LocalToWorldScalingFactor => InstanceMetadata.LocalToWorldScalingFactor;

      public bool EnableDebugHighlight { get; set; }
      //      public IReadOnlyCollection<DynamicTerrainHole> Holes => InstanceMetadata.Holes;

      public AxisAlignedBoundingBox3 WorldBounds => InstanceMetadata.WorldAABB;

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
         var boundingBoxExtrusionFactor = (CDoubleMath.c1 / (cDouble)1000) * basisZ / basisZ.Norm2D();

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
         InstanceMetadata.WorldAABB = AxisAlignedBoundingBox3.FromExtents(mins, maxs);
      }

      public override string ToString() => guid.ToString("n").Substring(0, 8);
   }
}