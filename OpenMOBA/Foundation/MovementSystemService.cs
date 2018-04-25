using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.CompilationResults;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation {
   using EntityNodeIslandAndTriangleIndex = ValueTuple<Entity, TerrainOverlayNetworkNode, TriangulationIsland, int>;

   public enum WalkResult {
      PushInward,
      CanPushInward,
      Progress,
      CanEdgeFollow,
      Completion
   }

   public class MovementSystemService : EntitySystemService {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Movement);
      private readonly GameTimeService gameTimeService;
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly StatsCalculator statsCalculator;
      private readonly TerrainService terrainService;

      public MovementSystemService(
         EntityService entityService,
         GameTimeService gameTimeService,
         StatsCalculator statsCalculator,
         TerrainService terrainService,
         PathfinderCalculator pathfinderCalculator
      ) : base(entityService, kComponentMask) {
         this.gameTimeService = gameTimeService;
         this.statsCalculator = statsCalculator;
         this.terrainService = terrainService;
         this.pathfinderCalculator = pathfinderCalculator;
      }

      public void Pathfind(Entity entity, DoubleVector3 destination) {
         var movementComponent = entity.MovementComponent;
         movementComponent.PathingDestination = destination;

         var holeDilationRadius = statsCalculator.ComputeCharacterRadius(entity);

         if (pathfinderCalculator.TryFindPath(holeDilationRadius, movementComponent.WorldPosition, destination, out var roadmap)) {
            movementComponent.PathingRoadmap = roadmap;
            movementComponent.PathingIsInvalidated = false;
            movementComponent.PathingRoadmapProgressIndex = 0;
            movementComponent.LastFailedPathfindingSnapshot = null;
            Console.WriteLine("Pathfinding succeeded");
         } else {
            movementComponent.PathingRoadmap = null;
            movementComponent.PathingIsInvalidated = false;
            movementComponent.PathingRoadmapProgressIndex = -1;
            movementComponent.LastFailedPathfindingSnapshot = terrainService.CompileSnapshot();
            Console.WriteLine("Pathfinding Failed");
         }
      }

      public void HandleHoleAdded(DynamicTerrainHoleDescription holeDescription) {
         InvalidatePaths();

         foreach (var entity in AssociatedEntities) {
            var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
            var paddedHoleDilationRadius = characterRadius + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius + InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            if (holeDescription.ContainsPoint(paddedHoleDilationRadius, entity.MovementComponent.WorldPosition)) {
               Console.WriteLine("Out ouf bounds!");
               FixEntityInHole(entity);
            }
         }
      }

      private void FixEntityInHole(Entity entity) {
         var computedRadius = statsCalculator.ComputeCharacterRadius(entity);
         var movementComponent = entity.MovementComponent;
         movementComponent.WorldPosition = PushToLand(movementComponent.WorldPosition, computedRadius);
      }

      private DoubleVector3 PushToLand(DoubleVector3 pWorld, double computedRadius) {
         var paddedHoleDilationRadius = computedRadius + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius + InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
         var terrainOverlayNetwork = terrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(paddedHoleDilationRadius);
         Console.WriteLine("PHDR: " + paddedHoleDilationRadius);
         var bestWorldDistance = double.PositiveInfinity;
         var bestWorld = DoubleVector3.Zero;
         foreach (var terrainOverlayNode in terrainOverlayNetwork.TerrainNodes) {
            var pLocal = (DoubleVector2)terrainOverlayNode.SectorNodeDescription.WorldToLocal(pWorld);
            terrainOverlayNode.LocalGeometryView.FindNearestLandPointAndIsInHole(pLocal, out var pNearestLocal);

            // clamp pNearestLocal to be within bounds, not 

            var pNearestWorld = terrainOverlayNode.SectorNodeDescription.LocalToWorld(pNearestLocal);
            var worldDistance = pWorld.To(pNearestWorld).Norm2D();
            if (worldDistance < bestWorldDistance) {
               bestWorldDistance = worldDistance;
               bestWorld = pNearestWorld;
            }
         }
         return bestWorld;
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
         foreach (var entity in AssociatedEntities) entity.MovementComponent.PathingIsInvalidated = true;
      }

      public bool NN(TriangulationIsland island, DoubleVector3 destination, out Dictionary<int, int> d) {
         int rootTriangleIndex;
         if (!island.TryIntersect(destination.X, destination.Y, out rootTriangleIndex)) {
            d = null;
            return false;
         }

         d = new Dictionary<int, int>();
         var s = new PriorityQueue<Tuple<int, int, double>>((a, b) => a.Item3.CompareTo(b.Item3));
         s.Enqueue(Tuple.Create(rootTriangleIndex, -1, 0.0));
         while (s.Any()) {
            var t = s.Dequeue();
            var ti = t.Item1;
            if (d.ContainsKey(ti)) continue;
            var pi = t.Item2;
            var prevDist = t.Item3;
            d[ti] = pi;
            for (var i = 0; i < 3; i++) {
               var nti = island.Triangles[ti].NeighborOppositePointIndices[i];
               if (nti != Triangle3.NO_NEIGHBOR_INDEX) {
                  var addDist = (island.Triangles[nti].Centroid - island.Triangles[ti].Centroid).Norm2D();
                  s.Enqueue(Tuple.Create(nti, ti, prevDist + addDist));
               }
            }
         }
         return true;
      }

      public override void Execute() {
         var entities = AssociatedEntities.ToArray();
         var terrainSnapshot = terrainService.CompileSnapshot();

         // 0. Precompute computed entity stats, zero flocking intermediate aggregates
         for (var i = 0; i < entities.Length; i++) {
            var e = entities[i];
            e.MovementComponent.ComputedRadius = (int)Math.Ceiling(statsCalculator.ComputeCharacterRadius(e));
            e.MovementComponent.ComputedSpeed = (int)Math.Ceiling(statsCalculator.ComputeMovementSpeed(e));
            e.MovementComponent.WeightedSumNBodyForces = DoubleVector2.Zero;
            e.MovementComponent.SumWeightsNBodyForces = 0;
         }

         // 1. Determine Terrain Overlay Network Nodes entities are sitting on (group by)
         // additionally determine local position, triangulation island, and triangle index.
         var terrainOverlayNetworkNodes = new HashSet<TerrainOverlayNetworkNode>();
         var nodeIslandAndTriangleIndexesBySwarmAndComputedRadius = MultiValueDictionary<(Swarm, int), EntityNodeIslandAndTriangleIndex>.Create(() => new List<EntityNodeIslandAndTriangleIndex>());
         foreach (var e in entities) {
            var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(e.MovementComponent.ComputedRadius);
            FindOrFixEntityTerrainNodeAndTriangle(e, out terrainOverlayNetwork, out var terrainOverlayNetworkNode, out var localPosition, out var island, out var triangleIndex);

            e.MovementComponent.TerrainOverlayNetwork = terrainOverlayNetwork;
            e.MovementComponent.TerrainOverlayNetworkNode = terrainOverlayNetworkNode;
            e.MovementComponent.LocalPosition = new DoubleVector2(localPosition.X, localPosition.Y);
            e.MovementComponent.LocalPositionIv2 = e.MovementComponent.LocalPosition.LossyToIntVector2();
            terrainOverlayNetworkNodes.Add(terrainOverlayNetworkNode);

            e.MovementComponent.SwarmingIsland = island;
            e.MovementComponent.SwarmingTriangleIndex = triangleIndex;

            if (e.MovementComponent.Swarm == null) continue;

            nodeIslandAndTriangleIndexesBySwarmAndComputedRadius.Add(
               (e.MovementComponent.Swarm, e.MovementComponent.ComputedRadius),
               (e, terrainOverlayNetworkNode, island, triangleIndex));
         }

         // 2. Find PathfinderResultContext pathing from swarm triangles to destination,
         //    Additionally yields for each swarm/computedRadius group, yields centroid index for each entity within group to PRC destinations.
         RenderMe = new List<PathfinderResultContext>();
         var something = new Dictionary<(Swarm swarm, int computedRadius), (PathfinderResultContext pathfinderResultContext, int[] centroidIndicesByEntityIndex)>();
         foreach (var ((swarm, computedRadius), entityNodeAndTriangleIndexes) in nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
            var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(computedRadius);
            if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(swarm.Destination, out var destinationNode, out var destinationLocal)) {
               break;
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

            something.Add((swarm, computedRadius), (pathfinderResultContext, centroidIndices));
            RenderMe.Add(pathfinderResultContext);
         }

         // 3. For each entity, contribute path-following steering behavior
         foreach (var ((swarm, computedRadius), entityNodeAndTriangleIndexes) in nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
            if (!something.TryGetValue((swarm, computedRadius), out var res)) continue;
            var (pathfinderResultContext, centroidIndicesByEntityIndex) = res;
            foreach (var (i, (entity, entityTerrainOverlayNode, island, triangleIndex)) in entityNodeAndTriangleIndexes.Enumerate()) {
               var mc = entity.MovementComponent;
               var centroidIndex = centroidIndicesByEntityIndex[i];
               if (pathfinderResultContext.TryComputeRoadmap(centroidIndex, out var roadmap)) {
                  // path-following behavior. Recall from destination to source, so roadmap must be followed backward.
                  var action = (MotionRoadmapWalkAction)roadmap.Plan.Last();
                  Trace.Assert(action.Node == entityTerrainOverlayNode);

                  // path-following vector is from destination to source because our multi-pathfind goes from destination to source.
                  var v = action.Destination.To(action.Source).ToDoubleVector2().ToUnit();
                  var w = 1.0;
                  mc.WeightedSumNBodyForces += v * w;
                  mc.SumWeightsNBodyForces += w;
               }
            }
         }

         // 4. for each entity pairing, compute separation force vector which prevents overlap
         //    and "regroup" (cohesion) force vector, which causes clustering within swarms.
         //    Logic contained within should be scale invariant!
         var movementComponents = entities.Map(e => e.MovementComponent);
         for (var i = 0; i < movementComponents.Length - 1; i++) {
            var a = movementComponents[i];
            var aRadius = a.ComputedRadius;
            for (var j = i + 1; j < movementComponents.Length; j++) {
               var b = movementComponents[j];
               var aToB = b.LocalPositionIv2 - a.LocalPositionIv2;

               var radiusSum = (int)((aRadius + b.ComputedRadius) * a.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor);
               var radiusSumSquared = radiusSum * radiusSum;
               var centerDistanceSquared = aToB.SquaredNorm2();

               // Must either be overlapping or in the same swarm for us to compute
               // (In the future rather than "in same swarm" probably want "allied".
               var isOverlapping = centerDistanceSquared < radiusSumSquared;

               double w; // where 1 means equal in weight to isolated-unit pather
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
                  const int k = 16;
                  var centerDistance = (int)Math.Sqrt(centerDistanceSquared);
                  w = IntMath.Square(k * (radiusSum - centerDistance)) / (double)radiusSumSquared;
                  Debug.Assert(GeometryOperations.IsReal(w));

                  // And the force vector (outer code will tounit this)
                  aForce = aToB.SquaredNorm2() == 0
                     ? new IntVector2(2, 1)
                     : -1 * aToB;
               } else if (a.Swarm == b.Swarm && a.Swarm != null) {
                  // Case: Nonoverlapping, in same swarm. Push swarmlings near but nonoverlapping
                  // TODO: Alignment force.
                  const int groupingTolerance = 8;
                  var spacingBetweenBoundaries = (int)Math.Sqrt(centerDistanceSquared) - radiusSum;
                  var maxAttractionDistance = radiusSum * groupingTolerance;

                  if (spacingBetweenBoundaries > maxAttractionDistance)
                     continue;

                  // regroup = ((D - d) / D)^4
                  w = 0.001 * (double)Math.Pow(spacingBetweenBoundaries - maxAttractionDistance, 4.0) / Math.Pow(maxAttractionDistance, 4.0);
                  Debug.Assert(GeometryOperations.IsReal(w));

                  aForce = aToB;
               } else {
                  // todo: experiment with continue vs zero-weight for no failed branch prediction
                  // (this is pretty pipeliney code)
                  continue;
               }


               var wf = w * aForce.ToDoubleVector2().ToUnit();
               Debug.Assert(GeometryOperations.IsReal(wf));
               Debug.Assert(GeometryOperations.IsReal(w));

               a.WeightedSumNBodyForces += wf;
               a.SumWeightsNBodyForces += w;

               b.WeightedSumNBodyForces -= wf;
               b.SumWeightsNBodyForces += w;
            }

            if (a.Swarm == null) continue;


//            var seekAggregate = DoubleVector2.Zero;
//            var seekWeightAggregate = 0.0;

//            var d = ds[Tuple.Create(a.SwarmingIsland, a.Swarm.Destination)];
//            int nti;
//            if (d != null && d.TryGetValue(a.SwarmingTriangleIndex, out nti) && nti != Triangle.NO_NEIGHBOR_INDEX) {
//               var triangleCentroidDijkstrasOptimalSeekUnit = (a.SwarmingIsland.Triangles[nti].Centroid - a.SwarmingIsland.Triangles[a.SwarmingTriangleIndex].Centroid).ToUnit();
//               const double mul = 0.5;
//               seekAggregate += mul * triangleCentroidDijkstrasOptimalSeekUnit;
//               seekWeightAggregate += mul;
//            }
//
//            var key = Tuple.Create(a.ComputedRadius, a.SwarmingTriangleIndex, a.SwarmingIsland, a.Swarm.Destination);
//            var triangleCentroidOptimalSeekUnit = vectorField[key];
//            seekAggregate += triangleCentroidOptimalSeekUnit;
//            seekWeightAggregate += 1.0;

            // var directionalSeekUnit = (a.Swarm.Destination - a.Position).ToUnit();
            // seekAggregate += directionalSeekUnit;
            // seekWeightAggregate += 1.0;

//            var seekUnit = seekWeightAggregate < GeometryOperations.kEpsilon ? DoubleVector2.Zero : seekAggregate.ToUnit();
//
//            const double seekWeight = 1.0;
//            a.WeightedSumNBodyForces += seekWeight * seekUnit;
//            a.SumWeightsNBodyForces += seekWeight;
//            a.SwarmlingVelocity = (a.WeightedSumNBodyForces / a.SumWeightsNBodyForces) * a.ComputedSpeed;
//            Debug.Assert(GeometryOperations.IsReal(a.SwarmlingVelocity));
         }


         foreach (var entity in entities) {
            var movementComponent = entity.MovementComponent;

            var repath = movementComponent.PathingIsInvalidated || (
                            movementComponent.LastFailedPathfindingSnapshot != null &&
                            movementComponent.LastFailedPathfindingSnapshot != terrainService.CompileSnapshot());

            if (repath) {
               Pathfind(entity, movementComponent.PathingDestination);
            }

            if (movementComponent.Swarm == null) {
               ExecutePathNonswarmer(entity, movementComponent);
            } else {
               ExecutePathSwarmer(entity, movementComponent);
            }
         }
      }

      private void FindOrFixEntityTerrainNodeAndTriangle(Entity e, out TerrainOverlayNetwork terrainOverlayNetwork, out TerrainOverlayNetworkNode terrainOverlayNetworkNode, out DoubleVector3 localPosition, out TriangulationIsland island, out int triangleIndex) {
         var mc = e.MovementComponent;
         terrainOverlayNetwork = terrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(mc.ComputedRadius);

         for (var i = 0; i < 2; i++) {
            // which terrain overlay node are we on?
            if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(mc.WorldPosition, out terrainOverlayNetworkNode, out localPosition)) {
               FixEntityInHole(e);
               continue;
            }

            // Additionally determine which triangle entity is sitting on in LGV triangulation.
            if (!terrainOverlayNetworkNode.LocalGeometryView.Triangulation.TryIntersect(
               localPosition.X, localPosition.Y,
               out island, out triangleIndex)) {
               FixEntityInHole(e);
               continue;
            }

            return;
         }
         throw new InvalidStateException();
      }

      public static List<PathfinderResultContext> RenderMe;

      private void ExecutePathNonswarmer(Entity entity, MovementComponent movementComponent) {
         if (movementComponent.PathingRoadmap == null) return;

         var movementSpeed = statsCalculator.ComputeMovementSpeed(entity);
         var worldDistanceRemaining = movementSpeed * gameTimeService.SecondsPerTick;
         var plan = movementComponent.PathingRoadmap.Plan;

         while (worldDistanceRemaining > 0 && movementComponent.PathingRoadmapProgressIndex < plan.Count) {
            var action = plan[movementComponent.PathingRoadmapProgressIndex];
            switch (action) {
               case MotionRoadmapWalkAction wa:
                  var currentSectorLocalPositionDotNet = Vector3.Transform(movementComponent.WorldPosition.ToDotNetVector(), wa.Node.SectorNodeDescription.WorldTransformInv).ToOpenMobaVector();
                  var currentSectorLocalPosition = new DoubleVector2(currentSectorLocalPositionDotNet.X, currentSectorLocalPositionDotNet.Y);
                  Trace.Assert(Math.Abs(currentSectorLocalPositionDotNet.Z) < 1E-3);

                  // vect from position to next pathing breadcrumb (in local space)
                  // todo: set lookat
                  var pb = currentSectorLocalPosition.To(wa.Destination.ToDoubleVector2());

                  // |pb| - distance to next pathing breadcrumb
                  var localDistance = pb.Norm2D();
                  var worldDistance = localDistance * wa.Node.SectorNodeDescription.LocalToWorldScalingFactor;

                  DoubleVector2 nextSectorLocalPosition;
                  if (worldDistance <= float.Epsilon || worldDistance <= worldDistanceRemaining) {
                     nextSectorLocalPosition = wa.Destination.ToDoubleVector2();
                     movementComponent.PathingRoadmapProgressIndex++;
                     worldDistanceRemaining -= worldDistance;
                  } else {
                     nextSectorLocalPosition = currentSectorLocalPosition + pb * worldDistanceRemaining / worldDistance;
                     worldDistanceRemaining = 0;
                  }

                  movementComponent.WorldPosition = Vector3.Transform(
                     new Vector3(nextSectorLocalPosition.ToDotNetVector(), 0),
                     wa.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      private void ExecutePathSwarmer(Entity entity, MovementComponent movementComponent) {
         var worldDistanceRemaining = movementComponent.ComputedSpeed * gameTimeService.SecondsPerTick;
         var localDistanceRemaining = movementComponent.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor;
         var dv2 = ComputePositionUpdate(
            localDistanceRemaining,
            movementComponent.LocalPosition,
            movementComponent.WeightedSumNBodyForces.ToUnit(),
            movementComponent.SwarmingIsland,
            movementComponent.SwarmingTriangleIndex);
         movementComponent.LocalPosition = dv2;
         movementComponent.WorldPosition = movementComponent.TerrainOverlayNetworkNode.SectorNodeDescription.LocalToWorld(dv2);
      }

      private DoubleVector2 ComputePositionUpdate(double distanceRemaining, DoubleVector2 p, DoubleVector2 preferredDirectionUnit, TriangulationIsland island, int triangleIndex) {
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
         double distanceRemaining,
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
         Trace.Assert(triangle.Centroid.To(e0).ProjectOntoComponentD(e01Perp) > 0);

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

         if (pToEdgeComponentRemaining < 1) {
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
            var walkToEdgeVertex1 = d.ProjectOntoComponentD(e01) > 0;
            var vertexToWalkTowards = walkToEdgeVertex1 ? e1 : e0;
            var directionToWalkAlongEdge = walkToEdgeVertex1 ? e01 : -1.0 * e01;
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

            //            // Which edge would we be crossing if we walked along e01 past the vertex?
            //            // If we're walking along e01 past e1, then we're hitting e12 (across 0, keep 1)
            //            // If we're walking along e01 past e0, then we're hitting e20 (across 1, keep 0)
            //            // we'll denote the new edge eab
            //            var e2 = triangle.Points[opposingVertexIndex];
            //            var ea = walkToEdgeVertex1 ? e1 : e2;
            //            var eb = walkToEdgeVertex1 ? e2 : e0;
            //
            //            var vertexIndexOpposingEab =
            //               walkToEdgeVertex1
            //                  ? (opposingVertexIndex + 1) % 3
            //                  : (opposingVertexIndex + 2) % 3;
            //
            //            var otherNeighborTriangleIndex = triangle.NeighborOppositePointIndices[vertexIndexOpposingEab];
            //            if (otherNeighborTriangleIndex == Triangle.NO_NEIGHBOR_INDEX) {
            //               // No neighbor exists, so we're walking towards a corner.
            //               return WalkTriangle(
            //                  pNearEdge,
            //                  directionToWalkAlongEdge,
            //                  distanceRemaining - dToNearEdge.Norm2D(),
            //                  island,
            //                  triangleIndex,
            //                  true,
            //                  false);
            //            }
            //            // Neighbor exists, so walk until we get into its triangle...
            //            return WalkTriangle(
            //               pNearEdge,
            //               directionToWalkAlongEdge,
            //               distanceRemaining - dToNearEdge.Norm2D(),
            //               island,
            //               triangleIndex,
            //               true,
            //               false);
         }
      }
   }
}
