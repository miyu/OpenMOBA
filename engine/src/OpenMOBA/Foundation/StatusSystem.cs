namespace OpenMOBA.Foundation {
   public class StatusSystem : EntitySystem {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Status);

      public StatusSystem(EntityWorld entityWorld) : base(entityWorld, kComponentMask) { }

      public override void Execute() { }
   }
}
