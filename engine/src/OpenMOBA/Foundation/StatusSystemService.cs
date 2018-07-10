namespace OpenMOBA.Foundation {
   public class StatusSystemService : EntitySystemService {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Status);

      public StatusSystemService(EntityWorld entityWorld) : base(entityWorld, kComponentMask) { }

      public override void Execute() { }
   }
}