using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class MotionComponent : EntityComponent {
      public MotionComponent() : base(EntityComponentType.Movement) {
         Internals = MotionComponentInternals.Create();
      }

      public MotionStatistics BaseStatistics;
      public MotionComponentInternals Internals;

      public static MotionComponent Create(DoubleVector3 worldPosition, MotionStatistics? baseStatistics = null, DoubleVector3? initialPathfindingDestination = null) {
         var mc = new MotionComponent {
            BaseStatistics = baseStatistics ?? new MotionStatistics { Radius = 100, Speed = 100 },
            Internals = { Pose = { WorldPosition = worldPosition } }
         };
         if (initialPathfindingDestination.HasValue) {
            mc.Internals.Steering = new SteeringState {
               Destination = initialPathfindingDestination.Value,
               Status = FlockingStatus.EnabledInvalidatedRoadmap
            };
         }
         return mc;
      }

      public static MotionComponent CreateWithSwarm(DoubleVector3 worldPosition, MotionStatistics baseStatistics, Swarm swarm) => new MotionComponent {
         BaseStatistics = baseStatistics,
         Internals = {
            Pose = { WorldPosition = worldPosition },
            Swarm = swarm
         }
      };

      public static MotionComponent CreateImmobileBuilding(DoubleVector3 worldPosition, IHoleStaticMetadata holeStaticMetadata) {
         return new MotionComponent {
            BaseStatistics = default,
            Internals = {
               Pose = { WorldPosition = worldPosition },
               Structure = {
                  IsEnabled = true,
                  HoleStaticMetadata = holeStaticMetadata,
               }
            }
         };
      }
   }

   public struct MotionComponentInternals {
      public MotionPose Pose;
      public MotionStatistics ComputedStatistics;
      public bool IsLocalizationInvalidated;
      public Localization Localization;
      public Swarm Swarm;
      public int SwarmIndex;
      public SteeringState Steering;
      public DoubleVector2 Hack_CohesionSeparationVector;
      public StructureState Structure;

      public static MotionComponentInternals Create() => new MotionComponentInternals {
         Pose = MotionPose.Create(),
         IsLocalizationInvalidated = true,
      };
   }

   public static class MotionComponentInternalsStatics {
      public static bool IsPathfindingEnabled(this SteeringState ss) {
         return ss.Status != FlockingStatus.Disabled;
      }
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

   public struct StructureState {
      public bool IsEnabled;
      public IHoleStaticMetadata HoleStaticMetadata;
      public DynamicTerrainHoleDescription HoleDescription; // Handle into terrainservice, updated by terrainfacade
   }
}