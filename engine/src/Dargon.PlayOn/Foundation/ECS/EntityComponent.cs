namespace Dargon.PlayOn.Foundation.ECS {
   public abstract class EntityComponent {
      protected EntityComponent(EntityComponentType type) {
         Type = type;
      }

      public Entity Entity { get; } // populated by world AddComponent
      public EntityComponentType Type { get; }
   }
}