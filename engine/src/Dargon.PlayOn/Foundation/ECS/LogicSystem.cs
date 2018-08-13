#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using Dargon.PlayOn.Foundation.Terrain.Motion;
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.ECS {
   public class LogicSystem : UnorderedEntitySystemBase, INetworkedSystem {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Ai);

      public LogicSystem(EntityWorld entityWorld) : base(entityWorld, kComponentMask) {
      }

      public void ExecuteAiLogic() {
         foreach (var entity in AssociatedEntities) {
            var ai = entity.AiComponent;
            var intent = ai.ComputeIntent();
            intent.Execute(entity);
         }
      }

      public object SaveState() {
         return null;
      }
   }

   public class AiComponentContext {
      private readonly MotionSystem motionSystem;

      public AiComponentContext(MotionSystem motionSystem) {
         this.motionSystem = motionSystem;
      }

      public AiPathToIntent PathTo(Entity entity) {
         throw new System.NotImplementedException();
      }

      public AiIdleIntent Idle() => AiIdleIntent.Instance;
   }

   public abstract class AiIntent {
      public abstract void Execute(Entity entity);
   }

   public class AiIdleIntent : AiIntent {
      public static AiIdleIntent Instance { get; } = new AiIdleIntent();

      public override void Execute(Entity entity) {
      }
   }

   public class AiPathToIntent : AiIntent {
      public override void Execute(Entity entity) {
      }

      public cDouble Distance { get; set; }
   }

   public abstract class AiComponent : EntityComponent {
      protected AiComponent(Entity entity) : base(EntityComponentType.Ai) {
         Entity = entity;
      }

      public Entity Entity { get; }

      public abstract AiIntent ComputeIntent();

      private AiComponentContext AiContext;

      protected AiIdleIntent Idle() => AiContext.Idle();

      protected AiPathToIntent PathTo(Entity entity) => AiContext.PathTo(entity);

      // Attacks or idles (if in attack cd) if in range, else PathTo.
      protected AiPathToIntent BasicAttack(Entity entity) => AiContext.PathTo(entity);
   }

   public class TrivialSeekAiComponent : AiComponent {
      public TrivialSeekAiComponent(Entity entity) : base(entity) { }

      public override AiIntent ComputeIntent() {
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

      public override AiIntent ComputeIntent() {
         var pathToGoal = PathTo(IsResourceHeld ? Nexus : Mine);
         if (pathToGoal.Distance < (cDouble)Entity.MotionComponent.Internals.ComputedStatistics.Radius) {
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

      public override AiIntent ComputeIntent() {
         var visionRange = VisionRange(Entity);
         if (Target != null && IsAlive(Target) && InRange(Target, visionRange)) {
            return BasicAttack(Target);
         }
         var nearest = EnemyTeam.QueryNearest(Entity, isAlive: true);
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