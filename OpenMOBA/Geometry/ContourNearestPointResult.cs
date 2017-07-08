namespace OpenMOBA.Geometry {
   public struct ContourNearestPointResult3 {
      public double Distance { get; set; }
      public int SegmentFirstPointContourIndex { get; set; }
      public DoubleVector3 Query { get; set; }
      public DoubleVector3 NearestPoint { get; set; }
   }

   public struct ContourNearestPointResult2 {
      public double Distance { get; set; }
      public int SegmentFirstPointContourIndex { get; set; }
      public DoubleVector2 Query { get; set; }
      public DoubleVector2 NearestPoint { get; set; }
   }
}