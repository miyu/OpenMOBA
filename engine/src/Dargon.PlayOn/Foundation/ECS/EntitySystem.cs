using System.Collections.Generic;
using System.Diagnostics;
using Dargon.Commons;
using Dargon.PlayOn.DataStructures;

namespace Dargon.PlayOn.Foundation.ECS {
   public interface IEntitySystem {
      EntityWorld EntityWorld { get; }
      EntityComponentsMask RequiredComponentsMask { get; }
      IEnumerable<Entity> AssociatedEntities { get; }
      void AssociateEntity(Entity entity);
      void DisassociateEntity(Entity entity);
      void Execute();
   }

   public abstract class UnorderedEntitySystemBase {
      private readonly HashSet<Entity> associatedEntities = new HashSet<Entity>();

      protected UnorderedEntitySystemBase(EntityWorld entityWorld, EntityComponentsMask requiredComponentsMask) {
         EntityWorld = entityWorld;
         RequiredComponentsMask = requiredComponentsMask;
      }

      public EntityWorld EntityWorld { get; }
      public EntityComponentsMask RequiredComponentsMask { get; }
      public virtual IEnumerable<Entity> AssociatedEntities => associatedEntities;

      public virtual void AssociateEntity(Entity entity) {
         associatedEntities.Add(entity);
      }

      public virtual void DisassociateEntity(Entity entity) {
         associatedEntities.Remove(entity);
      }

      public abstract void Execute();
   }

   public abstract class OrderedEntitySystemBase {
      private readonly RemovalPermittingOrderedHashSet<Entity> associatedEntities = new RemovalPermittingOrderedHashSet<Entity>();

      protected OrderedEntitySystemBase(EntityWorld entityWorld, EntityComponentsMask requiredComponentsMask) {
         EntityWorld = entityWorld;
         RequiredComponentsMask = requiredComponentsMask;
      }

      public EntityWorld EntityWorld { get; }
      public EntityComponentsMask RequiredComponentsMask { get; }
      public virtual IEnumerable<Entity> AssociatedEntities => associatedEntities;

      public virtual void AssociateEntity(Entity entity) {
         associatedEntities.TryAdd(entity, out var addedIndex);
         Assert.Equals(addedIndex, associatedEntities.Count - 1);

         HandleEntityAssociated(entity);
      }

      public virtual void DisassociateEntity(Entity entity) {
         associatedEntities.TryRemove(entity, out var removedIndex, out var replacementIndex);
         HandleEntityDisassociated(entity, removedIndex, replacementIndex);
      }

      public abstract void HandleEntityAssociated(Entity entity);

      public abstract void HandleEntityDisassociated(Entity entity, int removedIndex, int replacementIndex);

      public abstract void Execute();
   }
}