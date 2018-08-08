using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class MotionOperations {
      private readonly TerrainFacade terrainFacade;
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly StatsCalculator statsCalculator;

      public MotionOperations(TerrainFacade terrainFacade, PathfinderCalculator pathfinderCalculator, StatsCalculator statsCalculator) {
         this.terrainFacade = terrainFacade;
         this.pathfinderCalculator = pathfinderCalculator;
         this.statsCalculator = statsCalculator;
      }

      public void SetPathfindingDestination(Entity entity, DoubleVector3 destination) {
         var mc = entity.MotionComponent;
         ref var steering = ref mc.Internals.Steering;
         if (steering.Status != FlockingStatus.Disabled && steering.Destination == destination) return;
         mc.Internals.Steering = new SteeringState(
            FlockingStatus.EnabledInvalidatedRoadmap,
            destination,
            null,
            -1,
            null);
      }

      public void FixEntityInHole(Entity e) {
         var mc = e.MotionComponent;
         var computedRadius = statsCalculator.ComputeCharacterRadius(e);
         var network = terrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(computedRadius);
         var res = network.FindNearestLandPointLocalization(mc.Internals.Pose.WorldPosition, computedRadius);
         mc.Internals.Pose.WorldPosition = res.world;
         mc.Internals.Localization = res.localization;
      }
   }
}