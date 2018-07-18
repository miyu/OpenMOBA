using System;

namespace Dargon.PlayOn.Foundation {
   public class StatsCalculator {
      public Double ComputeCharacterRadius(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) return CDoubleMath.c0;
         return movementComponent.BaseRadius;
      }

      public Double ComputeMovementSpeed(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) return CDoubleMath.c0;
         return movementComponent.BaseSpeed;
      }
   }
}