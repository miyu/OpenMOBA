using System.Numerics;
using OpenMOBA.DataStructures;

namespace OpenMOBA.Foundation.Terrain.Declarations {
   public class HoleInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;

      public AxisAlignedBoundingBox WorldAABB = null;
   }
}