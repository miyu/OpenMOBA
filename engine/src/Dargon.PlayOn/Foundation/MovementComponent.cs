using System;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation {
   public class MovementComponent : EntityComponent {
      public MovementComponent() : base(EntityComponentType.Movement) { }
      public DoubleVector3 WorldPosition { get; set; }
      public DoubleVector3 LookAt { get; set; } = DoubleVector3.UnitX;
      public Double BaseRadius { get; set; }
      public Double BaseSpeed { get; set; }
      public Swarm Swarm { get; set; }

      public bool GoalReached { get; set; }

      /// <summary>
      ///    If true, movement will recompute path before updating position
      /// </summary>
      public bool PathingIsInvalidated { get; set; }

      /// <summary>
      ///    The desired destination of the unit. Even if pathfinding fails, this is still set and,
      ///    once terrain changes, pathing may attempt to resume.
      /// </summary>
      public DoubleVector3 PathingDestination { get; set; }
      public bool IsPathfindingEnabled { get; set; }

      public MotionRoadmap PathingRoadmap { get; set; } = null;
      public int PathingRoadmapProgressIndex = -1;
      public TerrainSnapshot LastFailedPathfindingSnapshot = null;

      // public List<Tuple<DoubleVector3, DoubleVector3>> DebugLines { get; set; }

      // Values precomputed at entry of movement service
      public int ComputedRadius { get; set; }
      public int ComputedSpeed { get; set; }

      public DoubleVector2 LastSeekingWeightedSumNBodyForces { get; set; }
      public Double LastSeekingSumWeightsNBodyForces { get; set; }

      public DoubleVector2 SeekingWeightedSumNBodyForces { get; set; }
      public Double SeekingSumWeightsNBodyForces { get; set; }

      public DoubleVector2 AlignmentWeightedSumNBodyForces { get; set; }
      public Double AlignmentSumWeightsNBodyForces { get; set; }

      public DoubleVector2 WeightedSumNBodyForces { get; set; }
      public Double SumWeightsNBodyForces { get; set; }
      public DoubleVector2 LastWeightedSumNBodyForces { get; set; }
      public Double LastSumWeightsNBodyForces { get; set; }
      public TerrainOverlayNetwork TerrainOverlayNetwork { get; set; }
      public TerrainOverlayNetworkNode TerrainOverlayNetworkNode { get; set; }
      public DoubleVector2 LocalPosition { get; set; }
      public IntVector2 LocalPositionIv2 { get; set; }
      public TriangulationIsland SwarmingIsland { get; set; }
      public int SwarmingTriangleIndex { get; set; }

      // Final computed swarmling velocity
      public DoubleVector2 SwarmlingVelocity { get; set; }
   }
}