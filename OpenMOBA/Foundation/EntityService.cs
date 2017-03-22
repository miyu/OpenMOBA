using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
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
            var movementComponent = entity.MovementComponent;
            var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
            var paddedHoleDilationRadius = characterRadius + TerrainConstants.AdditionalHoleDilationRadius + TerrainConstants.TriangleEdgeBufferRadius;
            if (hole.ContainsPoint(paddedHoleDilationRadius, movementComponent.Position)) {
               IntVector2 nearestLandPoint;
               if (!terrainService.BuildSnapshot().FindNearestLandPointAndIsInHole(paddedHoleDilationRadius, movementComponent.Position.LossyToIntVector2(), out nearestLandPoint)) {
                  throw new InvalidOperationException("In new hole but not terrain snapshot hole.");
               }
               movementComponent.Position = nearestLandPoint.ToDoubleVector2();
            }
         }
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
         var triangulation = terrainService.BuildSnapshot().ComputeTriangulation(characterRadius + TerrainConstants.AdditionalHoleDilationRadius);

         // p = position of entity to move (updated incrementally)
         var p = movementComponent.Position;

         // d = remaining distance vector of entity to move
         var d = movementComponent.SwarmlingVelocity * gameTimeService.SecondsPerTick;
         var distanceRemaining = d.Norm2D();

         // Find triangle we're currently sitting on.
         TriangulationIsland island;
         Triangle triangle;
         if (!triangulation.TryIntersect(p.X, p.Y, out island, out triangle)) {
            Console.WriteLine("Warning: Entity not on land.");
            return;
         }

         movementComponent.DebugLines = new List<Tuple<DoubleVector2, DoubleVector2>>();

         while (distanceRemaining > GeometryOperations.kEpsilon) {
            // opposingVertexIndex = index of vertex opposing edge (e[0], e[1]) our ray of motion intersects.
            int opposingVertexIndex;
            if (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(p, d, ref triangle, out opposingVertexIndex)) {
               // we're presumably on an edge.
               for (int i = 0; i < 3; i++) {
                  var a = triangle.Points[i];
                  var b = triangle.Points[(i + 1) % 3];
                  if (GeometryOperations.Clockness(a, p, b) == Clockness.Neither) {
                     var ab = b - a;
                     var abPerp = new DoubleVector2(ab.Y, -ab.X);
                     p -= abPerp.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
                     continue;
                  }
               }
            }

            // Project p-e0 onto perp(e0-e1) to find shortest vector to edge.
            var e0 = triangle.Points[(opposingVertexIndex + 1) % 3];
            var e1 = triangle.Points[(opposingVertexIndex + 2) % 3];
            var e01 = e1 - e0;
            var e01Perp = new DoubleVector2(e01.Y, -e01.X); // points outside of current triangle.

            var pe0 = e0 - p;
            var pToEdge = pe0.ProjectOnto(e01Perp);

            // Project d onto pToEdge to see if we're moving beyond edge boundary
            var pToEdgeComponentRemaining = d.ProjectOntoComponentD(pToEdge);

//            Console.WriteLine("p " + p);
//            Console.WriteLine("d " + d);
//            Console.WriteLine("tr " + triangle.Points[0] + " " + triangle.Points[1] + " " + triangle.Points[2]);
//            Console.WriteLine("e01perp " + e01Perp);
//            Console.WriteLine("pToEdge " + pToEdge);
//            Console.WriteLine("CR " + pToEdgeComponentRemaining);

            for (var i = 0; i < 3; i++) {
               movementComponent.DebugLines.Add(Tuple.Create(p, triangle.Points[i]));
            }

            // If we're sitting right on the edge, push us into the triangle before doing any work
            // Otherwise, it's ambiguous as to what edge we're passing through on exit.
            if (pToEdge.Norm2D() < GeometryOperations.kEpsilon) {
               p -= e01Perp.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
               continue;
            }

            if (pToEdgeComponentRemaining < 1) {
               // Finish motion, don't leave triangle
               // TODO: Handle when this gets us very close to triangle edge e.g. cR = 0.99999.
               p += d;
               d = DoubleVector2.Zero;
               distanceRemaining = 0;
            } else {
               // TODO: This is slightly worrying code when dealing with triangle slivers.
               // A better approach might dilate the triangles to ensure in-ness of our points,
               // or pull points towards (but not past) the triangle centroid.

               // Adding d would go out of triangle. Move to edge.
               // Ensure TriangleEdgeBufferRadius respected.
               var neighborTriangleIndex = triangle.NeighborOppositePointIndices[opposingVertexIndex];
//               Console.WriteLine("NTI " + neighborTriangleIndex);

               if (neighborTriangleIndex == Triangle.NO_NEIGHBOR_INDEX) {
                  // Move near edge of this triangle
                  var deltaP = d / pToEdgeComponentRemaining;
                  deltaP -= deltaP.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
                  p += deltaP;
                  d -= deltaP;
                  distanceRemaining = 0;
               } else {
                  // Move to and past edge of other triangle. 
                  var deltaP = d / pToEdgeComponentRemaining;
                  deltaP += deltaP.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
                  p += deltaP;
                  d -= deltaP;
                  distanceRemaining -= deltaP.Norm2D();

                  triangle = island.Triangles[neighborTriangleIndex];
               }
            }
         }

//         Console.WriteLine("p' " + p);
         movementComponent.Position = p;
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

