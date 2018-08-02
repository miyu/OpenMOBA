#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

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

      public AiPathToCommand PathTo(Entity entity) {
         throw new System.NotImplementedException();
      }

      public AiIdleCommand Idle() => AiIdleCommand.Instance;
   }

   public abstract class AiCommand {
      public abstract void Execute(Entity entity);
   }

   public class AiIdleCommand : AiCommand {
      public static AiIdleCommand Instance { get; } = new AiIdleCommand();

      public override void Execute(Entity entity) {
      }
   }

   public class AiPathToCommand : AiCommand {
      public override void Execute(Entity entity) {
      }

      public cDouble Distance { get; set; }
   }

   public abstract class AiComponent : EntityComponent {
      protected AiComponent(Entity entity) : base(EntityComponentType.Logic) {
         Entity = entity;
      }

      public Entity Entity { get; }

      public abstract AiCommand ComputeIntent();

      private AiComponentContext AiContext;

      protected AiIdleCommand Idle() => AiContext.Idle();

      protected AiPathToCommand PathTo(Entity entity) => AiContext.PathTo(entity);

      // Attacks or idles (if in attack cd) if in range, else PathTo.
      protected AiPathToCommand BasicAttack(Entity entity) => AiContext.PathTo(entity);
   }

   public class TrivialSeekAiComponent : AiComponent {
      public TrivialSeekAiComponent(Entity entity) : base(entity) { }

      public override AiCommand ComputeIntent() {
         var target = (Entity)null;
         var pathToTargetCommand = PathTo(target);
         if (pathToTargetCommand.Distance < 10) {
            return pathToTargetCommand;
         }
         return Idle();
      }
   }

   public class CollectorAiComponent : AiComponent {
      public CollectorAiComponent(Entity entity, Entity mine, Entity nexus) : base(entity) {
         Mine = mine;
         Nexus = nexus;
      }

      public Entity Mine { get; }
      public Entity Nexus { get; }
      public bool IsResourceHeld;

      public override AiCommand ComputeIntent() {
         var pathToGoal = PathTo(IsResourceHeld ? Nexus : Mine);
         if (pathToGoal.Distance < (cDouble)Entity.MovementComponent.ComputedRadius) {
            IsResourceHeld = !IsResourceHeld;
            pathToGoal = PathTo(IsResourceHeld ? Nexus : Mine);
         }
         return pathToGoal;
      }
   }

   public class MinionAiComponent : AiComponent {
      public MinionAiComponent(Entity entity, Entity enemyNexus, dynamic enemyTeam) : base(entity) {
         EnemyNexus = enemyNexus;
         EnemyTeam = enemyTeam;
      }

      public Entity EnemyNexus { get; }
      public dynamic EnemyTeam { get; }
      public Entity Target { get; private set; }

      public override AiCommand ComputeIntent() {
         var visionRange = VisionRange(Entity);
         if (Target != null && IsAlive(Target) && InRange(Target, visionRange)) {
            return BasicAttack(Target);
         }
         var nearest = (Entity)EnemyTeam.QueryNearest(Entity, isAlive: true);
         if (nearest != null && InRange(nearest, visionRange)) {
            Target = nearest;
            return BasicAttack(Target);
         }
         return BasicAttack(EnemyNexus);
      }

      private bool IsAlive(Entity target) => true;
      private bool InRange(Entity target, cDouble range) => true;
      private cDouble VisionRange(Entity target) => CDoubleMath.c0;
   }
}