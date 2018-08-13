using System;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.ECS {
   public class StatisticsCalculator {
      public MotionStatistics CalculateMotionStatistics(Entity entity) {
         var mc = entity.MotionComponent;
         if (mc == null) return default;
         return mc.BaseStatistics;
      }

      public cDouble ComputeCharacterRadius(Entity entity) {
         var mc = entity.MotionComponent;
         if (mc == null) return default;
         return mc.BaseStatistics.Radius;
      }

      public cDouble ComputeMovementSpeed(Entity entity) {
         var mc = entity.MotionComponent;
         if (mc == null) return default;
         return mc.BaseStatistics.Speed;
      }
   }
}
