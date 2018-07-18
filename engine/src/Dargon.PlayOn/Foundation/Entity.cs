namespace Dargon.PlayOn.Foundation {
   public class Entity {
      public int Id { get; set; }
      public EntityComponentsMask ComponentMask { get; set; }
      public EntityComponent[] ComponentsByType { get; } = new EntityComponent[(int)EntityComponentType.Count];
      public MovementComponent MovementComponent => (MovementComponent)ComponentsByType[(int)EntityComponentType.Movement];

      private Entity(int id) {
         this.Id = id;
      }

      internal static Entity CreateEntity_OnlyInvokedFromWorldOrIO(int id) => new Entity(id);
   }
}