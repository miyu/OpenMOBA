namespace Dargon.PlayOn.Foundation.ECS {
   public abstract class EntityComponent {
      protected EntityComponent(EntityComponentType type) {
         Type = type;
      }

      public EntityComponentType Type { get; }
   }
}