using System;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   public interface ICoreHolePrimitive {
      AxisAlignedBoundingBox3 ComputeWorldAABB(CoreTransform holeTransform);
      HoleProjection Project(CoreTransform holeTransform, CoreTransform sectorTransform);
      bool TryFastPointDistance(CoreTransform holeTransform, DoubleVector3 pointWorld, out Double distance);
   }
}
