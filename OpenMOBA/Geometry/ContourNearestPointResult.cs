namespace OpenMOBA.Geometry {
   public struct ContourNearestPointResult {
      public double Distance { get; set; }
      public int SegmentFirstPointContourIndex { get; set; }
      public DoubleVector3 Query { get; set; }
      public DoubleVector3 NearestPoint { get; set; }
   }
}