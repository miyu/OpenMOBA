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
   using NodeIslandAndTriangleIndex = ValueTuple<TerrainOverlayNetworkNode, TriangulationIsland, int>;

   public class MovementSystemService : EntitySystemService {
      public enum WalkResult {
         PushInward,
         CanPushInward,
         Progress,
         CanEdgeFollow,
         Completion
      }

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

         var holeDilationRadius = statsCalculator.ComputeCharacterRadius(entity) + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius;
         pathfinderCalculator.TryFindPath(holeDilationRadius, movementComponent.WorldPosition, destination, out var roadmap);
         movementComponent.PathingRoadmap = roadmap;
         movementComponent.PathingIsInvalidated = false;
         movementComponent.PathingRoadmapProgressIndex = 0;
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
         var nodeIslandAndTriangleIndexesBySwarmAndComputedRadius = MultiValueDictionary<(Swarm, int), NodeIslandAndTriangleIndex>.Create(() => new List<NodeIslandAndTriangleIndex>());
         foreach (var e in entities) {
            var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(e.MovementComponent.ComputedRadius);
            e.MovementComponent.TerrainOverlayNetwork = terrainOverlayNetwork;
            if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(
               e.MovementComponent.WorldPosition, 
               out var terrainOverlayNetworkNode,
               out var localPosition)) {
               throw new InvalidStateException();
            }
            e.MovementComponent.TerrainOverlayNetworkNode = terrainOverlayNetworkNode;
            e.MovementComponent.LocalPosition = new DoubleVector2(localPosition.X, localPosition.Y);
            terrainOverlayNetworkNodes.Add(terrainOverlayNetworkNode);

            // Additionally determine which triangle entity is sitting on in LGV triangulation.
            if (!terrainOverlayNetworkNode.LocalGeometryView.Triangulation.TryIntersect(
               localPosition.X,
               localPosition.Y,
               out var island,
               out var triangleIndex)) {
               throw new InvalidStateException();
            }
            e.MovementComponent.SwarmingIsland = island;
            e.MovementComponent.SwarmingTriangleIndex = triangleIndex;

            if (e.MovementComponent.Swarm == null) continue;
            nodeIslandAndTriangleIndexesBySwarmAndComputedRadius.Add((e.MovementComponent.Swarm, e.MovementComponent.ComputedRadius), (terrainOverlayNetworkNode, island, triangleIndex));
         }

         RenderMe = new List<PathfinderResultContext>();
         foreach (var ((swarm, computedRadius), vals) in nodeIslandAndTriangleIndexesBySwarmAndComputedRadius) {
            var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(computedRadius);
            if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(swarm.Destination, out var destinationNode, out var destinationLocal)) {
               throw new NotImplementedException();
            }

            // ReSharper disable once PossibleInvalidCastException
            var starts = vals.Select(x => (x.Item1, x.Item2.Triangles[x.Item3].Centroid.LossyToIntVector2())).ToArray();
            RenderMe.Add(pathfinderCalculator.UniformCostSearch((destinationNode, new IntVector2((int)destinationLocal.X, (int)destinationLocal.Y)), starts, true));
         }

         // 2. For each TONN
         foreach (var entity in entities) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent.PathingIsInvalidated) Pathfind(entity, movementComponent.PathingDestination);

            if (movementComponent.Swarm == null) ExecutePathNonswarmer(entity, movementComponent);
            else ExecutePathSwarmer(entity, movementComponent);
         }
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

      private void ExecutePathSwarmer(Entity entity, MovementComponent movementComponent) { }
   }
}
//var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
         //var triangulation = terrainService.BuildSnapshot().ComputeTriangulation(characterRadius);
         //
         //// p = position of entity to move (updated incrementally)
         //var p = movementComponent.Position;
         //
         //// Find triangle we're currently sitting on.
         //TriangulationIsland island;
         //int triangleIndex;
         //if (!triangulation.TryIntersect(p.X, p.Y, out island, out triangleIndex)) {
         //   Console.WriteLine("Warning: Entity not on land.");
         //   FixEntityInHole(entity);
         //
         //   p = movementComponent.Position;
         //   if (!triangulation.TryIntersect(p.X, p.Y, out island, out triangleIndex)) {
         //      Console.WriteLine("Warning: fixing entity not on land failed?");
         //      return;
         //   }
         //}
         //
         //// Figure out how much further entity can move this tick
         //var preferredDirectionUnit = movementComponent.SwarmlingVelocity.ToUnit();
         //var distanceRemaining = movementComponent.SwarmlingVelocity.Norm2D() * gameTimeService.SecondsPerTick;
         //
         //movementComponent.Position = CPU(distanceRemaining, p, preferredDirectionUnit, island, triangleIndex);
