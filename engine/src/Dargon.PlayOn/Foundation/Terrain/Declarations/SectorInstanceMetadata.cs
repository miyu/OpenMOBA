using System.Numerics;
using Dargon.PlayOn.DataStructures;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain.Declarations {
   public class SectorInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;

      public cDouble WorldToLocalScalingFactor = CDoubleMath.c1;
      public cDouble LocalToWorldScalingFactor = CDoubleMath.c1;

      public AxisAlignedBoundingBox3 WorldAABB = null;
   }
}