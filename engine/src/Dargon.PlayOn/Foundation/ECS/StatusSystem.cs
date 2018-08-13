namespace Dargon.PlayOn.Foundation.ECS {
   public class StatusSystem : UnorderedEntitySystemBase {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Status);

      public StatusSystem(EntityWorld entityWorld) : base(entityWorld, kComponentMask) { }
   }
}
