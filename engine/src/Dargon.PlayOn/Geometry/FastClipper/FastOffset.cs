namespace Dargon.PlayOn.Geometry.FastClipper {
   public static class FastOffset {
      public static void ErodeTriangle(Polygon2 triangle) {
         var p1 = triangle.Points[0];
         var p2 = triangle.Points[1];
         var p3 = triangle.Points[2];

         // p1P2.y, -p1P2.x
         var p1P2Perp = new IntVector2(p2.Y - p1.Y, p1.X - p2.X);
         var p2P3Perp = new IntVector2(p3.Y - p2.Y, p2.X - p3.X);
         var p3P1Perp = new IntVector2(p1.Y - p3.Y, p3.X - p1.X);
      }
   }
}
