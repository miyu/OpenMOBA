using System.Numerics;
using OpenMOBA.DataStructures;

namespace OpenMOBA.Foundation.Terrain.Declarations {
   public class SectorInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;

      public float WorldToLocalScalingFactor = 1.0f;
      public float LocalToWorldScalingFactor = 1.0f;

      public AxisAlignedBoundingBox WorldAABB = null;
   }
}