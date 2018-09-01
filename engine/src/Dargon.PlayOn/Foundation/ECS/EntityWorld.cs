using System;
using System.Collections.Generic;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.ECS {
   public class EntityWorld {
      // TODO: It'll likely be desirable to maintain in-order semantics for determinism
      // Framework can (and probably will) roll randomness into hashcode (and thus
      // hashset order) to make less predictable vs attackers? -- warty
      private readonly HashSet<Entity> entities = new HashSet<Entity>();
      private readonly List<IEntitySystem> systems = new List<IEntitySystem>();
      private readonly SortedDictionary<StepHandlerPriority, Action> orderedStepHandlers = new SortedDictionary<StepHandlerPriority, Action>();
      private int nextEntityId = 0;

      public IEnumerable<Entity> EnumerateEntities() => entities;

      public Entity CreateEntity() {
         var entityId = nextEntityId++;
         var entity = Entity.CreateEntity_OnlyInvokedFromWorldOrIO(entityId);
         entities.Add(entity);
         return entity;
      }

      public void AddEntityComponent(Entity entity, EntityComponent component) {
         if (entity.ComponentMask.Contains(ComponentMaskUtils.Build(component.Type))) throw new InvalidOperationException("Entity already has component of type " + component.Type);
         entity.ComponentMask = entity.ComponentMask.Or(component.Type);
         entity.ComponentsByType[(int)component.Type] = component;
         foreach (var system in systems) {
            if (entity.ComponentMask.Contains(system.RequiredComponentsMask)) {
               system.AssociateEntity(entity);
            }
         }
      }

      public void RemoveEntity(Entity entity) {
         if (!entities.Remove(entity)) {
            throw new InvalidOperationException();
         }
         foreach (var system in systems) {
            if (entity.ComponentMask.Contains(system.RequiredComponentsMask)) {
               system.DisassociateEntity(entity);
            }
         }
      }

      public IReadOnlyList<IEntitySystem> EnumerateSystems() => systems;

      public void AddEntitySystem(IEntitySystem system) {
         systems.Add(system);
      }

      public void AddStepHandler(StepHandlerPriority priority, Action callback) => orderedStepHandlers.Add(priority, callback);

      public void ExecuteStepHandlers() {
         foreach (var (priority, stepHandler) in orderedStepHandlers) {
            stepHandler.Invoke();
         }
      }
   }

   public static class EntityWorldExtensions {
      public static Entity CreateEntity(this EntityWorld world, EntityComponent c0) {
         var entity = world.CreateEntity();
         world.AddEntityComponent(entity, c0);
         return entity;
      }

      public static Entity CreateEntity(this EntityWorld world, EntityComponent c0, EntityComponent c1) {
         var entity = world.CreateEntity();
         world.AddEntityComponent(entity, c0);
         world.AddEntityComponent(entity, c1);
         return entity;
      }

      public static Entity CreateEntity(this EntityWorld world, params EntityComponent[] components) {
         var entity = world.CreateEntity();
         foreach (var component in components) {
            world.AddEntityComponent(entity, component);
         }
         return entity;
      }
   }
}
