using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.ECS {
   public struct MotionPose {
      public DoubleVector3 WorldPosition;
      public DoubleVector3 LookAt;

      public static MotionPose Create() => new MotionPose {
         LookAt = DoubleVector3.UnitX
      };
   }
}