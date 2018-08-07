namespace Dargon.PlayOn.Foundation.ECS {
   public class Entity {
      public int Id { get; set; }
      public EntityComponentsMask ComponentMask { get; set; }
      public EntityComponent[] ComponentsByType { get; } = new EntityComponent[(int)EntityComponentType.Count];
      public MotionComponent MotionComponent => (MotionComponent)ComponentsByType[(int)EntityComponentType.Movement];
      public AiComponent AiComponent => (AiComponent)ComponentsByType[(int)EntityComponentType.Ai];

      private Entity(int id) {
         this.Id = id;
      }

      internal static Entity CreateEntity_OnlyInvokedFromWorldOrIO(int id) => new Entity(id);
   }
}