//      }

      /*
      private DoubleVector3 CPU(double distanceRemaining, DoubleVector3 p, DoubleVector2 preferredDirectionUnit, TriangulationIsland island, int triangleIndex) {
         var allowPushIntoTriangle = true;
         while (distanceRemaining > GeometryOperations.kEpsilon) {
            DoubleVector3 np;
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

      // removes normal component of point relative to triangle.
      // NOTE: This can change the point's XY!
//      private DoubleVector3 ProjectToTrianglePlane(DoubleVector3 p, ref Triangle3 triangle) {
//         return p - p.To(triangle.Points[0]).ProjectOnto(triangle.Normal);
//      }

      // Computes Z of p on triangle plane.
//      private DoubleVector3 ZIfyPointOnTrianglePlane(DoubleVector2 p, ref Triangle3 triangle) {
//         // Let p = point we're finding with same x, z
//         //     q = another point on triangle
//         //     n = triangle normal
//         // dot(p-q, normal) = 0
//         // normal.X * (p.X - q.X) + normal.Y * (p.Y - q.Y) + normal.Z * (p.Z - q.Z) = 0
//         // normal.Z * (p.Z - q.Z) = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z - normal.Z * q.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z) / normal.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)) / normal.Z + q.Z
//         var q1 = triangle.Points[0];
//         var q2 = triangle.Points[1];
//         var q = (p - q1.XY).SquaredNorm2D() > (p - q2.XY).SquaredNorm2D() ? q1 : q2;
//         var n = triangle.Normal;
//         var z = (n.X * (q.X - p.X) + n.Y * (q.Y - p.Y)) / n.Z + q.Z;
//         return new DoubleVector3(p.X, p.Y, z);
//      }

      // Computes Z of v formed by triangle plane basis.
//      private DoubleVector3 ZIfyVectorOnTriangleBasis(DoubleVector2 v, ref Triangle3 triangle) {
//         // This is equivalent to ZIfyPointOnTrianglePlane if triangle has 0,0,0 for a point.
//         // Let p = point we're finding with same x, z
//         //     q = 0,0,0
//         //     n = triangle normal
//         // dot(p-q, normal) = 0
//         // normal.X * (p.X - q.X) + normal.Y * (p.Y - q.Y) + normal.Z * (p.Z - q.Z) = 0
//         // normal.Z * (p.Z - q.Z) = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z - normal.Z * q.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z) / normal.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)) / normal.Z + q.Z
//         var p = v;
//         var q = DoubleVector3.Zero;
//         var n = triangle.Normal;
//         var z = (n.X * (q.X - p.X) + n.Y * (q.Y - p.Y)) / n.Z + q.Z;
//         return new DoubleVector3(p.X, p.Y, z);
//      }

      private WalkResult WalkTriangle(
         DoubleVector3 position,
         DoubleVector2 preferredDirectionUnit,
         double distanceRemaining,
         TriangulationIsland island,
         int triangleIndex,
         bool allowPushIntoTriangle,
         bool allowEdgeFollow,
         out DoubleVector3 nextPosition,
         out int nextTriangleIndex
      ) {
         Debug.Assert(GeometryOperations.IsReal(position));
         Debug.Assert(GeometryOperations.IsReal(preferredDirectionUnit));
         Debug.Assert(GeometryOperations.IsReal(distanceRemaining));
         nextPosition = position;
         nextTriangleIndex = triangleIndex;
         return WalkResult.Completion;
//         // Make this a ref in C# 7.0 for minor perf gains
//         var triangle = island.Triangles[triangleIndex];
//         ;
//
//         // NOTE: Position is assumed to be on the triangle plane already.
//         // Either way, enforce: Holding p.XY constant, reset Z to whatever's on triangle plane.
//         var npos = ZIfyPointOnTrianglePlane(position.XY, ref triangle);
//         if ((position - npos).SquaredNorm2D() > 0.05) Console.WriteLine("!! clamp z to triangle " + (position - npos).Norm2D());
//         position = npos;
//
//         // Find the edge of our container triangle that we're walking towards 
//         int opposingVertexIndex;
//         if (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(position.XY, preferredDirectionUnit, ref triangle, out opposingVertexIndex)) {
//            // Resolve if we're not inside the triangle.
//            if (!allowPushIntoTriangle) {
//               Console.WriteLine("Warning: Pushed into triangle, but immediately not in triangle?");
//               nextPosition = position;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.CanPushInward;
//            }
//            Console.WriteLine("Fix?");
//
//            // If this fails, we're confused as to whether we're in the triangle or not, because we're on an
//            // edge and floating point arithmetic error makes us confused. Simply push us slightly into the triangle
//            // by pulling us towards its centroid
//            // (A previous variant pulled based on perp of nearest edge, however the results are probably pretty similar)
//            var offsetToCentroid = position.To(triangle.Centroid);
//            if (offsetToCentroid.Norm2D() < TerrainConstants.TriangleEdgeBufferRadius) {
//               Console.WriteLine("Warning: Triangle width less than edge buffer radius!");
//               nextPosition = triangle.Centroid;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.PushInward;
//            } else {
//               nextPosition = position + offsetToCentroid.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.PushInward;
//            }
//         }
//
//         // Let d = remaining "preferred" motion.
//         var d = ZIfyVectorOnTriangleBasis(preferredDirectionUnit, ref triangle).ToUnit() * distanceRemaining;
//
//         // Project p-e0 onto perp(e0-e1) to find shortest vector from position to edge.
//         // Intuitively an edge direction and the direction's perp form a vector
//         // space. A point within the triangle's offset from a vertex (which has two edges)
//         // is the sum of vector to point on nearest edge and vector from that point to the 
//         // vertex. These vectors are orthogonal, so intuitively if we project onto the perp
//         // we'll isolate the perp component.
//         var e0 = triangle.Points[(opposingVertexIndex + 1) % 3];
//         var e1 = triangle.Points[(opposingVertexIndex + 2) % 3];
//         var e01 = e0.To(e1);
//         var e01Perp = e01.Cross(triangle.Normal); // points outside of current triangle, perp to edge we're crossing, on triangle plane.
//         Trace.Assert(triangle.Centroid.To(e0).ProjectOntoComponentD(e01Perp) > 0);
//
//         var pe0 = position.To(e0);
//         var pToEdge = pe0.ProjectOnto(e01Perp); // perp to plane normal.
//
//         // If we're sitting right on the edge, push us into the triangle before doing any work
//         // Otherwise, it can be ambiguous as to what edge we're passing through on exit.
//         // Don't delete this or we'll crash.
//         if (pToEdge.Norm2D() < GeometryOperations.kEpsilon) {
//            nextPosition = position - e01Perp.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//            nextTriangleIndex = triangleIndex;
//            return WalkResult.Progress; // is this the best result?
//         }
//
//         // Project d onto pToEdge to see if we're moving beyond edge boundary
//         var pToEdgeComponentRemaining = d.ProjectOntoComponentD(pToEdge);
//         Debug.Assert(GeometryOperations.IsReal(pToEdgeComponentRemaining));
//
//         if (pToEdgeComponentRemaining < 1) {
//            // Motion finishes within triangle.
//            // TODO: Handle when this gets us very close to triangle edge e.g. cR = 0.99999.
//            // (We don't want to fall close to the triangle edge but no longer in the triangle
//            // due to floating point error)
//            nextPosition = position + d;
//            nextTriangleIndex = triangleIndex;
//            return WalkResult.Completion;
//         }
//
//         // Proposed motion would finish outside the triangle
//         var neighborTriangleIndex = triangle.NeighborOppositePointIndices[opposingVertexIndex];
//         var dToEdge = d / pToEdgeComponentRemaining;
//         Debug.Assert(GeometryOperations.IsReal(dToEdge));
//
//         if (neighborTriangleIndex != Triangle3.NO_NEIGHBOR_INDEX) {
//            // Move towards and past the edge between us and the other triangle.
//            // There's a potential bug here where the other triangle is a sliver.
//            // The edge buffer radius could potentially move us past TWO of its edges, out of it.
//            // In practice, this bug happens OFTEN and is counteracted by the in-hole hack-fix.
//            var dToAndPastEdge = dToEdge + dToEdge.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//            nextPosition = position + dToAndPastEdge;
//            nextTriangleIndex = neighborTriangleIndex;
//            return WalkResult.Progress;
//         } else {
//            // We're running into an edge! First, place us as close to the edge as possible.
//            var dToNearEdge = dToEdge - dToEdge.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//            var pNearEdge = position + dToNearEdge;
//
//            // We have this guard so if we're edge following, we don't start an inner loop that's also
//            // edge following... which would probably lead to a stack overflow
//            if (!allowEdgeFollow) {
//               Console.WriteLine("Warning: Could edge follow, but was instructed not to?");
//               nextPosition = pNearEdge;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.CanEdgeFollow;
//            }
//
//            // We want to follow the edge, potentially past it if possible.
//            // Figure out which edge vertex we're walking towards
//            var walkToEdgeVertex1 = d.ProjectOntoComponentD(e01) > 0;
//            var vertexToWalkTowards = walkToEdgeVertex1 ? e1 : e0;
//            var directionToWalkAlongEdge = walkToEdgeVertex1 ? e01 : -1.0 * e01;
//            var directionToWalkAlongEdgeUnit = directionToWalkAlongEdge.ToUnit();
//
//            // start tracking p/drem independently.
//            var p = pNearEdge;
//            var ti = triangleIndex;
//            var drem = dToNearEdge.Norm2D();
//            var allowPushInward = true;
//            while (drem > GeometryOperations.kEpsilon) {
//               DoubleVector3 np;
//               int nti;
//               var wres = WalkTriangle(
//                  pNearEdge,
//                  directionToWalkAlongEdgeUnit.XY,
//                  distanceRemaining - dToNearEdge.Norm2D(),
//                  island,
//                  ti,
//                  allowPushInward,
//                  false,
//                  out np,
//                  out nti
//               );
//               switch (wres) {
//                  case WalkResult.Completion:
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Completion;
//                  case WalkResult.CanEdgeFollow:
//                     // This is an error, so we just finish
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Completion;
//                  case WalkResult.Progress:
//                     // Woohoo! Walking along edge brought us into another triangle
//                     Trace.Assert(ti != nti);
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Progress;
//                  case WalkResult.PushInward:
//                     p = np; // HAHA
//                     ti = nti;
//                     allowPushInward = false;
//                     continue;
//                  case WalkResult.CanPushInward:
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Completion;
//               }
//            }
//
//            nextPosition = p;
//            nextTriangleIndex = ti;
//            return WalkResult.Completion;
//
//            //            // Which edge would we be crossing if we walked along e01 past the vertex?
//            //            // If we're walking along e01 past e1, then we're hitting e12 (across 0, keep 1)
//            //            // If we're walking along e01 past e0, then we're hitting e20 (across 1, keep 0)
//            //            // we'll denote the new edge eab
//            //            var e2 = triangle.Points[opposingVertexIndex];
//            //            var ea = walkToEdgeVertex1 ? e1 : e2;
//            //            var eb = walkToEdgeVertex1 ? e2 : e0;
//            //
//            //            var vertexIndexOpposingEab =
//            //               walkToEdgeVertex1
//            //                  ? (opposingVertexIndex + 1) % 3
//            //                  : (opposingVertexIndex + 2) % 3;
//            //
//            //            var otherNeighborTriangleIndex = triangle.NeighborOppositePointIndices[vertexIndexOpposingEab];
//            //            if (otherNeighborTriangleIndex == Triangle.NO_NEIGHBOR_INDEX) {
//            //               // No neighbor exists, so we're walking towards a corner.
//            //               return WalkTriangle(
//            //                  pNearEdge,
//            //                  directionToWalkAlongEdge,
//            //                  distanceRemaining - dToNearEdge.Norm2D(),
//            //                  island,
//            //                  triangleIndex,
//            //                  true,
//            //                  false);
//            //            }
//            //            // Neighbor exists, so walk until we get into its triangle...
//            //            return WalkTriangle(
//            //               pNearEdge,
//            //               directionToWalkAlongEdge,
//            //               distanceRemaining - dToNearEdge.Norm2D(),
//            //               island,
//            //               triangleIndex,
//            //               true,
//            //               false);
//         }
      }
   }
}*/