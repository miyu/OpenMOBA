using System;
using System.Collections.Generic;

namespace OpenMOBA.Foundation {
   public class Entity {
      public EntityComponentsMask ComponentMask { get; set; }
      public EntityComponent[] ComponentsByType { get; } = new EntityComponent[(int)EntityComponentType.Count];
      public MovementComponent MovementComponent => (MovementComponent)ComponentsByType[(int)EntityComponentType.Movement];
   }

   public class EntityService {
      private readonly List<EntitySystemService> systems = new List<EntitySystemService>();
      private readonly HashSet<Entity> entities = new HashSet<Entity>();

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
         foreach (var system in systems) {
            if (entity.ComponentMask.Contains(system.RequiredComponentsMask)) {
               system.AddEntity(entity);
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
      public MovementComponent() : base(EntityComponentType.Movement) {}
      public Stack<IMovementDirective> DirectiveStack { get; } = new Stack<IMovementDirective>();
   }

   public interface IMovementDirective { }

   public class KnockbackMovementDirective : IMovementDirective {

   }

   public class PathFollowMovementDirective : IMovementDirective {

   }

   public abstract class EntitySystemService {
      private readonly HashSet<Entity> entities = new HashSet<Entity>();

      protected EntitySystemService(EntityService entityService, EntityComponentsMask requiredComponentsMask) {
         EntityService = entityService;
         RequiredComponentsMask = requiredComponentsMask;
      }

      public EntityService EntityService { get; }
      public EntityComponentsMask RequiredComponentsMask { get; }

      public void AddEntity(Entity entity) {
         entities.Add(entity);
      }

      public abstract void Execute();
   }

   public class MovementSystemService : EntitySystemService {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Movement);

      public MovementSystemService(
         EntityService entityService
      ) : base(entityService, kComponentMask) {
      }

      public override void Execute() {
          
      }
   }
}