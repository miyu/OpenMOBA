namespace Dargon.Terragami.Geometry {
   public enum PolygonContainmentResult {
      // These three values are defined by Clipper
      OutsidePolygon = 0,
      OnPolygon = -1,
      InPolygon = 1,

      // This value is defined by miyu, and will not be returned by Clipper.
      IntersectsPolygon = 2
   }
}