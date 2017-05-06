using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation;
using OpenMOBA.Utilities;

namespace OpenMOBA {
   public class Program {
      public static void Main(string[] args) {
         var gameInstance = new GameInstanceFactory().Create();
         gameInstance.Run();
      }

      private static void H() {
         var points = new[] {
            new IntVector3(100, 50, 0),
            new IntVector3(100, 100, 0),
            new IntVector3(200, 100, 0),
            new IntVector3(200, 150, 0),
            new IntVector3(200, 200, 0),
            new IntVector3(400, 250, 0),
            new IntVector3(200, 300, 0),
            new IntVector3(400, 315, 0),
            new IntVector3(200, 330, 0),
            new IntVector3(210, 340, 0),
            new IntVector3(220, 350, 0),
            new IntVector3(220, 400, 0),
            new IntVector3(221, 400, 0)
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
            Polygon.CreateRectXY(100, 100, 300, 300, 0),
            Polygon.CreateRectXY(400, 200, 100, 100, 0),
            Polygon.CreateRectXY(200, -50, 100, 150, 0),
            Polygon.CreateRectXY(600, 600, 300, 300, 0),
            Polygon.CreateRectXY(700, 500, 100, 100, 0),
            Polygon.CreateRectXY(200, 700, 100, 100, 0),
            Polygon.CreateRectXY(600, 100, 300, 50, 0),
            Polygon.CreateRectXY(600, 150, 50, 200, 0),
            Polygon.CreateRectXY(850, 150, 50, 200, 0),
            Polygon.CreateRectXY(600, 350, 300, 50, 0),
            Polygon.CreateRectXY(700, 200, 100, 100, 0)
         };

         var holeSquiggle = PolylineOperations.ExtrudePolygon(
            new[] {
               new IntVector3(100, 50, 0),
               new IntVector3(100, 100, 0),
               new IntVector3(200, 100, 0),
               new IntVector3(200, 150, 0),
               new IntVector3(200, 200, 0),
               new IntVector3(400, 250, 0),
               new IntVector3(200, 300, 0),
               new IntVector3(400, 315, 0),
               new IntVector3(200, 330, 0),
               new IntVector3(210, 340, 0),
               new IntVector3(220, 350, 0),
               new IntVector3(220, 400, 0),
               new IntVector3(221, 400, 0)
            }.Select(iv => new IntVector3(iv.X + 160, iv.Y + 200, iv.Z)).ToArray(), 10).FlattenToPolygons();
         holes = holes.Concat(holeSquiggle).ToArray();

         var landPoly = Polygon.CreateRectXY(0, 0, 1000, 1000, 0);
         var holesUnionResult = PolygonOperations.Offset()
                                                 .Include(holes)
                                                 .Include(holeSquiggle)
                                                 .Dilate(15)
                                                 .Execute();
         var landHolePunchResult = PolygonOperations.Punch()
                                                    .Include(landPoly)
                                                    .Exclude(holesUnionResult.FlattenToPolygons())
                                                    .Execute();
         var visibilityGraph = VisibilityGraphOperations.CreateVisibilityGraph(landHolePunchResult);
         var debugCanvas = DebugCanvasHost.CreateAndShowCanvas();
         debugCanvas.DrawPolygons(holes, Color.Red);
         debugCanvas.DrawVisibilityGraph(visibilityGraph);
         var testPathFindingQueries = new[] {
            Tuple.Create(new IntVector2(60, 40), new IntVector2(930, 300)),
            Tuple.Create(new IntVector2(675, 175), new IntVector2(825, 300)),
            Tuple.Create(new IntVector2(50, 900), new IntVector2(950, 475)),
            Tuple.Create(new IntVector2(50, 500), new IntVector2(80, 720))
         };

//         using (var pen = new Pen(Color.Lime, 2)) {
//            foreach (var query in testPathFindingQueries) {
//               Path path;
//               if (visibilityGraph.TryFindPath(query.Item1, query.Item2, out path))
//                  debugCanvas.DrawLineStrip(path.Points, pen);
//            }
//         }
      }
   }
}
