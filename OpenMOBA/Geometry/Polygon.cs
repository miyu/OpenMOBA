using System.Collections.Generic;
using System.Linq;

namespace OpenMOBA.Geometry {
   public class Polygon {
      public Polygon(List<IntVector2> points, bool isHole) {
         if (points[0] != points.Last()) {
            //            Console.WriteLine("Warn: Polygon took open (non-closed) poly");
            points.Add(points[0]);
         }

         Points = points;
         IsHole = isHole;
         IsClosed = Points.First() == Points.Last();
      }

      public List<IntVector2> Points { get; set; }
      public bool IsHole { get; set; }
      public bool IsClosed { get; set; }

      public static Polygon CreateRect(int x, int y, int width, int height) {
         var points = new List<IntVector2> {
            new IntVector2(x, y),
            new IntVector2(x + width, y),
            new IntVector2(x + width, y + height),
            new IntVector2(x, y + height),
            new IntVector2(x, y)
         };
         return new Polygon(points, true);
      }
   }
}
