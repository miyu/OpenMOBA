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
      private static readonly StrokeStyle PathStroke = new StrokeStyle(Color.Lime, 2.0);
      private static readonly StrokeStyle HighlightStroke = new StrokeStyle(Color.Red, 3.0);

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
         debugCanvas.BatchDraw(() => {
            debugCanvas.DrawPolyTree(terrainSnapshot.ComputePunchedLand(0));
            debugCanvas.DrawPolyTree(terrainSnapshot.ComputePunchedLand(holeDilationRadius));
            debugCanvas.DrawPolygons(temporaryHolePolygons, new StrokeStyle(Color.Red));
            debugCanvas.DrawTriangulation(terrainSnapshot.ComputeTriangulation(holeDilationRadius), new StrokeStyle(Color.DarkGray));
            debugCanvas.DrawTriangulationQuadTree(terrainSnapshot.ComputeTriangulation(holeDilationRadius));
            debugCanvas.DrawVisibilityGraph(visibilityGraph);
            debugCanvas.DrawWallPushGrid(terrainSnapshot, holeDilationRadius);

            DrawTestPathfindingQueries(debugCanvas, holeDilationRadius);
            DrawHighlightedEntityTriangles(terrainSnapshot, debugCanvas);
            DrawEntities(debugCanvas, terrainSnapshot);
            DrawEntityPaths(debugCanvas);
         });
      }

      private void DrawEntityPaths(DebugCanvas debugCanvas) {
         foreach (var entity in EntityService.EnumerateEntities()) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent == null) continue;
            var pathPoints = new[] { movementComponent.Position }.Concat(entity.MovementComponent.PathingBreadcrumbs).ToList();
            debugCanvas.DrawLineStrip(pathPoints, PathStroke);
         }
      }

      private void DrawEntities(DebugCanvas debugCanvas, TerrainSnapshot terrainSnapshot) {
         foreach (var entity in EntityService.EnumerateEntities()) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent != null) {
               debugCanvas.DrawPoint(movementComponent.Position, new StrokeStyle(Color.Black, 2 * movementComponent.BaseRadius));
               debugCanvas.DrawPoint(movementComponent.Position, new StrokeStyle(Color.White, 2 * movementComponent.BaseRadius - 2));

               if (movementComponent.Swarm != null && movementComponent.WeightedSumNBodyForces.Norm2D() > GeometryOperations.kEpsilon) {
                  var direction = movementComponent.WeightedSumNBodyForces.ToUnit() * movementComponent.BaseRadius;
                  var to = movementComponent.Position + new DoubleVector3(direction.X, direction.Y, 0.0);
                  debugCanvas.DrawLine(movementComponent.Position, to, new StrokeStyle(Color.Gray));
               }

               var unitLineOfSight = terrainSnapshot.ComputeLineOfSight(movementComponent.Position.XY, movementComponent.BaseRadius);
               debugCanvas.DrawLineOfSight(unitLineOfSight);

               if (movementComponent.DebugLines != null) {
                  debugCanvas.DrawLineList(
                     movementComponent.DebugLines.SelectMany(pair => new[] { pair.Item1, pair.Item2 }).ToList(),
                     new StrokeStyle(Color.Black));
               }
            }
         }
      }

      private void DrawHighlightedEntityTriangles(TerrainSnapshot terrainSnapshot, DebugCanvas debugCanvas) {
         foreach (var entity in EntityService.EnumerateEntities()) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent != null) {
               var triangulation = terrainSnapshot.ComputeTriangulation(movementComponent.BaseRadius);
               TriangulationIsland island;
               int triangleIndex;
               if (triangulation.TryIntersect(movementComponent.Position.X, movementComponent.Position.Y, out island, out triangleIndex)) {
                  debugCanvas.DrawTriangle(island.Triangles[triangleIndex], HighlightStroke);
               }
            }
         }
      }

      private void DrawTestPathfindingQueries(DebugCanvas debugCanvas, double holeDilationRadius) {
         var testPathFindingQueries = new[] {
            Tuple.Create(new DoubleVector3(60, 40, 0), new DoubleVector3(930, 300, 0)),
            Tuple.Create(new DoubleVector3(675, 175, 0), new DoubleVector3(825, 300, 0)),
            Tuple.Create(new DoubleVector3(50, 900, 0), new DoubleVector3(950, 475, 0)),
            Tuple.Create(new DoubleVector3(50, 500, 0), new DoubleVector3(80, 720, 0))
         };

         foreach (var query in testPathFindingQueries) {
            List<DoubleVector3> pathPoints;
            if (Game.PathfinderCalculator.TryFindPath(holeDilationRadius, query.Item1, query.Item2, out pathPoints)) {
               debugCanvas.DrawLineStrip(pathPoints, PathStroke);
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
