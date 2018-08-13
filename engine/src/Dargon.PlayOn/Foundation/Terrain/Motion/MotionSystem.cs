using System;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.ECS.Utils;
using Dargon.PlayOn.Foundation.Terrain.Declarations;

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
      private readonly GameTimeManager gameTimeManager;
      private readonly FlockingSimulator flockingSimulator;
      private readonly AssociatedStateContainer<object> associatedStateContainer;

      public MotionSystem(
         EntityWorld entityWorld, 
         GameTimeManager gameTimeManager, 
         FlockingSimulator flockingSimulator, 
         AssociatedStateContainer<object> associatedStateContainer
      ) : base(entityWorld, kComponentMask) {
         this.gameTimeManager = gameTimeManager;
         this.flockingSimulator = flockingSimulator;
         this.associatedStateContainer = associatedStateContainer;
      }

      public override void HandleEntityAssociated(Entity entity) {
         associatedStateContainer.Add(entity);
      }

      public override void HandleEntityDisassociated(Entity entity, int removedIndex, int replacementIndex) {
         associatedStateContainer.Remove(entity, removedIndex, replacementIndex);
      }

      public void ExecuteFlocking() {
         var dt = gameTimeManager.SecondsPerTick;
         flockingSimulator.Step(AssociatedEntities.ToArray(), dt);
      }
   }
}
