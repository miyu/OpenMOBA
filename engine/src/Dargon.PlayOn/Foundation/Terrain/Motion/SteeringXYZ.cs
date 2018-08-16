using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dargon.Commons;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class FlockingSimulator {
      private readonly StatisticsCalculator statisticsCalculator; // TODO: Feels out of place in this class.
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly TerrainFacade terrainFacade;
      private readonly TriangulationWalker triangulationWalker;

      public FlockingSimulator(StatisticsCalculator statisticsCalculator, PathfinderCalculator pathfinderCalculator, TerrainFacade terrainFacade, TriangulationWalker triangulationWalker) {
         this.statisticsCalculator = statisticsCalculator;
         this.pathfinderCalculator = pathfinderCalculator;
         this.terrainFacade = terrainFacade;
         this.triangulationWalker = triangulationWalker;
      }

      public void Step(Entity[] entities, cDouble dt) {
         CalculateComputedStatsAndEnsureLocalizationValid(entities);

         var terrainSnapshot = terrainFacade.CompileSnapshot();
         var swarmAndRadiusToEntityAndLocalization = SwarmAndRadius_To_EntityAndLocalization(entities, terrainSnapshot);
         var swarmAndRadiusToPrcAndIndices = ComputeSwarmAndRadiusToEntityTriangleCentroidPaths(terrainSnapshot, swarmAndRadiusToEntityAndLocalization);
         var centroidOptimalContinuousContribution = ContributeTriangleCentroidOptimalContinuousPathForceContributions(entities, swarmAndRadiusToEntityAndLocalization, swarmAndRadiusToPrcAndIndices);
         var centroidNonoptimalDiscreteAndCentroidDirectContribution = ContributeTriangleCentroidNonoptimalDiscreteSpanningDijkstrasAndCentroidDirectPathSteeringContribution(entities);
         var aggregate = AggregateForceContributions(centroidOptimalContinuousContribution, centroidNonoptimalDiscreteAndCentroidDirectContribution);
         ApplyForceContributions(entities, aggregate, dt);

         foreach (var entity in entities) {
            ref var mci = ref entity.MotionComponent.Internals;

            if (mci.Steering.Status == FlockingStatus.EnabledInvalidatedRoadmap) {
               var prc = pathfinderCalculator.UniformCostSearch(
                  mci.ComputedStatistics.Radius,
                  mci.Pose.WorldPosition,
                  new []{ mci.Steering.Destination },
                  false);
               if (!prc.TryComputeRoadmap(0, out var roadmap)) {
                  continue;
               }
               mci.Steering.Status = FlockingStatus.EnabledExecutingRoadmap;
               mci.Steering.Roadmap = roadmap;
               mci.Steering.RoadmapProgressIndex = 0;
            }

            if (mci.Swarm == null && mci.Steering.Status == FlockingStatus.EnabledExecutingRoadmap) {
               // jank
               var nspuc = new NonSwarmerPositionUpdateCalculator();
               var (npos, nrpi) = nspuc.CalculateRoadmapPositionUpdate(
                  mci.Pose,
                  mci.Steering.Roadmap,
                  mci.Steering.RoadmapProgressIndex,
                  mci.ComputedStatistics.Speed * dt);
               mci.Pose = npos;
               mci.Steering.RoadmapProgressIndex = nrpi;
               if (nrpi == mci.Steering.Roadmap.Plan.Count) {
                  mci.Steering.Roadmap = null;
                  mci.Steering.Status = FlockingStatus.EnabledIdle;
               }
            }
         }
      }

      private void CalculateComputedStatsAndEnsureLocalizationValid(Entity[] entities) {
         foreach (var entity in entities) {
            var mc = entity.MotionComponent;
            var statistics = mc.Internals.ComputedStatistics = statisticsCalculator.CalculateMotionStatistics(entity);
            // HACK: Always relocalize until we support updating localization.
            if (true || mc.Internals.IsLocalizationInvalidated) {
               mc.Internals.IsLocalizationInvalidated = false;
               var terrainOverlayNetwork = terrainFacade.CompileSnapshotAndTerrainOverlayNetwork((cDouble)statistics.Radius);
               var res = terrainOverlayNetwork.FindNearestLandPointLocalization(mc.Internals.Pose.WorldPosition, statistics.Radius);
               // Console.WriteLine("Relocalize " + mc.Internals.Pose.WorldPosition + " => " + res.world);
               mc.Internals.Pose.WorldPosition = res.world;
               mc.Internals.Localization = res.localization;
            }
         }
      }

      private ExposedArrayListMultiValueDictionary<(Swarm, int), (int, Entity, TerrainOverlayNetworkNode, TriangulationIsland, int)> 
         SwarmAndRadius_To_EntityAndLocalization(
            Entity[] entities, 
            TerrainSnapshot terrainSnapshot
      ) {
         var nodeIslandAndTriangleIndexesBySwarmAndComputedRadius = new ExposedArrayListMultiValueDictionary<(Swarm, int), (int, Entity, TerrainOverlayNetworkNode, TriangulationIsland, int)>();

         for (var i = 0; i < entities.Length; i++) {
            var e = entities[i];
            var mc = e.MotionComponent;
            // mc.Localization = LocalizeEntityAndFixOutOfTriangulationBounds(e);

            if (mc.Internals.Swarm == null) continue;

            nodeIslandAndTriangleIndexesBySwarmAndComputedRadius.Add(
               (mc.Internals.Swarm, mc.Internals.ComputedStatistics.Radius),
               (i, e, mc.Internals.Localization.TerrainOverlayNetworkNode, mc.Internals.Localization.TriangulationIsland, mc.Internals.Localization.TriangleIndex));
         }

         return nodeIslandAndTriangleIndexesBySwarmAndComputedRadius;
      }

      private Dictionary<(Swarm swarm, int computedRadius), (PathfinderResultContext pathfinderResultContext, int[] centroidIndicesByEntityIndex)> 
         ComputeSwarmAndRadiusToEntityTriangleCentroidPaths(
            TerrainSnapshot terrainSnapshot, 
            ExposedArrayListMultiValueDictionary<(Swarm, int), (int, Entity, TerrainOverlayNetworkNode, TriangulationIsland, int)> nodeIslandAndTriangleIndexesBySwarmAndComputedRadius
         ) {
         var swarmAndRadiusToEntityTriangleCentroidPaths = new Dictionary<(Swarm swarm, int computedRadius), (PathfinderResultContext pathfinderResultContext, int[] centroidIndicesByEntityIndex)>();

         foreach (var ((swarm, computedRadius), entityNodeAndTriangleIndexes) in nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
            var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork((cDouble)computedRadius);
            if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(swarm.Destination, out var destinationNode, out var destinationLocal)) {
               continue;
            }

            var swarmTriangleCentroids = new AddOnlyOrderedHashSet<(TerrainOverlayNetworkNode, IntVector2)>();
            var centroidIndices = new int[entityNodeAndTriangleIndexes.Count];
            for (var j = 0; j < entityNodeAndTriangleIndexes.Count; j++) {
               var (i, e, node, island, triangleIndex) = entityNodeAndTriangleIndexes[j];
               var centroid = island.Triangles[triangleIndex].Centroid.LossyToIntVector2();
               swarmTriangleCentroids.TryAdd((node, centroid), out var centroidIndex);
               centroidIndices[j] = centroidIndex;
            }

            var priorPathfinderResultContext = swarm.GetPriorPathfinderResultContextOrNull(computedRadius, destinationNode);
            var pathfinderResultContext = pathfinderCalculator.UniformCostSearch(
               (destinationNode, new IntVector2((int)destinationLocal.X, (int)destinationLocal.Y)),
               swarmTriangleCentroids.ToArray(),
               true,
               priorPathfinderResultContext);
            swarm.SetPriorPathfinderResultContext(computedRadius, destinationNode, pathfinderResultContext);

            swarmAndRadiusToEntityTriangleCentroidPaths.Add((swarm, computedRadius), (pathfinderResultContext, centroidIndices));
         }

         return swarmAndRadiusToEntityTriangleCentroidPaths;
      }

      private DoubleVector2[] ContributeTriangleCentroidOptimalContinuousPathForceContributions(
         Entity[] entities,
         ExposedArrayListMultiValueDictionary<(Swarm, int), (int, Entity, TerrainOverlayNetworkNode, TriangulationIsland, int)> nodeIslandAndTriangleIndexesBySwarmAndComputedRadius, 
         Dictionary<(Swarm swarm, int computedRadius), (PathfinderResultContext pathfinderResultContext, int[] centroidIndicesByEntityIndex)> swarmAndRadiusToEntityTriangleCentroidPaths
      ) {
         var contributions = new DoubleVector2[entities.Length];
         foreach (var ((swarm, computedRadius), idEntityNodeAndTriangleIndexes) in nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
            if (!swarmAndRadiusToEntityTriangleCentroidPaths.TryGetValue((swarm, computedRadius), out var res)) continue;
            var (pathfinderResultContext, centroidIndicesByEntityIndex) = res;
            // foreach (var (j, entity, entityTerrainOverlayNode, island, triangleIndex) in idEntityNodeAndTriangleIndexes) {
            for (var i = 0; i < idEntityNodeAndTriangleIndexes.Count; i++) {
               var (j, entity, entityTerrainOverlayNode, island, triangleIndex) = idEntityNodeAndTriangleIndexes[i];
               var mc = entity.MotionComponent;
               ref var steering = ref mc.Internals.Steering;

               if (steering.Status == FlockingStatus.EnabledIdle) continue;

               var centroidIndex = centroidIndicesByEntityIndex[i];
               if (pathfinderResultContext.TryComputeRoadmap(centroidIndex, out var roadmap)) {
                  // path-following behavior. Recall from destination to source, so roadmap must be followed backward.
                  var action = (MotionRoadmapWalkAction)roadmap.Plan.Last();
                  Trace.Assert(action.Node == entityTerrainOverlayNode);

                  // HACK:
                  if ((mc.Internals.Pose.WorldPosition - mc.Internals.Swarm.Destination).Norm2D() < CDoubleMath.c5) {
                     steering.Status = FlockingStatus.EnabledIdle;
                  }

                  // path-following vector is from destination to source because our multi-pathfind goes from destination to source.
                  contributions[j] = action.DestinationToSourceUnit;
               }
            }
         }
         return contributions;
      }

      private (bool, DoubleVector2)[] ContributeTriangleCentroidNonoptimalDiscreteSpanningDijkstrasAndCentroidDirectPathSteeringContribution(Entity[] entities) {
         var res = new (bool, DoubleVector2)[entities.Length];
         var islandAndGoalToRootTriangleIndex = new Dictionary<(TriangulationIsland, DoubleVector3), (int rootTriangleIndex, int[] predecessorByTriangleIndex, DoubleVector3 loc)>();
         for (var i = 0; i < entities.Length; i++) {
            var mc = entities[i].MotionComponent;
            if (mc.Internals.Swarm == null) continue;
            if (mc.Internals.Steering.Status == FlockingStatus.EnabledIdle) continue;

            var key = (mc.Internals.Localization.TriangulationIsland, mc.Internals.Swarm.Destination);
            if (!islandAndGoalToRootTriangleIndex.TryGetValue(key, out var t)) {
               if (mc.Internals.Localization.TerrainOverlayNetworkNode.Contains(mc.Internals.Swarm.Destination, out var local)) {
                  // Contains can work but NN fail due to point-in-triangle robustness issues. TODO: Is this comment stale?
                  NN(mc.Internals.Localization.TriangulationIsland, local.XY, out var tt);
                  islandAndGoalToRootTriangleIndex[key] = t = (tt.rootTriangleIndex, tt.predecessorByTriangleIndex, local);
               }
               // TODO: Should still cache something if fail - how do we handle case where pos/dest on different islands?
            }

            if (t.predecessorByTriangleIndex != null) {
               var nti = t.predecessorByTriangleIndex[mc.Internals.Localization.TriangleIndex];
               if (nti != Triangle3.NO_NEIGHBOR_INDEX) {
                  DoubleVector2 next = mc.Internals.Localization.TriangulationIsland.Triangles[nti].Centroid;

                  var triangleCentroidDijkstrasOptimalSeekUnit = (next - mc.Internals.Localization.TriangulationIsland.Triangles[mc.Internals.Localization.TriangleIndex].Centroid).ToUnit();
                  res[i] = (false, triangleCentroidDijkstrasOptimalSeekUnit);
               } else {
                  // we're on the destination triangle.
                  res[i] = (true, mc.Internals.Localization.LocalPosition.To(t.loc.XY).ToUnit());
               }
            }
         }
         return res;
      }

      private bool NN(TriangulationIsland island, DoubleVector2 destination, out (int rootTriangleIndex, int[] predecessorByTriangleIndex) res) {
         if (!island.TryIntersect(destination.X, destination.Y, out var rootTriangleIndex)) {
            res = (Triangle3.NO_NEIGHBOR_INDEX, null);
            return false;
         }

         var prior = new int[island.Triangles.Length];
         var costUpperBounds = new cDouble[island.Triangles.Length];
         for (var i = 0; i < island.Triangles.Length; i++) {
            prior[i] = int.MinValue;
            costUpperBounds[i] = cDouble.MaxValue;
         }

         var q = new PriorityQueue<(cDouble, int, int)>((a, b) => a.Item1.CompareTo(b.Item1));
         q.Enqueue((CDoubleMath.c0, rootTriangleIndex, -1));
         while (q.Count > 0) {
            var (ticost, ticur, tiprev) = q.Dequeue();
            if (prior[ticur] != int.MinValue) continue;
            prior[ticur] = tiprev;

            for (var i = 0; i < 3; i++) {
               var nti = island.Triangles[ticur].NeighborOppositePointIndices[i];
               if (nti == Triangle3.NO_NEIGHBOR_INDEX) continue;

               var edgeCost = (island.Triangles[nti].Centroid - island.Triangles[ticur].Centroid).Norm2D();
               var ntiCost = ticost + edgeCost;
               if (costUpperBounds[nti] <= ntiCost) continue;
               costUpperBounds[nti] = ntiCost;
               q.Enqueue((ntiCost, nti, ticur));
            }
         }

         res = (rootTriangleIndex, prior);
         return true;
      }

      private DoubleVector2[] AggregateForceContributions(DoubleVector2[] centroidSeekContinuous, (bool, DoubleVector2)[] centroidNonoptimalDiscreteAndCentroidDirectContribution) {
         Assert.Equals(centroidSeekContinuous.Length, centroidNonoptimalDiscreteAndCentroidDirectContribution.Length);

         var res = new DoubleVector2[centroidSeekContinuous.Length];
         for (var i = 0; i < res.Length; i++) {
            var csc = centroidSeekContinuous[i];
            var (isOnGoalTriangle, csd) = centroidNonoptimalDiscreteAndCentroidDirectContribution[i];
            if (isOnGoalTriangle) {
               res[i] = csd;
            } else {
               res[i] = csc * CDoubleMath.c0_8 + csd * CDoubleMath.c0_2;
            }
         }
         return res;
      }

      private void ApplyForceContributions(Entity[] entities, DoubleVector2[] contributions, cDouble dt) {
         Assert.Equals(entities.Length, contributions.Length);
         for (var i = 0; i < entities.Length; i++) {
            var e = entities[i];
            var mc = e.MotionComponent;
            var v = contributions[i];
            var pNext = triangulationWalker.WalkTriangulation(
               mc.Internals.Localization.TriangulationIsland,
               mc.Internals.Localization.TriangleIndex,
               mc.Internals.Localization.LocalPosition,
               v,
               e.MotionComponent.Internals.ComputedStatistics.Speed * dt * mc.Internals.Localization.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor);
            mc.Internals.Localization.LocalPosition = pNext;
            mc.Internals.Localization.LocalPositionIv2 = pNext.LossyToIntVector2();
            var tonn = mc.Internals.Localization.TerrainOverlayNetworkNode;
            mc.Internals.Pose.WorldPosition = tonn.SectorNodeDescription.LocalToWorld(pNext);
            mc.Internals.Pose.LookAt = tonn.SectorNodeDescription.LocalToWorldNormal(new DoubleVector3(v));
         }
      }
   }
}