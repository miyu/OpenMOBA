using System;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.ECS {
   public enum WalkResult {
      PushInward,
      CanPushInward,
      Progress,
      CanEdgeFollow,
      Completion
   }

   public class MotionSystem : EntitySystem, INetworkedSystem {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Movement);
      private readonly GameTimeManager gameTimeManager;
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly StatsCalculator statsCalculator;
      private readonly TerrainFacade terrainFacade;

      public MotionSystem(
         EntityWorld entityWorld,
         GameTimeManager gameTimeManager,
         StatsCalculator statsCalculator,
         TerrainFacade terrainFacade,
         PathfinderCalculator pathfinderCalculator
      ) : base(entityWorld, kComponentMask) {
         this.gameTimeManager = gameTimeManager;
         this.statsCalculator = statsCalculator;
         this.terrainFacade = terrainFacade;
         this.pathfinderCalculator = pathfinderCalculator;
      }

      public void HandleHoleAdded(DynamicTerrainHoleDescription holeDescription) {
         InvalidatePaths();

         foreach (var entity in AssociatedEntities) {
            var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
            var paddedHoleDilationRadius = characterRadius + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius + InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            if (holeDescription.ContainsPoint(paddedHoleDilationRadius, entity.MotionComponent.Pose.WorldPosition)) {
//               Console.WriteLine("Out ouf bounds!");
               FixEntityInHole(entity);
            }
         }
      }

      public void InvalidatePaths() {
         foreach (var entity in AssociatedEntities) {
            entity.MotionComponent.Steering.IsRoadmapInvalidated = true;
         }
      }

      private int zzzz = 0;
      private cDouble wwww = CDoubleMath.c0;
      private cDouble wmul = CDoubleMath.c1 / (cDouble)0.69;

      private void ExecutePathSwarmer(Entity entity, MotionComponent mc) {
         // ReSharper disable once CompareOfFloatsByEqualityOperator
         if (mc.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights == CDoubleMath.c0) return;
         if (mc.Steering.CurrentUpdateForceContributions.Aggregate.SumForces == DoubleVector2.Zero) return;

         var k = mc.Steering.CurrentUpdateForceContributions.Aggregate.SumForces / mc.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights;
         zzzz++;
         wwww += k.Norm2D();
         if (zzzz % 1000 == 0) {
            wmul = CDoubleMath.c1 / (wwww / (cDouble)zzzz);
//            Console.WriteLine("!!!!!" + wwww / zzzz);
            wwww = CDoubleMath.c0;
            zzzz = 0;
         }

         var worldDistanceRemaining = (cDouble)mc.ComputedStatistics.Speed * gameTimeManager.SecondsPerTick * wmul;
         var localDistanceRemaining = worldDistanceRemaining * mc.Localization.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor;
         var dv2 = ComputePositionUpdate(
            localDistanceRemaining,
            mc.Localization.LocalPosition,
            mc.Steering.CurrentUpdateForceContributions.Aggregate.SumForces / mc.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights, // dupe of k above?
            mc.Localization.TriangulationIsland,
            mc.Localization.TriangleIndex);
         mc.Localization.LocalPosition = dv2;
         mc.Localization.LocalPositionIv2 = dv2.LossyToIntVector2();
         mc.Pose.WorldPosition = mc.Localization.TerrainOverlayNetworkNode.SectorNodeDescription.LocalToWorld(dv2);
      }

      public object SaveState() {
         throw new NotImplementedException();
      }
   }
}
