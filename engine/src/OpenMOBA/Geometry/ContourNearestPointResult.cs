#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Geometry {
   public struct ContourNearestPointResult3 {
      public cDouble Distance { get; set; }
      public int SegmentFirstPointContourIndex { get; set; }
      public DoubleVector3 Query { get; set; }
      public DoubleVector3 NearestPoint { get; set; }
   }

   public struct ContourNearestPointResult2 {
      public cDouble Distance { get; set; }
      public int SegmentFirstPointContourIndex { get; set; }
      public DoubleVector2 Query { get; set; }
      public DoubleVector2 NearestPoint { get; set; }
   }
}