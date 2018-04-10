using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ClipperLib;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Geometry;
using cInt = System.Int32;

namespace OpenMOBA.Foundation {
   public class Entity {
      public EntityComponentsMask ComponentMask { get; set; }
      public EntityComponent[] ComponentsByType { get; } = new EntityComponent[(int)EntityComponentType.Count];
      public MovementComponent MovementComponent => (MovementComponent)ComponentsByType[(int)EntityComponentType.Movement];
   }

   public class EntityService {
      private readonly HashSet<Entity> entities = new HashSet<Entity>();
      private readonly List<EntitySystemService> systems = new List<EntitySystemService>();

      public IEnumerable<Entity> EnumerateEntities() {
         return entities;
      }

      public void AddEntitySystem(EntitySystemService system) {
         systems.Add(system);
      }

      public Entity CreateEntity() {
         var entity = new Entity();
         entities.Add(entity);
         return entity;
      }

      public void AddEntityComponent(Entity entity, EntityComponent component) {
         if (entity.ComponentMask.Contains(ComponentMaskUtils.Build(component.Type))) throw new InvalidOperationException("Entity already has component of type " + component.Type);
         entity.ComponentMask = entity.ComponentMask.Or(component.Type);
         entity.ComponentsByType[(int)component.Type] = component;
         foreach (var system in systems) if (entity.ComponentMask.Contains(system.RequiredComponentsMask)) system.AssociateEntity(entity);
      }

      public void ProcessSystems() {
         foreach (var system in systems) system.Execute();
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
         foreach (var componentType in componentTypes) result = result.Or(componentType);
         return result;
      }
   }

   /// <summary>
   ///    Note: A value in this enum is treated as an offset into an array.
   ///    Note: This takes advantage of the first enum member having value 0.
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
      public DoubleVector3 WorldPosition { get; set; }
      public DoubleVector3 LookAt { get; set; } = DoubleVector3.UnitX;
      public float BaseRadius { get; set; }
      public float BaseSpeed { get; set; }

      /// <summary>
      ///    If true, movement will recompute path before updating position
      /// </summary>
      public bool PathingIsInvalidated { get; set; }

      /// <summary>
      ///    The desired destination of the unit. Even if pathfinding fails, this is still set and,
      ///    once terrain changes, pathing may attempt to resume.
      /// </summary>
      public DoubleVector3 PathingDestination { get; set; }

      public MotionRoadmap PathingRoadmap { get; set; } = null;
      public int PathingRoadmapProgressIndex = -1;

      public Swarm Swarm { get; set; }

      public List<Tuple<DoubleVector3, DoubleVector3>> DebugLines { get; set; }

      // Values precomputed at entry of movement service
      public int ComputedRadius { get; set; }
      public int ComputedSpeed { get; set; }
      public DoubleVector2 WeightedSumNBodyForces { get; set; }
      public double SumWeightsNBodyForces { get; set; }
      public TerrainOverlayNetwork TerrainOverlayNetwork { get; set; }
      public TerrainOverlayNetworkNode TerrainOverlayNetworkNode { get; set; }
      public DoubleVector2 LocalPosition { get; set; }
      public TriangulationIsland SwarmingIsland { get; set; }
      public int SwarmingTriangleIndex { get; set; }

      // Final computed swarmling velocity
      public DoubleVector2 SwarmlingVelocity { get; set; }
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

   public static class IntMath {
      private const int MaxLutIntExclusive = 1024 * 1024;
      private static readonly cInt[] SqrtLut = Enumerable.Range(0, MaxLutIntExclusive).Select(x => (cInt)Math.Sqrt(x)).ToArray();

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static cInt Square(cInt x) {
         return x * x;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static cInt Quad(cInt x) {
         return Square(Square(x));
      }

      public static cInt Sqrt(cInt x) {
         if (x < 0) throw new ArgumentException($"sqrti({x})");
         else if (x < MaxLutIntExclusive) return SqrtLut[x];
         else return (cInt)Math.Sqrt(x);
      }
   }

   public class MotionRoadmap {
      public List<MotionRoadmapAction> Plan = new List<MotionRoadmapAction>();
   }

   public abstract class MotionRoadmapAction { }

   public class MotionRoadmapWalkAction : MotionRoadmapAction {
      public MotionRoadmapWalkAction(TerrainOverlayNetworkNode node, IntVector2 source, IntVector2 destination) {
         Node = node;
         Source = source;
         Destination = destination;
      }

      public readonly TerrainOverlayNetworkNode Node;
      public readonly IntVector2 Source;
      public readonly IntVector2 Destination;
   }

   public class StatusComponent : EntityComponent {
      public StatusComponent() : base(EntityComponentType.Status) { }
   }

   public class StatsCalculator {
      public double ComputeCharacterRadius(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) return 0;
         return movementComponent.BaseRadius;
      }

      public double ComputeMovementSpeed(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) return 0;
         return movementComponent.BaseSpeed;
      }
   }

   public class StatusSystemService : EntitySystemService {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Status);

      public StatusSystemService(EntityService entityService) : base(entityService, kComponentMask) { }

      public override void Execute() { }
   }
}

