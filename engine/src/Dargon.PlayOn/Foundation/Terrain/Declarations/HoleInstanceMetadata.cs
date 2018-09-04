using System.Numerics;
using Dargon.PlayOn.DataStructures;

namespace Dargon.PlayOn.Foundation.Terrain.Declarations {
   public class HoleInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;

      public AxisAlignedBoundingBox3 WorldAABB = null;
      public bool EnableHolePathfindingWaypoints;
   }
}