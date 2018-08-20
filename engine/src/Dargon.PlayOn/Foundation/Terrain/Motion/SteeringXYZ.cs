using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dargon.Commons;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class FlockingSimulator {
      private readonly EntityGridFacade entityGridFacade;
      private readonly StatisticsCalculator statisticsCalculator; // TODO: Feels out of place in this class.
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly TerrainFacade terrainFacade;
      private readonly TriangulationWalker triangulationWalker;

      public FlockingSimulator(EntityGridFacade entityGridFacade, StatisticsCalculator statisticsCalculator, PathfinderCalculator pathfinderCalculator, TerrainFacade terrainFacade, TriangulationWalker triangulationWalker) {
         this.entityGridFacade = entityGridFacade;
         this.statisticsCalculator = statisticsCalculator;
         this.pathfinderCalculator = pathfinderCalculator;
         this.terrainFacade = terrainFacade;
         this.triangulationWalker = triangulationWalker;
      }

      public void Step(Entity[] entities, cDouble dt) {
         CalculateComputedStatsAndEnsureLocalizationValid(entities);

         var gridViews = entityGridFacade.CreateGridViews(entities);

         var terrainSnapshot = terrainFacade.CompileSnapshot();
         var swarmAndRadiusToEntityAndLocalization = SwarmAndRadius_To_EntityAndLocalization(entities, terrainSnapshot);
         var swarmAndRadiusToPrcAndIndices = ComputeSwarmAndRadiusToEntityTriangleCentroidPaths(terrainSnapshot, swarmAndRadiusToEntityAndLocalization);
         var centroidOptimalContinuousContribution = CalculateTriangleCentroidOptimalContinuousPathForceContributions(entities, swarmAndRadiusToEntityAndLocalization, swarmAndRadiusToPrcAndIndices);
         var centroidNonoptimalDiscreteAndCentroidDirectContribution = CalculateTriangleCentroidNonoptimalDiscreteSpanningDijkstrasAndCentroidDirectPathSteeringContribution(entities);
         var steeringContribution = CalculateMergedSeekContributions(centroidOptimalContinuousContribution, centroidNonoptimalDiscreteAndCentroidDirectContribution);
         var cohesionSeparationContributions = CalculateCohesionSeparationContributions(entities, gridViews);
         var alignmentContributions = CalculateAlignmentContributions(entities, gridViews, steeringContribution);

         var aggregate = AggregateForceContributions(entities, centroidOptimalContinuousContribution, centroidNonoptimalDiscreteAndCentroidDirectContribution, cohesionSeparationContributions, alignmentContributions);
         ApplyForceContributions(entities, aggregate, dt);

         foreach (var entity in entities) {
            ref var mci = ref entity.MotionComponent.Internals;

            if (mci.Steering.Status == FlockingStatus.EnabledInvalidatedRoadmap) {
               var prc = pathfinderCalculator.UniformCostSearch(
                  mci.ComputedStatistics.Radius,
                  mci.Pose.WorldPosition,
                  new []{ mci.Steering.Destination },
                  false);
               if (prc == null || !prc.TryComputeRoadmap(0, out var roadmap)) {
                  // prc null if src or dest can't be localized.
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

      private static cDouble[] Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isOverlappingWeightLut;
      private static cDouble[] Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isNonOverlappingWeightLut;

      private DoubleVector2[] CalculateCohesionSeparationContributions(Entity[] entities, Dictionary<SectorNodeDescription, EntityGridView> gridViews) {
         foreach (var entity in entities) {
            var mc = entity.MotionComponent;
            if (mc == null) continue;
            mc.Internals.Hack_CohesionSeparationVector = DoubleVector2.Zero;
         }

         foreach (var (snd, view) in gridViews) {
            var grid = view.Grid;
            foreach (var (itx, ity) in grid.Occupancy) {
               var head = grid.Cells[ity, itx];
               for (var it1 = head; it1 != null; it1 = it1.Next) {
                  var mc1 = it1.Entity.MotionComponent;
                  foreach (var (nid, neighbor) in view.InQuarterCircleBRExcludeCenter(itx, ity, (int)(mc1.Internals.ComputedStatistics.Radius * snd.WorldToLocalScalingFactor))) {
                     var mc2 = neighbor.MotionComponent;
                     if (TryContribution(mc1, mc2, out var contribution)) {
                        mc1.Internals.Hack_CohesionSeparationVector += contribution.v;
                        mc2.Internals.Hack_CohesionSeparationVector -= contribution.v * 0.9;
                     }
                  }
                  for (var it2 = it1.Next; it2 != null; it2 = it2.Next) {
                     var mc2 = it2.Entity.MotionComponent;
                     if (TryContribution(mc1, mc2, out var contribution)) {
                        mc1.Internals.Hack_CohesionSeparationVector += contribution.v;
                        mc2.Internals.Hack_CohesionSeparationVector -= contribution.v * 0.9;
                     }
                  }
               }
            }
         }

         var res = new DoubleVector2[entities.Length];
         for (var i = 0; i < entities.Length; i++) {
            var entity = entities[i];
            var mc = entity.MotionComponent;
            if (mc == null || mc.Internals.Hack_CohesionSeparationVector == default) continue;
            res[i] = mc.Internals.Hack_CohesionSeparationVector.ToUnit();
         }
         return res;

         bool TryContribution(MotionComponent a, MotionComponent b, out (bool isOverlapping, DoubleVector2 v) contribution) {
            // unpack motion component
            var amci = a.Internals;
            var bmci = b.Internals;
            var aRadius = amci.ComputedStatistics.Radius;
            var bRadius = bmci.ComputedStatistics.Radius;
            ref var aLocalization = ref amci.Localization;
            ref var bLocalization = ref bmci.Localization;

            var aToB = bLocalization.LocalPositionIv2 - aLocalization.LocalPositionIv2;

            var radiusSum = (int)((cDouble)(aRadius + bRadius) * aLocalization.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor);
            var radiusSumSquared = radiusSum * radiusSum;
            var centerDistanceSquared = aToB.SquaredNorm2();

            // Must either be overlapping or in the same swarm for us to compute
            // (In the future rather than "in same swarm" probably want "allied".
            var isOverlapping = centerDistanceSquared < radiusSumSquared;

            cDouble w; // where 1 means equal in weight to isolated-unit pather
            IntVector2 aForce;
            if (isOverlapping) {
               // Case: Overlapping, may or may not be in same swarm.
               // Let D = radius sum
               // Let d = center distance 
               // Separate Force Weight: (k * (D - d) / D)^2
               // Intuitively D-d represents overlapness.
               // k impacts how quickly overlapping overwhelms seeking.
               // k = 1: When fully overlapping
               // k = 2: When half overlapped.
               var wLut = Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isOverlappingWeightLut ??
                          (Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isOverlappingWeightLut =
                             Frac01Lut.BuildLut((paramCenterDistance, paramRadiusSum) => {
                                cDouble k = (cDouble)700;

                                // w = k * k * (CDoubleMath.c1 - CDoubleMath.Pow((cDouble)centerDistance / (cDouble)radiusSum, CDoubleMath.c0_3)); // / (double)radiusSumSquared;
                                return k * k * (0.8 + 0.2 * (CDoubleMath.c1 - Frac01Lut.Pow0_3(paramCenterDistance, paramRadiusSum)));
                             }));

               // var centerDistance = CDoubleMath.Sqrt((cDouble)centerDistanceSquared);
               var centerDistance = IntMath.Sqrt(checked((int)centerDistanceSquared));

               // w = k * k * (CDoubleMath.c1 - CDoubleMath.Pow((cDouble)centerDistance / (cDouble)radiusSum, CDoubleMath.c0_3)); // / (double)radiusSumSquared;
               // w = k * k * (CDoubleMath.c1 - Frac01Lut.Pow0_3(centerDistance, radiusSum));
               w = Frac01Lut.Lookup(wLut, centerDistance, radiusSum);
               Debug.Assert(GeometryOperations.IsReal(w));

               // And the force vector (outer code will tounit this)
               aForce = aToB.SquaredNorm2() == 0
                  ? new IntVector2(2, 1)
                  : -1 * aToB;
            } else if (amci.Swarm == bmci.Swarm && amci.Swarm != null) {
               // Case: Nonoverlapping, in same swarm. Push swarmlings near but nonoverlapping
               // TODO: Alignment force.
               const int groupingTolerance = 8;
               var spacingBetweenBoundaries = IntMath.Sqrt(checked((int)centerDistanceSquared)) - radiusSum;
               var maxAttractionDistance = radiusSum * groupingTolerance;

               if (spacingBetweenBoundaries > maxAttractionDistance) {
                  contribution = default;
                  return false;
               }

               // regroup (aka cohesion) = ((D - d) / D)^4 
               // w = CDoubleMath.c10 * CDoubleMath.Pow((cDouble)(maxAttractionDistance - spacingBetweenBoundaries) / (cDouble)maxAttractionDistance, CDoubleMath.c0_5);
               // w = CDoubleMath.c10 * CDoubleMath.Sqrt((cDouble)(maxAttractionDistance - spacingBetweenBoundaries) / (cDouble)maxAttractionDistance);

               var wLut = Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isNonOverlappingWeightLut ??
                          (Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isNonOverlappingWeightLut =
                             Frac01Lut.BuildLut((maxAttractionDistsanceMinusSpacingBetweenBoundariesParam, maxAttractionDistanceParam) => {
                                return CDoubleMath.c0 * Frac01Lut.Pow1_8(maxAttractionDistsanceMinusSpacingBetweenBoundariesParam, maxAttractionDistanceParam);
                             }));
               w = 0; // Frac01Lut.Lookup(wLut, maxAttractionDistance - spacingBetweenBoundaries, maxAttractionDistance);
               Debug.Assert(GeometryOperations.IsReal(w));

               aForce = aToB;
            } else {
               // todo: experiment with continue vs zero-weight for no failed branch prediction
               // (this is pretty pipeliney code)
               contribution = default;
               return false;
            }


            // slow due to fix64 sqrt
            //var wf = w * aForce.ToDoubleVector2().ToUnit();
            var aForceMagSquared = aForce.SquaredNorm2();
            var aForceMag = (cDouble)IntMath.Sqrt(checked((int)aForceMagSquared));
            var v = new DoubleVector2((w * (cDouble)aForce.X) / aForceMag, (w * (cDouble)aForce.Y) / aForceMag);
            contribution = (isOverlapping, v);
            return true;
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

      private DoubleVector2[] CalculateTriangleCentroidOptimalContinuousPathForceContributions(
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
               
               var centroidIndex = centroidIndicesByEntityIndex[i];
               if (pathfinderResultContext.TryComputeRoadmap(centroidIndex, out var roadmap)) {
                  // path-following behavior. Recall from destination to source, so roadmap must be followed backward.
                  var action = (MotionRoadmapWalkAction)roadmap.Plan.Last();
                  Trace.Assert(action.Node == entityTerrainOverlayNode);

                  // HACK:
                  if ((mc.Internals.Pose.WorldPosition - mc.Internals.Swarm.Destination).Norm2D() < 100) {
                     steering.Status = FlockingStatus.EnabledIdle;
                  }

                  // path-following vector is from destination to source because our multi-pathfind goes from destination to source.
                  contributions[j] = action.DestinationToSourceUnit;
               }
            }
         }
         return contributions;
      }

      private (bool, DoubleVector2)[] CalculateTriangleCentroidNonoptimalDiscreteSpanningDijkstrasAndCentroidDirectPathSteeringContribution(Entity[] entities) {
         var res = new (bool, DoubleVector2)[entities.Length];
         var islandAndGoalToRootTriangleIndex = new Dictionary<(TriangulationIsland, DoubleVector3), (int rootTriangleIndex, int[] predecessorByTriangleIndex, DoubleVector3 loc)>();
         for (var i = 0; i < entities.Length; i++) {
            var mc = entities[i].MotionComponent;
            if (mc.Internals.Swarm == null) continue;

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
               if (nti != Triangle3.NO_NEIGHBOR_INDEX && nti != t.rootTriangleIndex) {
                  DoubleVector2 next = mc.Internals.Localization.TriangulationIsland.Triangles[nti].Centroid;

                  var triangleCentroidDijkstrasOptimalSeekUnit = (next - mc.Internals.Localization.TriangulationIsland.Triangles[mc.Internals.Localization.TriangleIndex].Centroid).ToUnit();
                  res[i] = (false, triangleCentroidDijkstrasOptimalSeekUnit);
               } else {
                  // we're on the destination triangle or going straight to it.
                  res[i] = (true, mc.Internals.Localization.LocalPosition.To(t.loc.XY).ToUnit());
               }
            }
         }
         return res;
      }

      private DoubleVector2[] CalculateMergedSeekContributions(DoubleVector2[] centroidSeekContinuous, (bool, DoubleVector2)[] centroidNonoptimalDiscreteAndCentroidDirectContribution) {
         Assert.Equals(centroidSeekContinuous.Length, centroidNonoptimalDiscreteAndCentroidDirectContribution.Length);
         var res = new DoubleVector2[centroidSeekContinuous.Length];
         for (var i = 0; i < res.Length; i++) {
            var csc = centroidSeekContinuous[i];
            var (isOnGoalTriangle, csd) = centroidNonoptimalDiscreteAndCentroidDirectContribution[i];
            res[i] = isOnGoalTriangle ? csd : csc * CDoubleMath.c0_6 + csd * CDoubleMath.c0_4;
         }
         return res;
      }

      private DoubleVector2[] CalculateAlignmentContributions(Entity[] entities, Dictionary<SectorNodeDescription, EntityGridView> gridViews, DoubleVector2[] steeringContribution) {
         var res = new DoubleVector2[entities.Length];
         foreach (var (snd, view) in gridViews) {
            var grid = view.Grid;
            foreach (var (itx, ity) in grid.Occupancy) {
               var head = grid.Cells[ity, itx];
               for (var it1 = head; it1 != null; it1 = it1.Next) {
                  var mc1 = it1.Entity.MotionComponent;
                  var id1 = it1.EntityIndex;
                  foreach (var (id2, neighbor) in view.InQuarterCircleBRExcludeCenter(itx, ity, (int)(mc1.Internals.ComputedStatistics.Radius * snd.WorldToLocalScalingFactor) * 5)) {
                     res[id1] += steeringContribution[id2];
                     res[id2] += steeringContribution[id1];
                  }
                  for (var it2 = it1.Next; it2 != null; it2 = it2.Next) {
                     var id2 = it2.EntityIndex;
                     res[id1] += steeringContribution[id2];
                     res[id2] += steeringContribution[id1];
                  }
               }
            }
         }
         for (var i = 0; i < res.Length; i++) {
            res[i] = res[i] == default ? default : res[i].ToUnit();
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

      private DoubleVector2[] AggregateForceContributions(Entity[] entities, DoubleVector2[] centroidSeekContinuous, (bool, DoubleVector2)[] centroidNonoptimalDiscreteAndCentroidDirectContribution, DoubleVector2[] cohesionSeparation, DoubleVector2[] alignment) {
         Assert.Equals(centroidSeekContinuous.Length, centroidNonoptimalDiscreteAndCentroidDirectContribution.Length);

         var res = new DoubleVector2[centroidSeekContinuous.Length];
         for (var i = 0; i < res.Length; i++) {
            var csc = centroidSeekContinuous[i];
            var (isOnGoalTriangle, csd) = centroidNonoptimalDiscreteAndCentroidDirectContribution[i];
            var ccs = cohesionSeparation[i];
            var ali = alignment[i];
            Assert.IsTrue(GeometryOperations.IsReal(ali));
            var pushover = entities[i].MotionComponent.Internals.Steering.Status == FlockingStatus.EnabledIdle;

            var seek = isOnGoalTriangle ? csd : csc * CDoubleMath.c0_6 + csd * CDoubleMath.c0_4;
            var swarm = entities[i].MotionComponent.Internals.Swarm;
            cDouble seekAlignWeight;
            if (swarm == null) {
               seekAlignWeight = 1.0;
            } else {
               var d = entities[i].MotionComponent.Internals.Pose.WorldPosition.To(swarm.Destination).Norm2D();
               seekAlignWeight = Math.Max(0.1, Math.Min(1, Math.Pow(d / 200, 1.5)));
            }
            // var v = seek * w + ccs * 2 + ali;
            var wali = Math.Max(0, seek.Dot(ali));
            var v = (seek * (1.0 - wali) + ali * wali) * seekAlignWeight + ccs;

            // if (pushover) {
            //    seek = csd;
            //    var d = entities[i].MotionComponent.Internals.Pose.WorldPosition.To(new DoubleVector3(-50, -50, 0)).Norm2D();
            //    var w = Math.Min(1.4, Math.Pow(d / 200, 1.5));
            //    v = seek * w + ccs;
            //    // var comp = ccs.ProjectOntoComponentD(csc);
            //    // if (comp < 0) {
            //    //    v += ccs;
            //    // }
            // } else {
            //    seek = isOnGoalTriangle ? csd : csc * CDoubleMath.c0_9 + csd * CDoubleMath.c0_1;
            //    v = seek * 1.1 + ccs;
            // }

            res[i] = v == default ? default : v.ToUnit();
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