using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.ECS {
   public class SteeringXYZ {
      public bool NN(TriangulationIsland island, DoubleVector2 destination, out (int rootTriangleIndex, int[] predecessorByTriangleIndex) res) {
         int rootTriangleIndex;
         if (!island.TryIntersect(destination.X, destination.Y, out rootTriangleIndex)) {
            res = (Triangle3.NO_NEIGHBOR_INDEX, null);
            return false;
         }

         var prior = new int[island.Triangles.Length];
         var costUpperBounds = new Double[island.Triangles.Length];
         for (var i = 0; i < island.Triangles.Length; i++) {
            prior[i] = Triangle3.NO_NEIGHBOR_INDEX;
            costUpperBounds[i] = Double.MaxValue;
         }

         var q = new PriorityQueue<(Double, int, int)>((a, b) => a.Item1.CompareTo(b.Item1));
         q.Enqueue((CDoubleMath.c0, rootTriangleIndex, -1));
         while (q.Count > 0) {
            var (ticost, ticur, tiprev) = q.Dequeue();
            if (prior[ticur] != -1) continue;
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

      public void Execute() {
         var entities = AssociatedEntities.ToArray();
         var terrainSnapshot = terrainFacade.CompileSnapshot();
         var movementComponents = entities.Map(e => e.MotionComponent);

         // 0. Precompute computed entity stats, zero flocking intermediate aggregates
         Execute_ZeroMovementComponentCountersAndUpdateLastCounters(movementComponents, entities);

         // 1. Determine Terrain Overlay Network Nodes entities are sitting on (group by)
         // additionally determine local position, triangulation island, and triangle index.
         var nodeIslandAndTriangleIndexesBySwarmAndComputedRadius = Execute_ComputeEntityOverlayNetworkPlacementAndGroupBySwarmsAndRadius(entities, terrainSnapshot);

         // 2. Find PathfinderResultContext pathing from swarm triangles to destination,
         //    Additionally yields for each swarm/computedRadius group, yields centroid index for each entity within group to PRC destinations.
         RenderMe = new List<PathfinderResultContext>();
         var swarmAndRadiusToEntityTriangleCentroidPaths = Execute_ComputeSwarmAndRadiusToEntityTriangleCentroidPaths(terrainSnapshot, nodeIslandAndTriangleIndexesBySwarmAndComputedRadius);

         // 3. For each entity, contribute path-following steering behavior
         Execute_ContributeTriangleCentroidOptimalContinuousPathSteeringBehavior(nodeIslandAndTriangleIndexesBySwarmAndComputedRadius, swarmAndRadiusToEntityTriangleCentroidPaths);

         // 3.1 For each (island, dest) compute spanning dijkstras of tree centroids to dest triangle
         Execute_ContributeTriangleCentroidOptimalDiscreteSpanningDijkstrasPathSteeringBehavior(movementComponents);

         // 4. for each entity pairing, compute separation force vector which prevents overlap
         //    and "regroup" (cohesion) force vector, which causes clustering within swarms.
         //    Logic contained within should be scale invariant!
         Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors(movementComponents);

         // 5. Aggregate entity force contributions
         foreach (var mc in movementComponents) {
            Double ks = (Double)2000;
            mc.Steering.CurrentUpdateForceContributions.Aggregate.SumForces += mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces * ks; // is normalized
            mc.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights += ks;

            if (mc.Steering.CurrentUpdateForceContributions.Alignment.SumWeights > CDoubleMath.c0) {
               Double kc = (Double)1500;
               mc.Steering.CurrentUpdateForceContributions.Aggregate.SumForces += (mc.Steering.CurrentUpdateForceContributions.Alignment.SumForces / mc.Steering.CurrentUpdateForceContributions.Alignment.SumWeights) * kc;
               mc.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights += kc;
            }
         }

         // 6. Apply entity force contributions if swarmers, else follow optimal path.
         foreach (var entity in entities) {
            var mc = entity.MotionComponent;

            var repath = mc.Steering.IsRoadmapInvalidated || (
               mc.Steering.LastFailedPathfindingSnapshot != null &&
               mc.Steering.LastFailedPathfindingSnapshot != terrainFacade.CompileSnapshot());

            if (repath) {
               Pathfind(entity, mc.Steering.Destination);
            }

            if (mc.Swarm == null) {
               ExecutePathNonswarmer(entity, mc);
            } else {
               ExecutePathSwarmer(entity, mc);
            }
         }

         void Execute_ZeroMovementComponentCountersAndUpdateLastCounters(MotionComponent[] movementComponents1, Entity[] entities1) {
            for (var i = 0; i < movementComponents1.Length; i++) {
               var e = entities1[i];
               var mc = movementComponents1[i];
               mc.ComputedStatistics = statsCalculator.CalculateMotionStatistics(e);

               mc.Steering.LastUpdateForceContributions = mc.Steering.CurrentUpdateForceContributions;
               mc.Steering.CurrentUpdateForceContributions = default;
            }
         }
      }

      private MultiValueDictionary<(Swarm, int), (Entity, TerrainOverlayNetworkNode, TriangulationIsland, int)> Execute_ComputeEntityOverlayNetworkPlacementAndGroupBySwarmsAndRadius(Entity[] entities, TerrainSnapshot terrainSnapshot) {
         var terrainOverlayNetworkNodes = new HashSet<TerrainOverlayNetworkNode>();
         var nodeIslandAndTriangleIndexesBySwarmAndComputedRadius = MultiValueDictionary<(Swarm, int), ValueTuple<Entity, TerrainOverlayNetworkNode, TriangulationIsland, int>>.Create(() => new List<ValueTuple<Entity, TerrainOverlayNetworkNode, TriangulationIsland, int>>());
         foreach (var e in entities) {
            var mc = e.MotionComponent;

            mc.Localization = LocalizeEntityAndFixOutOfTriangulationBounds(e);
            terrainOverlayNetworkNodes.Add(mc.Localization.TerrainOverlayNetworkNode);

            if (mc.Swarm == null) continue;

            nodeIslandAndTriangleIndexesBySwarmAndComputedRadius.Add(
               (mc.Swarm, mc.ComputedStatistics.Radius),
               (e, mc.Localization.TerrainOverlayNetworkNode, mc.Localization.TriangulationIsland, mc.Localization.TriangleIndex));
         }

         return nodeIslandAndTriangleIndexesBySwarmAndComputedRadius;
      }

      private Dictionary<(Swarm swarm, int computedRadius), (PathfinderResultContext pathfinderResultContext, int[] centroidIndicesByEntityIndex)> Execute_ComputeSwarmAndRadiusToEntityTriangleCentroidPaths(TerrainSnapshot terrainSnapshot, MultiValueDictionary<(Swarm, int), (Entity, TerrainOverlayNetworkNode, TriangulationIsland, int)> nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
         var swarmAndRadiusToEntityTriangleCentroidPaths = new Dictionary<(Swarm swarm, int computedRadius), (PathfinderResultContext pathfinderResultContext, int[] centroidIndicesByEntityIndex)>();

         foreach (var ((swarm, computedRadius), entityNodeAndTriangleIndexes) in nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
            var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork((Double)computedRadius);
            if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(swarm.Destination, out var destinationNode, out var destinationLocal)) {
               continue;
            }

            var swarmTriangleCentroids = new AddOnlyOrderedHashSet<(TerrainOverlayNetworkNode, IntVector2)>();
            var centroidIndices = new int[entityNodeAndTriangleIndexes.Count];
            foreach (var (i, (e, node, island, triangleIndex)) in entityNodeAndTriangleIndexes.Enumerate()) {
               var centroid = island.Triangles[triangleIndex].Centroid.LossyToIntVector2();
               swarmTriangleCentroids.TryAdd((node, centroid), out var centroidIndex);
               centroidIndices[i] = centroidIndex;
            }

            var priorPathfinderResultContext = swarm.GetPriorPathfinderResultContextOrNull(computedRadius, destinationNode);
            var pathfinderResultContext = pathfinderCalculator.UniformCostSearch(
               (destinationNode, new IntVector2((int)destinationLocal.X, (int)destinationLocal.Y)),
               swarmTriangleCentroids.ToArray(),
               true,
               priorPathfinderResultContext);
            swarm.SetPriorPathfinderResultContext(computedRadius, destinationNode, pathfinderResultContext);

            swarmAndRadiusToEntityTriangleCentroidPaths.Add((swarm, computedRadius), (pathfinderResultContext, centroidIndices));
            RenderMe.Add(pathfinderResultContext);
         }

         return swarmAndRadiusToEntityTriangleCentroidPaths;
      }

      private void Execute_ContributeTriangleCentroidOptimalContinuousPathSteeringBehavior(MultiValueDictionary<(Swarm, int), (Entity, TerrainOverlayNetworkNode, TriangulationIsland, int)> nodeIslandAndTriangleIndexesBySwarmAndComputedRadius, Dictionary<(Swarm swarm, int computedRadius), (PathfinderResultContext pathfinderResultContext, int[] centroidIndicesByEntityIndex)> swarmAndRadiusToEntityTriangleCentroidPaths) {
         foreach (var ((swarm, computedRadius), entityNodeAndTriangleIndexes) in nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
            if (!swarmAndRadiusToEntityTriangleCentroidPaths.TryGetValue((swarm, computedRadius), out var res)) continue;
            var (pathfinderResultContext, centroidIndicesByEntityIndex) = res;
            foreach (var (i, (entity, entityTerrainOverlayNode, island, triangleIndex)) in entityNodeAndTriangleIndexes.Enumerate()) {
               var mc = entity.MotionComponent;
               ref var steering = ref mc.Steering;

               if (steering.IsDestinationReached) continue;

               var centroidIndex = centroidIndicesByEntityIndex[i];
               if (pathfinderResultContext.TryComputeRoadmap(centroidIndex, out var roadmap)) {
                  // path-following behavior. Recall from destination to source, so roadmap must be followed backward.
                  var action = (MotionRoadmapWalkAction)roadmap.Plan.Last();
                  Trace.Assert(action.Node == entityTerrainOverlayNode);

                  // HACK:
                  if ((mc.Pose.WorldPosition - mc.Swarm.Destination).Norm2D() < CDoubleMath.c5) {
                     steering.IsDestinationReached = true;
                  }

                  // path-following vector is from destination to source because our multi-pathfind goes from destination to source.
                  var v = action.Destination.To(action.Source).ToDoubleVector2().ToUnit();
                  var w = CDoubleMath.c1;
                  steering.CurrentUpdateForceContributions.Seeking.SumForces += v * w;
                  steering.CurrentUpdateForceContributions.Seeking.SumWeights += w;
               }
            }
         }
      }

      private void Execute_ContributeTriangleCentroidOptimalDiscreteSpanningDijkstrasPathSteeringBehavior(MotionComponent[] motionComponents) {
         var islandAndGoalToRootTriangleIndex = new Dictionary<(TriangulationIsland, DoubleVector3), (int rootTriangleIndex, int[] predecessorByTriangleIndex, DoubleVector3 loc)>();
         foreach (var mc in motionComponents) {
            if (mc.Swarm == null) continue;
            if (mc.Steering.IsDestinationReached) continue;

            var key = (mc.Localization.TriangulationIsland, mc.Swarm.Destination);
            if (!islandAndGoalToRootTriangleIndex.TryGetValue(key, out var t)) {
               if (mc.Localization.TerrainOverlayNetworkNode.Contains(mc.Swarm.Destination, out var local)) {
                  // Contains can work but NN fail due to point-in-triangle robustness issues.
                  NN(mc.Localization.TriangulationIsland, local.XY, out var tt);
                  islandAndGoalToRootTriangleIndex[key] = t = (tt.rootTriangleIndex, tt.predecessorByTriangleIndex, local);
               }
            }

            if (t.predecessorByTriangleIndex != null) {
               var nti = t.predecessorByTriangleIndex[mc.Localization.TriangleIndex];
               if (nti != Triangle3.NO_NEIGHBOR_INDEX) {
                  DoubleVector2 next = mc.Localization.TriangulationIsland.Triangles[nti].Centroid;

                  var triangleCentroidDijkstrasOptimalSeekUnit = (next - mc.Localization.TriangulationIsland.Triangles[mc.Localization.TriangleIndex].Centroid).ToUnit();

                  Double mul = CDoubleMath.c0_8;
                  mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces += mul * triangleCentroidDijkstrasOptimalSeekUnit;
                  mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights += mul;
               }
            }

            // Normalize seek forces now that we've done centroid path-follow and optimal heuristics.
            if (mc.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights > CDoubleMath.c0) {
               // TODO: This seems like a bugged normalization including test above?
               Debugger.Break();
               mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces = mc.Steering.CurrentUpdateForceContributions.Aggregate.SumForces.ToUnit();
               mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights = CDoubleMath.c1;
            }

            Double k = (Double)Math.Min(gameTimeManager.Ticks, 19);
            mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces += mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces * k;
            mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights += mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights * k;

            mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces /= k + CDoubleMath.c1;
            mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights /= k + CDoubleMath.c1;
         }
      }

      private static Double[] Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isOverlappingWeightLut;
      private static Double[] Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isNonOverlappingWeightLut;

      private static void Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors(MotionComponent[] motionComponents) {
         for (var i = 0; i < motionComponents.Length - 1; i++) {
            var a = motionComponents[i];
            var aRadius = a.ComputedStatistics.Radius;
            for (var j = i + 1; j < motionComponents.Length; j++) {
               var b = motionComponents[j];
               var aToB = b.Localization.LocalPositionIv2 - a.Localization.LocalPositionIv2;

               var radiusSum = (int)((Double)(aRadius + b.ComputedStatistics.Radius) * a.Localization.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor);
               var radiusSumSquared = radiusSum * radiusSum;
               var centerDistanceSquared = aToB.SquaredNorm2();

               // Must either be overlapping or in the same swarm for us to compute
               // (In the future rather than "in same swarm" probably want "allied".
               var isOverlapping = centerDistanceSquared < radiusSumSquared;

               Double w; // where 1 means equal in weight to isolated-unit pather
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
                                   Double k = (Double)350;

                                   // w = k * k * (CDoubleMath.c1 - CDoubleMath.Pow((cDouble)centerDistance / (cDouble)radiusSum, CDoubleMath.c0_3)); // / (double)radiusSumSquared;
                                   return k * k * (CDoubleMath.c1 - Frac01Lut.Pow0_3(paramCenterDistance, paramRadiusSum));
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
               } else if (a.Swarm == b.Swarm && a.Swarm != null) {
                  // Case: Nonoverlapping, in same swarm. Push swarmlings near but nonoverlapping
                  // TODO: Alignment force.
                  const int groupingTolerance = 8;
                  var spacingBetweenBoundaries = IntMath.Sqrt(checked((int)centerDistanceSquared)) - radiusSum;
                  var maxAttractionDistance = radiusSum * groupingTolerance;

                  if (spacingBetweenBoundaries > maxAttractionDistance)
                     continue;

                  // regroup (aka cohesion) = ((D - d) / D)^4 
                  // w = CDoubleMath.c10 * CDoubleMath.Pow((cDouble)(maxAttractionDistance - spacingBetweenBoundaries) / (cDouble)maxAttractionDistance, CDoubleMath.c0_5);
                  // w = CDoubleMath.c10 * CDoubleMath.Sqrt((cDouble)(maxAttractionDistance - spacingBetweenBoundaries) / (cDouble)maxAttractionDistance);

                  var wLut = Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isNonOverlappingWeightLut ??
                             (Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isNonOverlappingWeightLut =
                                Frac01Lut.BuildLut((maxAttractionDistsanceMinusSpacingBetweenBoundariesParam, maxAttractionDistanceParam) => {
                                   return CDoubleMath.c10 * Frac01Lut.Pow0_5(maxAttractionDistsanceMinusSpacingBetweenBoundariesParam, maxAttractionDistanceParam);
                                }));
                  w = Frac01Lut.Lookup(wLut, maxAttractionDistance - spacingBetweenBoundaries, maxAttractionDistance);
                  Debug.Assert(GeometryOperations.IsReal(w));

                  aForce = aToB;

                  // alignment
                  a.Steering.CurrentUpdateForceContributions.Alignment.SumForces += b.Steering.LastUpdateForceContributions.Seeking.SumForces * CDoubleMath.c0_01;
                  a.Steering.CurrentUpdateForceContributions.Alignment.SumWeights += b.Steering.LastUpdateForceContributions.Seeking.SumWeights * CDoubleMath.c0_01;

                  b.Steering.CurrentUpdateForceContributions.Alignment.SumForces += a.Steering.LastUpdateForceContributions.Seeking.SumForces * CDoubleMath.c0_01;
                  b.Steering.CurrentUpdateForceContributions.Alignment.SumWeights += a.Steering.LastUpdateForceContributions.Seeking.SumWeights * CDoubleMath.c0_01;
               } else {
                  // todo: experiment with continue vs zero-weight for no failed branch prediction
                  // (this is pretty pipeliney code)
                  continue;
               }


               // slow due to fix64 sqrt
               //var wf = w * aForce.ToDoubleVector2().ToUnit();
               var aForceMagSquared = aForce.SquaredNorm2();
               var aForceMag = (Double)IntMath.Sqrt(checked((int)aForceMagSquared));
               var wf = new DoubleVector2((w * (Double)aForce.X) / aForceMag, (w * (Double)aForce.Y) / aForceMag);

               Debug.Assert(GeometryOperations.IsReal(wf));
               Debug.Assert(GeometryOperations.IsReal(w));

               a.Steering.CurrentUpdateForceContributions.Aggregate.SumForces += wf;// * (cDouble)0.95;
               a.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights += w;// * (cDouble)0.95;

               b.Steering.CurrentUpdateForceContributions.Aggregate.SumForces -= wf;
               b.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights += w;
            }
         }
      }
   }
}