namespace OpenMOBA.Geometry {
   public struct ContourNearestPointResult {
      public float Distance { get; set; }
      public int SegmentFirstPointContourIndex { get; set; }
      public IntVector2 Query { get; set; }
      public IntVector2 NearestPoint { get; set; }
   }
}