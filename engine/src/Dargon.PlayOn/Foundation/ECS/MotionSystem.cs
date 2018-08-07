using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.Terrain;
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

namespace Dargon.PlayOn.Foundation.ECS {
   using EntityNodeIslandAndTriangleIndex = ValueTuple<Entity, TerrainOverlayNetworkNode, TriangulationIsland, int>;

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

      public void Pathfind(Entity entity, DoubleVector3 destination) {
         var mc = entity.MotionComponent;
         mc.Steering.Destination = destination;

         var holeDilationRadius = statsCalculator.ComputeCharacterRadius(entity);

         if (pathfinderCalculator.TryFindPath(holeDilationRadius, mc.Pose.WorldPosition, destination, out var roadmap)) {
            mc.Steering.Roadmap = roadmap;
            mc.Steering.IsRoadmapInvalidated = false;
            mc.Steering.RoadmapProgressIndex = 0;
            mc.Steering.LastFailedPathfindingSnapshot = null;
         } else {
            mc.Steering.Roadmap = null;
            mc.Steering.IsRoadmapInvalidated = false;
            mc.Steering.RoadmapProgressIndex = -1;
            mc.Steering.LastFailedPathfindingSnapshot = terrainFacade.CompileSnapshot();
         }
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

      private (DoubleVector3 world, TerrainOverlayNetworkNode node, DoubleVector2 local, TriangulationIsland island, int triangleIndex) FixEntityInHole(Entity entity) {
         var computedRadius = statsCalculator.ComputeCharacterRadius(entity);
         var mc = entity.MotionComponent;
         var res = PushToLand(mc.Pose.WorldPosition, computedRadius);
         mc.Pose.WorldPosition = res.world;
         mc.Localization.TerrainOverlayNetworkNode = res.node;
         mc.Localization.LocalPosition = res.local;
         mc.Localization.LocalPositionIv2 = res.local.LossyToIntVector2();
         mc.Localization.TriangulationIsland = res.island;
         mc.Localization.TriangleIndex = res.triangleIndex;
         return res;
      }

      private (DoubleVector3 world, TerrainOverlayNetworkNode node, DoubleVector2 local, TriangulationIsland island, int triangleIndex) PushToLand(DoubleVector3 pWorld, cDouble computedRadius) {
         var paddedHoleDilationRadius = computedRadius + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius + InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
         var terrainOverlayNetwork = terrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(paddedHoleDilationRadius);
//         Console.WriteLine("PHDR: " + paddedHoleDilationRadius);
         var bestWorldDistance = cDouble.MaxValue;
         var bestWorld = DoubleVector3.Zero;
         var bestLocal = DoubleVector2.Zero;
         TerrainOverlayNetworkNode bestNode = null;
         foreach (var terrainOverlayNode in terrainOverlayNetwork.TerrainNodes) {
            var pLocal = (DoubleVector2)terrainOverlayNode.SectorNodeDescription.WorldToLocal(pWorld);
            terrainOverlayNode.LocalGeometryView.FindNearestLandPointAndIsInHole(pLocal, out var pNearestLocal);

            var pNearestWorld = terrainOverlayNode.SectorNodeDescription.LocalToWorld(pNearestLocal);
            var worldDistance = pWorld.To(pNearestWorld).Norm2D();
            if (worldDistance < bestWorldDistance) {
               bestWorldDistance = worldDistance;
               bestWorld = pNearestWorld;
               bestLocal = pNearestLocal;
               bestNode = terrainOverlayNode;
            }
         }

         if (bestNode == null) throw new InvalidStateException();

         // ensure containment within triangulation
         if (!bestNode.LocalGeometryView.Triangulation.TryIntersect(bestLocal.X, bestLocal.Y, out var island, out var triangleIndex)) {
            throw new NotImplementedException();
         }

         return (bestWorld, bestNode, bestLocal, island, triangleIndex);
         throw new NotImplementedException();
         //         DoubleVector3 nearestLandPoint;
         //         if (!terrainService.BuildSnapshot().FindNearestLandPointAndIsInHole(paddedHoleDilationRadius, vect, out nearestLandPoint)) {
         //            throw new InvalidOperationException("In new hole but not terrain snapshot hole.");
         //         }
         //         return nearestLandPoint;
      }

      /// <summary>
      ///    Invalidates all pathing entities' paths, flagging them for recomputation.
      /// </summary>
      public void InvalidatePaths() {
         foreach (var entity in AssociatedEntities) {
            entity.MotionComponent.Steering.IsRoadmapInvalidated = true;
         }
      }

      public bool NN(TriangulationIsland island, DoubleVector2 destination, out (int rootTriangleIndex, int[] predecessorByTriangleIndex) res) {
         int rootTriangleIndex;
         if (!island.TryIntersect(destination.X, destination.Y, out rootTriangleIndex)) {
            res = (Triangle3.NO_NEIGHBOR_INDEX, null);
            return false;
         }

         var prior = new int[island.Triangles.Length];
         var costUpperBounds = new cDouble[island.Triangles.Length];
         for (var i = 0; i < island.Triangles.Length; i++) {
            prior[i] = Triangle3.NO_NEIGHBOR_INDEX;
            costUpperBounds[i] = cDouble.MaxValue;
         }

         var q = new PriorityQueue<(cDouble, int, int)>((a, b) => a.Item1.CompareTo(b.Item1));
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

      public override void Execute() {
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
            cDouble ks = (cDouble)2000;
            mc.Steering.CurrentUpdateForceContributions.Aggregate.SumForces += mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces * ks; // is normalized
            mc.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights += ks;

            if (mc.Steering.CurrentUpdateForceContributions.Alignment.SumWeights > CDoubleMath.c0) {
               cDouble kc = (cDouble)1500;
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
         var nodeIslandAndTriangleIndexesBySwarmAndComputedRadius = MultiValueDictionary<(Swarm, int), EntityNodeIslandAndTriangleIndex>.Create(() => new List<EntityNodeIslandAndTriangleIndex>());
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
            var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork((cDouble)computedRadius);
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

                  cDouble mul = CDoubleMath.c0_8;
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

            cDouble k = (cDouble)Math.Min(gameTimeManager.Ticks, 19);
            mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces += mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces * k;
            mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights += mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights * k;

            mc.Steering.CurrentUpdateForceContributions.Seeking.SumForces /= k + CDoubleMath.c1;
            mc.Steering.CurrentUpdateForceContributions.Seeking.SumWeights /= k + CDoubleMath.c1;
         }
      }

      private static cDouble[] Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isOverlappingWeightLut;
      private static cDouble[] Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors_isNonOverlappingWeightLut;

      private static void Execute_ContributeEntityCohesionAlignmentAndSeparationSteeringBehaviors(MotionComponent[] motionComponents) {
         for (var i = 0; i < motionComponents.Length - 1; i++) {
            var a = motionComponents[i];
            var aRadius = a.ComputedStatistics.Radius;
            for (var j = i + 1; j < motionComponents.Length; j++) {
               var b = motionComponents[j];
               var aToB = b.Localization.LocalPositionIv2 - a.Localization.LocalPositionIv2;

               var radiusSum = (int)((cDouble)(aRadius + b.ComputedStatistics.Radius) * a.Localization.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor);
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
                                   cDouble k = (cDouble)350;

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
               var aForceMag = (cDouble)IntMath.Sqrt(checked((int)aForceMagSquared));
               var wf = new DoubleVector2((w * (cDouble)aForce.X) / aForceMag, (w * (cDouble)aForce.Y) / aForceMag);

               Debug.Assert(GeometryOperations.IsReal(wf));
               Debug.Assert(GeometryOperations.IsReal(w));

               a.Steering.CurrentUpdateForceContributions.Aggregate.SumForces += wf;// * (cDouble)0.95;
               a.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights += w;// * (cDouble)0.95;

               b.Steering.CurrentUpdateForceContributions.Aggregate.SumForces -= wf;
               b.Steering.CurrentUpdateForceContributions.Aggregate.SumWeights += w;
            }
         }
      }

      private LocalizationState LocalizeEntityAndFixOutOfTriangulationBounds(Entity e) {
         var mc = e.MotionComponent;
         var network = terrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork((cDouble)mc.ComputedStatistics.Radius);

         for (var i = 0; i < 2; i++) {
            // which terrain overlay node are we on?
            if (!network.TryFindTerrainOverlayNode(mc.Pose.WorldPosition, out var node, out var lpos)) {
               FixEntityInHole(e);
               continue;
            }

            // Additionally determine which triangle entity is sitting on in LGV triangulation.
            // TODO: Determinism
            if (!node.LocalGeometryView.Triangulation.TryIntersect(lpos.X, lpos.Y, out var island, out var triangleIndex)) {
               FixEntityInHole(e);
               continue;
            }

            var localPosition = lpos.XY;
            return new LocalizationState {
               TerrainOverlayNetwork = network,
               TerrainOverlayNetworkNode = node,
               LocalPosition = localPosition,
               LocalPositionIv2 = localPosition.LossyToIntVector2(),
               TriangulationIsland = island,
               TriangleIndex = triangleIndex,
            };
         }
         throw new InvalidStateException();
      }

      public List<PathfinderResultContext> RenderMe;

      private void ExecutePathNonswarmer(Entity entity, MotionComponent mc) {
         if (mc.Steering.Roadmap == null) return;
         if (!mc.Steering.IsPathfindingEnabled) return;

         var movementSpeed = statsCalculator.ComputeMovementSpeed(entity);
         var worldDistanceRemaining = movementSpeed * gameTimeManager.SecondsPerTick;
         var plan = mc.Steering.Roadmap.Plan;

         while (worldDistanceRemaining > CDoubleMath.c0 && mc.Steering.RoadmapProgressIndex < plan.Count) {
            var action = plan[mc.Steering.RoadmapProgressIndex];
            switch (action) {
               case MotionRoadmapWalkAction wa:
                  var currentSectorLocalPositionDotNet = Vector3.Transform(mc.Pose.WorldPosition.ToDotNetVector(), wa.Node.SectorNodeDescription.WorldTransformInv).ToOpenMobaVector();
                  var currentSectorLocalPosition = new DoubleVector2(currentSectorLocalPositionDotNet.X, currentSectorLocalPositionDotNet.Y);
                  Trace.Assert(CDoubleMath.Abs(currentSectorLocalPositionDotNet.Z) < (cDouble)1E-3);

                  // vect from position to next pathing breadcrumb (in local space)
                  // todo: set lookat
                  var pb = currentSectorLocalPosition.To(wa.Destination.ToDoubleVector2());

                  // |pb| - distance to next pathing breadcrumb
                  var localDistance = pb.Norm2D();
                  var worldDistance = localDistance * wa.Node.SectorNodeDescription.LocalToWorldScalingFactor;

                  DoubleVector2 nextSectorLocalPosition;
                  if (worldDistance <= CDoubleMath.Epsilon || worldDistance <= worldDistanceRemaining) {
                     nextSectorLocalPosition = wa.Destination.ToDoubleVector2();
                     mc.Steering.RoadmapProgressIndex++;
                     worldDistanceRemaining -= worldDistance;
                  } else {
                     nextSectorLocalPosition = currentSectorLocalPosition + pb * worldDistanceRemaining / worldDistance;
                     worldDistanceRemaining = CDoubleMath.c0;
                  }

                  mc.Pose.WorldPosition = Vector3.Transform(
                     new Vector3(nextSectorLocalPosition.ToDotNetVector(), 0),
                     wa.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                  break;
               default:
                  throw new NotImplementedException();
            }
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

      private DoubleVector2 ComputePositionUpdate(cDouble distanceRemaining, DoubleVector2 p, DoubleVector2 preferredDirectionUnit, TriangulationIsland island, int triangleIndex) {
         var allowPushIntoTriangle = true;
         while (distanceRemaining > GeometryOperations.kEpsilon) {
            DoubleVector2 np;
            int nti;
            var walkResult = WalkTriangle(p, preferredDirectionUnit, distanceRemaining, island, triangleIndex, allowPushIntoTriangle, true, out np, out nti);
            switch (walkResult) {
               case WalkResult.Completion:
                  return np;
               case WalkResult.Progress:
                  distanceRemaining -= (p - np).Norm2D();
                  p = np;
                  triangleIndex = nti;
                  allowPushIntoTriangle = true;
                  continue;
               case WalkResult.PushInward:
                  distanceRemaining -= (p - np).Norm2D();
                  p = np;
                  triangleIndex = nti;
                  allowPushIntoTriangle = false;
                  break;
               case WalkResult.CanPushInward:
                  Console.WriteLine("Warning: Push inward didn't result in being in triangle?");
                  return np;
               case WalkResult.CanEdgeFollow:
                  throw new Exception("Impossible CanEdgeFollow state");
               default:
                  throw new Exception("Impossible state " + walkResult);
            }
         }
         return p;
      }

      private WalkResult WalkTriangle(
         DoubleVector2 position,
         DoubleVector2 preferredDirectionUnit,
         cDouble distanceRemaining,
         TriangulationIsland island,
         int triangleIndex,
         bool allowPushIntoTriangle,
         bool allowEdgeFollow,
         out DoubleVector2 nextPosition,
         out int nextTriangleIndex
      ) {
         Debug.Assert(GeometryOperations.IsReal(position));
         Debug.Assert(GeometryOperations.IsReal(preferredDirectionUnit));
         Debug.Assert(GeometryOperations.IsReal(distanceRemaining));

         // Make this a ref in C# 7.0 for minor perf gains
         ref var triangle = ref island.Triangles[triangleIndex];

         // Find the edge of our container triangle that we're walking towards 
         int opposingVertexIndex;
         if (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(position, preferredDirectionUnit, ref triangle, out opposingVertexIndex)) {
            // Resolve if we're not inside the triangle.
            if (!allowPushIntoTriangle) {
               Console.WriteLine("Warning: Pushed into triangle, but immediately not in triangle?");
               nextPosition = position;
               nextTriangleIndex = triangleIndex;
               return WalkResult.CanPushInward;
            }
            Console.WriteLine("Fix?");

            // If this fails, we're confused as to whether we're in the triangle or not, because we're on an
            // edge and floating point arithmetic error makes us confused. Simply push us slightly into the triangle
            // by pulling us towards its centroid
            // (A previous variant pulled based on perp of nearest edge, however the results are probably pretty similar)
            var offsetToCentroid = position.To(triangle.Centroid);
            if (offsetToCentroid.Norm2D() < InternalTerrainCompilationConstants.TriangleEdgeBufferRadius) {
               Console.WriteLine("Warning: Triangle width less than edge buffer radius!");
               nextPosition = triangle.Centroid;
               nextTriangleIndex = triangleIndex;
               return WalkResult.PushInward;
            } else {
               nextPosition = position + offsetToCentroid.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
               nextTriangleIndex = triangleIndex;
               return WalkResult.PushInward;
            }
         }

         // Let d = remaining "preferred" motion.
         var d = preferredDirectionUnit * distanceRemaining;

         // Project p-e0 onto perp(e0-e1) to find shortest vector from position to edge.
         // Intuitively an edge direction and the direction's perp form a vector
         // space. A point within the triangle's offset from a vertex (which has two edges)
         // is the sum of vector to point on nearest edge and vector from that point to the 
         // vertex. These vectors are orthogonal, so intuitively if we project onto the perp
         // we'll isolate the perp component.
         var e0 = triangle.Points[(opposingVertexIndex + 1) % 3];
         var e1 = triangle.Points[(opposingVertexIndex + 2) % 3];
         var e01 = e0.To(e1); // NOTE: triangle points are CCW.
         var e01Perp = new DoubleVector2(e01.Y, -e01.X); // points outside of current triangle, perp to edge we're crossing
         Trace.Assert(triangle.Centroid.To(e0).ProjectOntoComponentD(e01Perp) > CDoubleMath.c0);

         var pe0 = position.To(e0);
         var pToEdge = pe0.ProjectOnto(e01Perp); // perp to plane normal.

         // If we're sitting right on the edge, push us into the triangle before doing any work
         // Otherwise, it can be ambiguous as to what edge we're passing through on exit.
         // Don't delete this or we'll crash.
         if (pToEdge.Norm2D() < GeometryOperations.kEpsilon) {
            nextPosition = position - e01Perp.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            nextTriangleIndex = triangleIndex;
            return WalkResult.Progress; // is this the best result?
         }

         // Project d onto pToEdge to see if we're moving beyond edge boundary
         var pToEdgeComponentRemaining = d.ProjectOntoComponentD(pToEdge);
         Debug.Assert(GeometryOperations.IsReal(pToEdgeComponentRemaining));

         if (pToEdgeComponentRemaining < CDoubleMath.c1) {
            // Motion finishes within triangle.
            // TODO: Handle when this gets us very close to triangle edge e.g. cR = 0.99999.
            // (We don't want to fall close to the triangle edge but no longer in the triangle
            // due to floating point error)
            nextPosition = position + d;
            nextTriangleIndex = triangleIndex;
            return WalkResult.Completion;
         }

         // Proposed motion would finish outside the triangle
         var neighborTriangleIndex = triangle.NeighborOppositePointIndices[opposingVertexIndex];
         var dToEdge = d / pToEdgeComponentRemaining;
         Debug.Assert(GeometryOperations.IsReal(dToEdge));

         if (neighborTriangleIndex != Triangle3.NO_NEIGHBOR_INDEX) {
            // Move towards and past the edge between us and the other triangle.
            // There's a potential bug here where the other triangle is a sliver.
            // The edge buffer radius could potentially move us past TWO of its edges, out of it.
            // In practice, this bug happens OFTEN and is counteracted by the in-hole hack-fix.
            var dToAndPastEdge = dToEdge + dToEdge.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            nextPosition = position + dToAndPastEdge;
            nextTriangleIndex = neighborTriangleIndex;
            return WalkResult.Progress;
         } else {
            // We're running into an edge! First, place us as close to the edge as possible.
            var dToNearEdge = dToEdge - dToEdge.ToUnit() * InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            var pNearEdge = position + dToNearEdge;

            // We have this guard so if we're edge following, we don't start an inner loop that's also
            // edge following... which would probably lead to a stack overflow
            if (!allowEdgeFollow) {
               Console.WriteLine("Warning: Could edge follow, but was instructed not to?");
               nextPosition = pNearEdge;
               nextTriangleIndex = triangleIndex;
               return WalkResult.CanEdgeFollow;
            }

            // We want to follow the edge, potentially past it if possible.
            // Figure out which edge vertex we're walking towards
            var walkToEdgeVertex1 = d.ProjectOntoComponentD(e01) > CDoubleMath.c0;
            var vertexToWalkTowards = walkToEdgeVertex1 ? e1 : e0;
            var directionToWalkAlongEdge = walkToEdgeVertex1 ? e01 : CDoubleMath.cNeg1 * e01;
            var directionToWalkAlongEdgeUnit = directionToWalkAlongEdge.ToUnit();

            // start tracking p/drem independently.
            var p = pNearEdge;
            var ti = triangleIndex;
            var drem = dToNearEdge.Norm2D();
            var allowPushInward = true;
            while (drem > GeometryOperations.kEpsilon) {
               DoubleVector2 np;
               int nti;
               var wres = WalkTriangle(
                  pNearEdge,
                  directionToWalkAlongEdgeUnit,
                  distanceRemaining - dToNearEdge.Norm2D(),
                  island,
                  ti,
                  allowPushInward,
                  false,
                  out np,
                  out nti
               );
               switch (wres) {
                  case WalkResult.Completion:
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Completion;
                  case WalkResult.CanEdgeFollow:
                     // This is an error, so we just finish
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Completion;
                  case WalkResult.Progress:
                     // Woohoo! Walking along edge brought us into another triangle
                     Trace.Assert(ti != nti);
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Progress;
                  case WalkResult.PushInward:
                     p = np; // HAHA
                     ti = nti;
                     allowPushInward = false;
                     continue;
                  case WalkResult.CanPushInward:
                     nextPosition = np;
                     nextTriangleIndex = nti;
                     return WalkResult.Completion;
               }
            }

            nextPosition = p;
            nextTriangleIndex = ti;
            return WalkResult.Completion;
         }
      }

      public object SaveState() {
         throw new NotImplementedException();
      }
   }
}
