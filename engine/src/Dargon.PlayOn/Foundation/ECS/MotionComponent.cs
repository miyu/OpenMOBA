using System;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.ECS {
   public struct MotionPose {
      public DoubleVector3 WorldPosition;
      public DoubleVector3 LookAt;

      public static MotionPose Create() => new MotionPose {
         LookAt = DoubleVector3.UnitX
      };
   }

   public struct MotionStatistics {
      public int Radius;
      public int Speed;
   }

   public struct SteeringState {
      public bool IsPathfindingEnabled;

      public DoubleVector3 Destination;
      public bool IsDestinationReached;

      public MotionRoadmap Roadmap;
      public int RoadmapProgressIndex;
      public bool IsRoadmapInvalidated;

      public TerrainSnapshot LastFailedPathfindingSnapshot;

      public FlockingForceContributions CurrentUpdateForceContributions;
      public FlockingForceContributions LastUpdateForceContributions;

      public static SteeringState Create() => new SteeringState {
         RoadmapProgressIndex = -1
      };
   }

   public struct LocalizationState {
      public TerrainOverlayNetwork TerrainOverlayNetwork;
      public TerrainOverlayNetworkNode TerrainOverlayNetworkNode;
      public DoubleVector2 LocalPosition;
      public IntVector2 LocalPositionIv2;
      public TriangulationIsland TriangulationIsland;
      public int TriangleIndex;
   }

   public struct ForceContribution {
      public DoubleVector2 SumForces;
      public cDouble SumWeights;
   }

   public struct FlockingForceContributions {
      public ForceContribution Seeking;
      public ForceContribution Alignment;
      public ForceContribution Aggregate;
   }

   public class MotionComponent : EntityComponent {
      public MotionComponent() : base(EntityComponentType.Movement) {
         Pose = MotionPose.Create();
         Steering = SteeringState.Create();
      }

      public MotionPose Pose;
      public MotionStatistics BaseStatistics;
      public MotionStatistics ComputedStatistics;
      public Swarm Swarm;
      public LocalizationState Localization;
      public SteeringState Steering;
      // public List<Tuple<DoubleVector3, DoubleVector3>> DebugLines;
   }
}