using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using OpenMOBA.Debugging;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Geometry;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Utilities;

namespace OpenMOBA.DevTool {
   public static class Program {
      public static void Main(string[] args) {
         var gameFactory = new GameFactory();
         gameFactory.GameCreated += (s, game) => {
            GameDebugger.AttachTo(game);
         };
         OpenMOBA.Program.Main(gameFactory);
      }
   }

   public class GameDebugger : IGameDebugger {
      private static readonly Pen PathPen = new Pen(Color.Lime, 2);

      public GameDebugger(Game game, DebugMultiCanvasHost debugMultiCanvasHost) {
         Game = game;
         DebugMultiCanvasHost = debugMultiCanvasHost;
      }

      public Game Game { get; }
      public DebugMultiCanvasHost DebugMultiCanvasHost { get; }

      private DebugProfiler DebugProfiler => Game.DebugProfiler;
      private GameTimeService GameTimeService => Game.GameTimeService;
      private MapConfiguration MapConfiguration => Game.MapConfiguration;
      private TerrainService TerrainService => Game.TerrainService;
      private EntityService EntityService => Game.EntityService;

      public void HandleFrameEnd(FrameEndStatistics frameStatistics) {
         if (GameTimeService.Ticks == 0) {
            AddSquiggleHole();
         }
         if (frameStatistics.EventsProcessed != 0 || GameTimeService.Ticks % 1 == 0) {
            RenderDebugFrame();
         }
      }

      private void AddSquiggleHole() {
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
         TerrainService.AddTemporaryHole(new TerrainHole{ Polygons = holeSquiggle });
      }

      private void RenderDebugFrame() {
         var terrainSnapshot = TerrainService.BuildSnapshot();
         var debugCanvas = DebugMultiCanvasHost.CreateAndAddCanvas(GameTimeService.Ticks);

         var temporaryHolePolygons = terrainSnapshot.TemporaryHoles.SelectMany(th => th.Polygons).ToList();
         var holeDilationRadius = 15.0;
         var visibilityGraph = terrainSnapshot.ComputeVisibilityGraph(holeDilationRadius);
         debugCanvas.Draw(g => {
            debugCanvas.DrawPolyTree(terrainSnapshot.ComputePunchedLand(0));
//            debugCanvas.DrawPolyTree(terrainSnapshot.ComputePunchedLand(holeDilationRadius));
//            debugCanvas.DrawPolygons(temporaryHolePolygons, Color.Red);
            debugCanvas.DrawTriangulation(terrainSnapshot.ComputeTriangulation(holeDilationRadius), Pens.DarkGray);
            DrawTriangulationQuadTree(debugCanvas, terrainSnapshot.ComputeTriangulation(holeDilationRadius));
//            debugCanvas.DrawVisibilityGraph(visibilityGraph);
//            DrawWallPushGrid(debugCanvas, terrainSnapshot, holeDilationRadius);

            var testPathFindingQueries = new[] {
               Tuple.Create(new DoubleVector3(60, 40, 0), new DoubleVector3(930, 300, 0)),
               Tuple.Create(new DoubleVector3(675, 175, 0), new DoubleVector3(825, 300, 0)),
               Tuple.Create(new DoubleVector3(50, 900, 0), new DoubleVector3(950, 475, 0)),
               Tuple.Create(new DoubleVector3(50, 500, 0), new DoubleVector3(80, 720, 0))
            };

            foreach (var query in testPathFindingQueries) {
               List<DoubleVector3> pathPoints3d;
               if (Game.PathfinderCalculator.TryFindPath(holeDilationRadius, query.Item1, query.Item2, out pathPoints3d)) {
                  var pathPoints = pathPoints3d.Select(p => p.XY.LossyToIntVector2()).ToList();
                  debugCanvas.DrawLineStrip(pathPoints, PathPen);
               }
            }

            foreach (var entity in EntityService.EnumerateEntities()) {
               var movementComponent = entity.MovementComponent;
               if (movementComponent == null) continue;
               var pathPoints = entity.MovementComponent.PathingBreadcrumbs.Select(p => p.XY.LossyToIntVector2()).ToList();
               pathPoints.Insert(0, movementComponent.Position.XY.LossyToIntVector2());
               debugCanvas.DrawLineStrip(pathPoints, PathPen);
            }

            using (var highlightPen = new Pen(Brushes.Red, 3.0f)) {
               foreach (var entity in EntityService.EnumerateEntities()) {
                  var movementComponent = entity.MovementComponent;
                  if (movementComponent != null) {
                     var triangulation = terrainSnapshot.ComputeTriangulation(movementComponent.BaseRadius);
                     TriangulationIsland island;
                     int triangleIndex;
                     if (triangulation.TryIntersect(movementComponent.Position.X, movementComponent.Position.Y, out island, out triangleIndex)) {
                        debugCanvas.DrawTriangle(island.Triangles[triangleIndex], highlightPen);
                     }
                  }
               }
            }

            //            var losRadius = EntityService.EnumerateEntities().First().MovementComponent.BaseRadius;
            //            var waypoint = TerrainService.BuildSnapshot().ComputeVisibilityGraph(losRadius).Waypoints[5];
            //            debugCanvas.DrawPoint(waypoint.XY, Brushes.Black, 2f);
            //            DrawLineOfSight(waypoint.XY.ToDoubleVector2(), losRadius);

            foreach (var entity in EntityService.EnumerateEntities()) {
               var movementComponent = entity.MovementComponent;
               if (movementComponent != null) {
                  debugCanvas.DrawPoint(movementComponent.Position.XY.LossyToIntVector2(), Brushes.Black, movementComponent.BaseRadius);
                  debugCanvas.DrawPoint(movementComponent.Position.XY.LossyToIntVector2(), Brushes.White, movementComponent.BaseRadius - 2);

                  if (movementComponent.Swarm != null && movementComponent.WeightedSumNBodyForces.Norm2D() > GeometryOperations.kEpsilon) {
                     debugCanvas.DrawLineList(
                        new[] {
                           movementComponent.Position.XY.LossyToIntVector2(),
                           (movementComponent.Position.XY + movementComponent.WeightedSumNBodyForces.ToUnit() * movementComponent.BaseRadius).LossyToIntVector2()
                        }, Pens.Gray);
                  }

                  var unitLineOfSight = terrainSnapshot.ComputeLineOfSight(movementComponent.Position.XY, movementComponent.BaseRadius);
                  DrawLineOfSight(debugCanvas, unitLineOfSight);

                  if (movementComponent.DebugLines != null) {
                     foreach (var l in movementComponent.DebugLines) {
                        debugCanvas.DrawLineList(new[] { l.Item1.LossyToIntVector2(), l.Item2.LossyToIntVector2() }, Pens.Black);
                     }
                  }
               }
            }
         });

      }

