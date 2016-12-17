using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA {
   public class Program {
      public static void Main(string[] args) {
         X();
         return;
         var points = new[] {
            new IntVector2(100, 50),
            new IntVector2(100, 100),
            new IntVector2(200, 100),
            new IntVector2(200, 150),
            new IntVector2(200, 200),
            new IntVector2(400, 250),
            new IntVector2(200, 300),
            new IntVector2(400, 315),
            new IntVector2(200, 330),
            new IntVector2(210, 340),
            new IntVector2(220, 350),
            new IntVector2(220, 400),
            new IntVector2(221, 400)
         };
//         }.Concat(Enumerable.Range(1, 90).Select(i => {
//            var p = new IntVector2(220, 400);
//            var r = IntVector2.FromRadiusAngle(20, i * Math.PI / 180);
//            return p + r;
//         })).ToArray();

         PolylineOperations.ExtrudePolygon(points, 10);
      }

      private static void X() {
         var mapDimensions = new Size(1000, 1000);
         var holes = new[] {
            Polygon.CreateRect(100, 100, 300, 300),
            Polygon.CreateRect(400, 200, 100, 100),
            Polygon.CreateRect(200, -50, 100, 150),
            Polygon.CreateRect(600, 600, 300, 300),
            Polygon.CreateRect(700, 500, 100, 100),
            Polygon.CreateRect(200, 700, 100, 100),
            Polygon.CreateRect(600, 100, 300, 50),
            Polygon.CreateRect(600, 150, 50, 200),
            Polygon.CreateRect(850, 150, 50, 200),
            Polygon.CreateRect(600, 350, 300, 50),
            Polygon.CreateRect(700, 200, 100, 100)
         };

         var holeSquiggle = PolylineOperations.ExtrudePolygon(
            new[] {
               new IntVector2(100, 50),
               new IntVector2(100, 100),
               new IntVector2(200, 100),
               new IntVector2(200, 150),
               new IntVector2(200, 200),
               new IntVector2(400, 250),
               new IntVector2(200, 300),
               new IntVector2(400, 315),
               new IntVector2(200, 330),
               new IntVector2(210, 340),
               new IntVector2(220, 350),
               new IntVector2(220, 400),
               new IntVector2(221, 400)
            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygons();
         holes = holes.Concat(holeSquiggle).ToArray();

         var visibilityGraph = VisibilityGraphOperations.CreateVisibilityGraph(mapDimensions, PolygonOperations.Offset().Include(holes).Dilate(15).Execute().FlattenToPolygons());
         var display = DebugDisplay.CreateShow();
         display.DrawPolygons(holes, Color.Red);
         display.DrawVisibilityGraph(visibilityGraph);
         var testPathFindingQueries = new[] {
            Tuple.Create(new IntVector2(60, 40), new IntVector2(930, 300)),
            Tuple.Create(new IntVector2(675, 175), new IntVector2(825, 300)),
            Tuple.Create(new IntVector2(50, 900), new IntVector2(950, 475)),
            Tuple.Create(new IntVector2(50, 500), new IntVector2(80, 720))
         };

         using (var pen = new Pen(Color.Lime, 2)) {
            foreach (var query in testPathFindingQueries) {
               var path = visibilityGraph.FindPath(query.Item1, query.Item2);
               display.DrawLineStrip(path.Points, pen);
            }
         }
      }
   }
}
