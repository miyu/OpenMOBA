using System.Collections.Generic;

namespace Dargon.PlayOn.Foundation.ECS {
   public abstract class EntitySystem {
      private readonly HashSet<Entity> associatedEntities = new HashSet<Entity>();

      protected EntitySystem(EntityWorld entityWorld, EntityComponentsMask requiredComponentsMask) {
         EntityWorld = entityWorld;
         RequiredComponentsMask = requiredComponentsMask;
      }

      public EntityWorld EntityWorld { get; }
      public EntityComponentsMask RequiredComponentsMask { get; }
      public IEnumerable<Entity> AssociatedEntities => associatedEntities;

      public void AssociateEntity(Entity entity) {
         associatedEntities.Add(entity);
      }

      public void DisassociateEntity(Entity entity) {
         associatedEntities.Remove(entity);
      }

      public virtual void Initialize() { }

      public abstract void Execute();
   }
}