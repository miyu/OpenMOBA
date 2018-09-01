using System;
using System.Numerics;
using Dargon.Commons;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.ECS.Utils;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

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
      private readonly TerrainFacade terrainFacade;
      private readonly FlockingSimulator flockingSimulator;
      private readonly AssociatedStateContainer<object> associatedStateContainer;

      public MotionSystem(
         EntityWorld entityWorld, 
         GameTimeManager gameTimeManager, 
         TerrainFacade terrainFacade,
         FlockingSimulator flockingSimulator,
         AssociatedStateContainer<object> associatedStateContainer
      ) : base(entityWorld, kComponentMask) {
         this.gameTimeManager = gameTimeManager;
         this.terrainFacade = terrainFacade;
         this.flockingSimulator = flockingSimulator;
         this.associatedStateContainer = associatedStateContainer;
      }

      public override void HandleEntityAssociated(Entity entity) {
         associatedStateContainer.Add(entity);

         ref var structure = ref entity.MotionComponent.Internals.Structure;
         if (structure.IsEnabled) {
            Assert.IsNull(structure.HoleDescription);

            var holeDescription = terrainFacade.CreateHoleDescription(structure.HoleStaticMetadata);
            var m = Matrix4x4.CreateTranslation(entity.MotionComponent.Internals.Pose.WorldPosition.ToDotNetVector());
            holeDescription.InstanceMetadata.WorldTransform = m;
            structure.HoleDescription = holeDescription;
            terrainFacade.AddTemporaryHoleDescription(structure.HoleDescription);
         }
      }

      public override void HandleEntityDisassociated(Entity entity, int removedIndex, int replacementIndex) {
         associatedStateContainer.Remove(entity, removedIndex, replacementIndex);

         ref var structure = ref entity.MotionComponent.Internals.Structure;
         if (structure.IsEnabled) {
            Assert.IsNull(structure.HoleDescription);
            terrainFacade.RemoveTemporaryHoleDescription(structure.HoleDescription);
            structure.HoleDescription = null;
         }
      }

      public void ExecuteFlocking() {
         var dt = gameTimeManager.SecondsPerTick;
         flockingSimulator.Step(AssociatedEntities.ToArray(), dt);
      }
   }
}
