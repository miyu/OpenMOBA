using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenMOBA.Foundation {
   public class Entity {
      public EntityComponentsMask ComponentMask { get; set; }
      public EntityComponent[] ComponentsByType { get; } = new EntityComponent[(int)EntityComponentType.Count];
      public MovementComponent MovementComponent => (MovementComponent)ComponentsByType[(int)EntityComponentType.Movement];
   }

   public class EntityService {
      private readonly List<EntitySystemService> systems = new List<EntitySystemService>();
      private readonly HashSet<Entity> entities = new HashSet<Entity>();

      public IEnumerable<Entity> EnumerateEntities() => entities;

      public void AddEntitySystem(EntitySystemService system) {
         systems.Add(system);
      }

      public Entity CreateEntity() {
         var entity = new Entity();
         entities.Add(entity);
         return entity;
      }

      public void AddEntityComponent(Entity entity, EntityComponent component) {
         if (entity.ComponentMask.Contains(ComponentMaskUtils.Build(component.Type))) {
            throw new InvalidOperationException("Entity already has component of type " + component.Type);
         }
         entity.ComponentMask = entity.ComponentMask.Or(component.Type);
         entity.ComponentsByType[(int)component.Type] = component;
         foreach (var system in systems) {
            if (entity.ComponentMask.Contains(system.RequiredComponentsMask)) {
               system.AssociateEntity(entity);
            }
         }
      }

      public void ProcessSystems() {
         foreach (var system in systems) {
            system.Execute();
         }
      }
   }

   public static class ComponentMaskUtils {
      public static EntityComponentsMask Or(this EntityComponentsMask mask, EntityComponentType type) {
         return mask | (EntityComponentsMask)(1 << (int)type);
      }

      public static bool Contains(this EntityComponentsMask mask, EntityComponentsMask other) {
         return (mask & other) == other;
      }

      public static EntityComponentsMask Build(params EntityComponentType[] componentTypes) {
         EntityComponentsMask result = 0;
         foreach (var componentType in componentTypes) {
            result = result.Or(componentType);
         }
         return result;
      }
   }

   /// <summary>
   /// Note: A value in this enum is treated as an offset into an array.
   /// Note: This takes advantage of the first enum member having value 0.
   /// </summary>
   public enum EntityComponentType {
      Movement,
      Status,

      Count
   }

   [Flags]
   public enum EntityComponentsMask : uint { }
   
   public abstract class EntityComponent {
      protected EntityComponent(EntityComponentType type) {
         Type = type;
      }

      public EntityComponentType Type { get; }
   }

   public class MovementComponent : EntityComponent {
      public MovementComponent() : base(EntityComponentType.Movement) { }
      public DoubleVector2 Position { get; set; }
      public DoubleVector2 LookAt { get; set; } = DoubleVector2.UnitX;
      public float BaseRadius { get; set; }
      public float BaseSpeed { get; set; }

      /// <summary>
      /// If true, movement will recompute path before updating position
      /// </summary>
      public bool PathingIsInvalidated { get; set; } = false;

      /// <summary>
      /// The desired destination of the unit. Even if pathfinding fails, this is still set and,
      /// once terrain changes, pathing may attempt to resume.
      /// </summary>
      public DoubleVector2 PathingDestination { get; set; }
      // poor datastructure use, but irrelevant for perf 
      public List<DoubleVector2> PathingBreadcrumbs { get; set; } = new List<DoubleVector2>();
      
      /// <summary>
      /// Only when in swarm mode.
      /// </summary>
      public DoubleVector2 SwarmlingVelocity { get; set; }

      /// <summary>
      /// List of other entities in swarm.
      /// </summary>
      public List<Entity> Swarm { get; set; }

      public List<Tuple<DoubleVector2, DoubleVector2>> DebugLines { get; set; }
   }

   public abstract class EntitySystemService {
      private readonly HashSet<Entity> associatedEntities = new HashSet<Entity>();

      protected EntitySystemService(EntityService entityService, EntityComponentsMask requiredComponentsMask) {
         EntityService = entityService;
         RequiredComponentsMask = requiredComponentsMask;
      }

      public EntityService EntityService { get; }
      public EntityComponentsMask RequiredComponentsMask { get; }
      public IEnumerable<Entity> AssociatedEntities => associatedEntities;

      public void AssociateEntity(Entity entity) {
         associatedEntities.Add(entity);
      }

      public abstract void Execute();
   }

   public class PathfinderCalculator {
      private readonly TerrainService terrainService;
      private readonly StatsCalculator statsCalculator;

      public PathfinderCalculator(TerrainService terrainService, StatsCalculator statsCalculator) {
         this.terrainService = terrainService;
         this.statsCalculator = statsCalculator;
      }

      public bool TryFindPath(double holeDilationRadius, DoubleVector2 source, DoubleVector2 destination, out List<DoubleVector2> pathPoints) {
         var terrainSnapshot = terrainService.BuildSnapshot();
         var visibilityGraph = terrainSnapshot.ComputeVisibilityGraph(holeDilationRadius);
         Path path;
         if (!visibilityGraph.TryFindPath(source.LossyToIntVector2(), destination.LossyToIntVector2(), out path)) {
            pathPoints = null;
            return false;
         } else {
            pathPoints = path.Points.Select(p => p.ToDoubleVector2()).ToList();
            pathPoints[0] = source;
            pathPoints[pathPoints.Count - 1] = destination;
            return true;
         }
      }
   }

   public class MovementSystemService : EntitySystemService {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Movement);
      private readonly GameTimeService gameTimeService;
      private readonly StatsCalculator statsCalculator;
      private readonly TerrainService terrainService;
      private readonly PathfinderCalculator pathfinderCalculator;

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

      public void Pathfind(Entity entity, DoubleVector2 destination) {
         var movementComponent = entity.MovementComponent;

         var holeDilationRadius = statsCalculator.ComputeCharacterRadius(entity) + TerrainConstants.AdditionalHoleDilationRadius;
         List<DoubleVector2> pathPoints;
         if (!pathfinderCalculator.TryFindPath(holeDilationRadius, movementComponent.Position, destination, out pathPoints)) {
            movementComponent.PathingBreadcrumbs.Clear();
         } else {
            movementComponent.PathingBreadcrumbs = pathPoints;
         }
         movementComponent.PathingIsInvalidated = false;
         movementComponent.PathingDestination = destination;
      }

      public void HandleHoleAdded(TerrainHole hole) {
         InvalidatePaths();

         foreach (var entity in AssociatedEntities) {
            var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
            var paddedHoleDilationRadius = characterRadius + TerrainConstants.AdditionalHoleDilationRadius + TerrainConstants.TriangleEdgeBufferRadius;
            if (hole.ContainsPoint(paddedHoleDilationRadius, entity.MovementComponent.Position)) {
               FixEntityInHole(entity);
            }
         }
      }

      private void FixEntityInHole(Entity entity) {
         var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
         var paddedHoleDilationRadius = characterRadius + TerrainConstants.AdditionalHoleDilationRadius + TerrainConstants.TriangleEdgeBufferRadius;
         MovementComponent movementComponent = entity.MovementComponent;
         IntVector2 nearestLandPoint;
         if (!terrainService.BuildSnapshot().FindNearestLandPointAndIsInHole(paddedHoleDilationRadius, movementComponent.Position.LossyToIntVector2(), out nearestLandPoint)) {
            throw new InvalidOperationException("In new hole but not terrain snapshot hole.");
         }
         movementComponent.Position = nearestLandPoint.ToDoubleVector2();
      }

      /// <summary>
      /// Invalidates all pathing entities' paths, flagging them for recomputation.
      /// </summary>
      public void InvalidatePaths() {
         foreach (var entity in AssociatedEntities) {
            entity.MovementComponent.PathingIsInvalidated = true;
         }
      }

      public override void Execute() {
         foreach (var entity in AssociatedEntities) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent.PathingIsInvalidated) {
               Pathfind(entity, movementComponent.PathingDestination);
            }

            if (movementComponent.Swarm == null) {
               ExecutePathNonswarmer(entity, movementComponent);
            } else {
               ExecutePathSwarmer(entity, movementComponent);
            }
         }
      }

      private void ExecutePathNonswarmer(Entity entity, MovementComponent movementComponent) {
         if (!movementComponent.PathingBreadcrumbs.Any()) return;

         var movementSpeed = statsCalculator.ComputeMovementSpeed(entity);
         var distanceRemaining = movementSpeed * gameTimeService.SecondsPerTick;
         while (distanceRemaining > 0 && movementComponent.PathingBreadcrumbs.Any()) {
            // vect from position to next pathing breadcrumb
            var pb = movementComponent.PathingBreadcrumbs[0] - movementComponent.Position;
            movementComponent.LookAt = pb;

            // |pb| - distance to next pathing breadcrumb
            var d = pb.Norm2D();

            if (Math.Abs(d) <= float.Epsilon || d <= distanceRemaining) {
               movementComponent.Position = movementComponent.PathingBreadcrumbs[0];
               movementComponent.PathingBreadcrumbs.RemoveAt(0);
               distanceRemaining -= d;
            } else {
               movementComponent.Position += (pb * distanceRemaining) / d;
               distanceRemaining = 0;
            }
         }
      }

      private void ExecutePathSwarmer(Entity entity, MovementComponent movementComponent) {
         var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
         var triangulation = terrainService.BuildSnapshot().ComputeTriangulation(characterRadius);

         // p = position of entity to move (updated incrementally)
         var p = movementComponent.Position;

         // Find triangle we're currently sitting on.
         TriangulationIsland island;
         int triangleIndex;
         if (!triangulation.TryIntersect(p.X, p.Y, out island, out triangleIndex)) {
            Console.WriteLine("Warning: Entity not on land.");
            FixEntityInHole(entity);

            p = movementComponent.Position;
            if (!triangulation.TryIntersect(p.X, p.Y, out island, out triangleIndex)) {
               Console.WriteLine("Warning: fixing entity not on land failed?");
               return;
            }
         }

         // Figure out how much further entity can move this tick
         var preferredDirectionUnit = movementComponent.SwarmlingVelocity.ToUnit();
         var distanceRemaining = movementComponent.SwarmlingVelocity.Norm2D() * gameTimeService.SecondsPerTick;

         movementComponent.Position = CPU(distanceRemaining, p, preferredDirectionUnit, island, triangleIndex);
      }

      private DoubleVector2 CPU(double distanceRemaining, DoubleVector2 p, DoubleVector2 preferredDirectionUnit, TriangulationIsland island, int triangleIndex) {
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

      public enum WalkResult {
         PushInward,
         CanPushInward,
         Progress,
         CanEdgeFollow,
         Completion,
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
         var triangle = island.Triangles[triangleIndex];

         // Find the edge of the triangle that we're potentially moving into. 
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
            var centroid = triangle.Points.Aggregate(new DoubleVector2(), (acc, it) => acc + it) / 3.0;
            var offsetToCentroid = centroid - position;
            if (offsetToCentroid.Norm2D() < TerrainConstants.TriangleEdgeBufferRadius) {
               Console.WriteLine("Warning: Triangle width less than edge buffer radius!");
               nextPosition = centroid;
               nextTriangleIndex = triangleIndex;
               return WalkResult.PushInward;
            } else {
               nextPosition = position + offsetToCentroid.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
               nextTriangleIndex = triangleIndex;
               return WalkResult.PushInward;
            }
         }

         // Let d = remaining "preferred" motion
         var d = preferredDirectionUnit * distanceRemaining;

         // Project p-e0 onto perp(e0-e1) to find shortest vector to edge.
         // Intuitively an edge direction and the direction's perp form a vector
         // space. A point within the triangle's offset from a vertex (which has two edges)
         // is the sum of vector to point on nearest edge and vector from that point to the 
         // vertex. These vectors are orthogonal, so intuitively if we project onto the perp
         // we'll isolate the perp component.
         var e0 = triangle.Points[(opposingVertexIndex + 1) % 3];
         var e1 = triangle.Points[(opposingVertexIndex + 2) % 3];
         var e01 = e1 - e0;
         var e01Perp = new DoubleVector2(e01.Y, -e01.X); // points outside of current triangle.

         var pe0 = e0 - position;
         var pToEdge = pe0.ProjectOnto(e01Perp);

         // If we're sitting right on the edge, push us into the triangle before doing any work
         // Otherwise, it can be ambiguous as to what edge we're passing through on exit.
         // Don't delete this or we'll crash.
         if (pToEdge.Norm2D() < GeometryOperations.kEpsilon) {
            nextPosition = position - e01Perp.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
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

         if (neighborTriangleIndex != Triangle.NO_NEIGHBOR_INDEX) {
            // Move towards and past the edge between us and the other triangle.
            var dToAndPastEdge = dToEdge + dToEdge.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
            nextPosition = position + dToAndPastEdge;
            nextTriangleIndex = neighborTriangleIndex;
            return WalkResult.Progress;
         } else {
            // We're running into an edge! First, place us as close to the edge as possible.
            var dToNearEdge = dToEdge - dToEdge.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
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
            bool allowPushInward = true;
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

   public class StatusComponent : EntityComponent {
      public StatusComponent() : base(EntityComponentType.Status) {}
   }

   public class StatsCalculator {
      public double ComputeCharacterRadius(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) {
            return 0;
         }
         return movementComponent.BaseRadius;
      }

      public double ComputeMovementSpeed(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) {
            return 0;
         }
         return movementComponent.BaseSpeed;
      }
   }

   public class StatusSystemService : EntitySystemService {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Status);

      public StatusSystemService(EntityService entityService) : base(entityService, kComponentMask) {}

      public override void Execute() {
      }
   }
}