      private static void DrawTriangulationQuadTree(DebugCanvas debugCanvas, Triangulation triangulation) {
         foreach (var island in triangulation.Islands) {
            var s = new Stack<Tuple<int, QuadTree<int>.Node>>();
            s.Push(Tuple.Create(0, island.TriangleIndexQuadTree.Root));
            while (s.Any()) {
               var tuple = s.Pop();
               var depth = tuple.Item1;
               var node = tuple.Item2;
               debugCanvas.DrawRectangle(node.Rect);
               if (node.TopLeft != null) {
                  s.Push(Tuple.Create(depth + 1, node.TopLeft));
                  s.Push(Tuple.Create(depth + 1, node.TopRight));
                  s.Push(Tuple.Create(depth + 1, node.BottomLeft));
                  s.Push(Tuple.Create(depth + 1, node.BottomRight));
               }
            }
         }
      }

      private static void DrawWallPushGrid(DebugCanvas debugCanvas, TerrainSnapshot terrainSnapshot, double holeDilationRadius) {
         for (int x = -50; x < 1100; x += 100) {
            for (int y = -50; y < 1100; y += 100) {
               var query = new DoubleVector3(x, y, 0);
               DoubleVector3 nearestLandPoint;
               var isInHole = TerrainSnapshotQueryOperations.FindNearestLandPointAndIsInHole(terrainSnapshot, holeDilationRadius, query, out nearestLandPoint);
               GeometryDebugDisplay.DrawPoint(debugCanvas, query.XY.LossyToIntVector2(), isInHole ? Brushes.Red : Brushes.Gray, 3.0f);
               if (isInHole) {
                  GeometryDebugDisplay.DrawLineStrip(debugCanvas, new[] { query.XY.LossyToIntVector2(), nearestLandPoint.XY.LossyToIntVector2() },
                     Pens.Red);
               }
            }
         }
      }

      private static void DrawLineOfSight(DebugCanvas debugCanvas, AngularVisibleSegmentStore avss) {
         var oxy = avss.Origin;
         foreach (var range in avss.Get().Where(range => range.Id != AngularVisibleSegmentStore.RANGE_ID_NULL)) {
            var rstart = DoubleVector2.FromRadiusAngle(100, range.ThetaStart);
            var rend = DoubleVector2.FromRadiusAngle(100, range.ThetaEnd);

            var s = range.Segment;
            var s1 = s.First.XY.ToDoubleVector2();
            var s2 = s.Second.XY.ToDoubleVector2();
            DoubleVector2 visibleStart, visibleEnd;
            if (!GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rstart, s1, s2, out visibleStart) ||
                !GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rend, s1, s2, out visibleEnd)) {
               // wtf?
               continue;
            }

            using (var b = new SolidBrush(Color.FromArgb(120, Color.Yellow)))
               debugCanvas.FillPolygon(
                  new Polygon(new List<IntVector3> {
                     new IntVector3((long)oxy.X, (long)oxy.Y, 0),
                     new IntVector3((long)visibleStart.X, (long)visibleStart.Y, 0),
                     new IntVector3((long)visibleEnd.X, (long)visibleEnd.Y, 0)
                  }, false), b);

            using (var dash = new Pen(Color.FromArgb(30, Color.Black), 1f) { DashPattern = new[] { 10.0f, 10.0f } })
            using (var thick = new Pen(Color.Black, 5f)) {
               debugCanvas.DrawLineStrip(
                  new[] {
                     oxy.LossyToIntVector2(),
                     visibleStart.LossyToIntVector2()
                  }, dash);
               debugCanvas.DrawLineStrip(
                  new[] {
                     oxy.LossyToIntVector2(),
                     visibleEnd.LossyToIntVector2()
                  }, dash);
               debugCanvas.DrawLineStrip(
                  new[] {
                     visibleStart.LossyToIntVector2(),
                     visibleEnd.LossyToIntVector2()
                  }, thick);
            }
         }
      }

      public static void AttachTo(Game game) {
         var debugMultiCanvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(game.MapConfiguration.Size, new Point(100, 100));
         var debugger = new GameDebugger(game, debugMultiCanvasHost);
         game.Debuggers.Add(debugger);
      }
   }
}
