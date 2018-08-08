using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class MotionComponent : EntityComponent {
      public MotionComponent() : base(EntityComponentType.Movement) {
         Internals = MotionComponentInternals.Create();
      }

      public MotionStatistics BaseStatistics;
      public MotionComponentInternals Internals;
   }

   public struct MotionComponentInternals {
      public MotionPose Pose;
      public MotionStatistics ComputedStatistics;
      public Localization Localization;
      public Swarm Swarm;
      public int SwarmIndex;
      public SteeringState Steering;

      public static MotionComponentInternals Create() => new MotionComponentInternals {
         Pose = MotionPose.Create()
      };
   }

   public struct SteeringState {
      public FlockingStatus Status;
      public DoubleVector3 Destination;

      public MotionRoadmap Roadmap;
      public int RoadmapProgressIndex;
      public TerrainSnapshot LastFailedPathfindingSnapshot;

      public SteeringState(FlockingStatus status, DoubleVector3 destination, MotionRoadmap roadmap, int roadmapProgressIndex, TerrainSnapshot lastFailedPathfindingSnapshot) {
         Status = status;
         Destination = destination;
         Roadmap = roadmap;
         RoadmapProgressIndex = roadmapProgressIndex;
         LastFailedPathfindingSnapshot = lastFailedPathfindingSnapshot;
      }
   }
}