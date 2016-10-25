using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Drawing;

namespace OpenMOBA {
   public class Program {
      public static void Main(string[] args) {
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
         var visibilityGraph = VisibilityGraphOperations.CreateVisibilityGraph(mapDimensions, holes);
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
