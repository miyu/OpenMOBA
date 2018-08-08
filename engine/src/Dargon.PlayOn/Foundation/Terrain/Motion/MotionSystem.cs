using System;
using Dargon.PlayOn.Foundation.ECS;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public enum WalkResult {
      PushInward,
      CanPushInward,
      Progress,
      CanEdgeFollow,
      Completion
   }

   public class MotionSystem : OrderedEntitySystemBase {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Movement);
      private readonly MotionStateContainer motionStateContainer;

      public MotionSystem(
         EntityWorld entityWorld,
         MotionStateContainer motionStateContainer
      ) : base(entityWorld, kComponentMask) {
         this.motionStateContainer = motionStateContainer;
      }

      public override void HandleEntityAssociated(Entity entity) {
         motionStateContainer.Add(entity);
      }

      public override void HandleEntityDisassociated(Entity entity, int removedIndex, int replacementIndex) {
         motionStateContainer.Remove(entity, removedIndex, replacementIndex);
      }

      public override void Execute() {
      }
   }
}
