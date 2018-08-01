namespace Dargon.PlayOn.Foundation.ECS {
   /// <summary>
   ///    Note: A value in this enum is treated as an offset into an array.
   ///    Note: This takes advantage of the first enum member having value 0.
   /// </summary>
   public enum EntityComponentType {
      Movement,
      Logic,
      Status,

      Count
   }
}