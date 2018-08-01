namespace Dargon.PlayOn.Foundation.ECS {
   public class LogicSystem : EntitySystem, INetworkedSystem {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Logic);

      public LogicSystem(EntityWorld entityWorld) : base(entityWorld, kComponentMask) {
      }

      public override void Execute() {
      }

      public object SaveState() {
         return null;
      }
   }

   public class AiComponentContext {
      private readonly MovementSystem movementSystem;

      public AiComponentContext(MovementSystem movementSystem) {
         this.movementSystem = movementSystem;
      }
   }

   public abstract class AiCommand {
      public abstract void Execute(Entity entity);
   }

   public class AiIdleCommand : AiCommand {
      public override void Execute(Entity entity) {
      }
   }

   public abstract class AiComponent {
      private dynamic PathTo(Entity entity) {
         throw new System.NotImplementedException();
      }

      private object Idle() {
         throw new System.NotImplementedException();
      }
   }
}