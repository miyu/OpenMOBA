using System.Collections.Generic;
using System.Linq;

namespace OpenMOBA.Geometry {
   public class Polygon {
      public Polygon(List<IntVector3> points, bool isHole) {
         if (points[0] != points.Last()) {
            //            Console.WriteLine("Warn: Polygon took open (non-closed) poly");
            points.Add(points[0]);
         }

         Points = points;
         IsHole = isHole;
         IsClosed = Points.First() == Points.Last();
      }

      public List<IntVector3> Points { get; set; }
      public bool IsHole { get; set; }
      public bool IsClosed { get; set; }

      public static Polygon CreateRectXY(int x, int y, int width, int height, int z) {
         var points = new List<IntVector3> {
            new IntVector3(x, y, z),
            new IntVector3(x + width, y, z),
            new IntVector3(x + width, y + height, z),
            new IntVector3(x, y + height, z),
            new IntVector3(x, y, z)
         };
         return new Polygon(points, true);
      }
   }
}
