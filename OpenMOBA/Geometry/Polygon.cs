using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenMOBA.Geometry {
   public class Polygon {
      public Polygon(List<IntVector3> points, bool isHole) {
         // enforce closed poly
         if (points[0] != points.Last()) {
            //            Console.WriteLine("Warn: Polygon took open (non-closed) poly");
            points.Add(points[0]);
         }

//#if DEBUG
//         // This test doesn't work while polyline dilate doesn't know about holeness
//         for (var i = 0; i < points.Count - 2; i++) {
//            var clockness = GeometryOperations.Clockness(points[i].XY, points[i + 1].XY, points[i + 2].XY);
//            var expectedClockness = isHole ? Clockness.CounterClockwise : Clockness.Clockwise;
//            Debug.Assert(clockness == expectedClockness);
//         }
//#endif

         Points = points;
         IsHole = isHole;
      }

      public List<IntVector3> Points { get; set; }
      public bool IsHole { get; set; }
      public bool IsClosed => true;

      public static Polygon CreateRectXY(int x, int y, int width, int height, int z) {
         var points = new List<IntVector3> {
            new IntVector3(x, y, z),
            new IntVector3(x, y + height, z),
            new IntVector3(x + width, y + height, z),
            new IntVector3(x + width, y, z),
            new IntVector3(x, y, z)
         };
         return new Polygon(points, true);
      }
   }
}
