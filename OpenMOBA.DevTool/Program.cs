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
using Shade;

namespace OpenMOBA.DevTool {
   public static class Program {
      public static void Main(string[] args) {
//         CanvasProgram.EntryPoint(args);
//         return;
         var gameFactory = new GameFactory();
         gameFactory.GameCreated += (s, game) => {
            GameDebugger.AttachToWithSoftwareRendering(game);
         };
         OpenMOBA.Program.Main(gameFactory);
      }
   }

   public class GameDebugger : IGameDebugger {
      private static readonly StrokeStyle PathStroke = new StrokeStyle(Color.Lime, 2.0);
      private static readonly StrokeStyle HighlightStroke = new StrokeStyle(Color.Red, 3.0);

      public GameDebugger(Game game, IDebugMultiCanvasHost debugMultiCanvasHost) {
         Game = game;
         DebugMultiCanvasHost = debugMultiCanvasHost;
      }

      public Game Game { get; }
      public IDebugMultiCanvasHost DebugMultiCanvasHost { get; }

      private DebugProfiler DebugProfiler => Game.DebugProfiler;
      private GameTimeService GameTimeService => Game.GameTimeService;
      private TerrainService TerrainService => Game.TerrainService;
      private EntityService EntityService => Game.EntityService;

      public void HandleFrameEnd(FrameEndStatistics frameStatistics) {
         if (GameTimeService.Ticks == 0) {
//            AddSquiggleHole();
         }
         if (frameStatistics.EventsProcessed != 0 || GameTimeService.Ticks % 64 == 0) {
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

         debugCanvas.BatchDraw(() => {
            for (var i = 0; i < terrainSnapshot.SectorSnapshots.Count; i++) {
               //if (i != 3) continue;
               var sectorSnapshot = terrainSnapshot.SectorSnapshots[i];
               var visibilityGraph = sectorSnapshot.ComputeVisibilityGraph(holeDilationRadius);
               //            debugCanvas.DrawLine(new DoubleVector3(490, 490, 0), new DoubleVector3(510, 510, 0), new StrokeStyle(Color.Black) { DisableStrokePerspective = true });
               //            return;
//               debugCanvas.DrawPolyTree(sectorSnapshot.ComputePunchedLand(0));
               //debugCanvas.DrawPolyTree(sectorSnapshot.ComputePunchedLand(holeDilationRadius));
//               debugCanvas.DrawPolygons(temporaryHolePolygons, new StrokeStyle(Color.Red));
//               debugCanvas.DrawTriangulation(sectorSnapshot.ComputeTriangulation(holeDilationRadius), new StrokeStyle(Color.DarkGray));
               //            debugCanvas.DrawTriangulationQuadTree(terrainSnapshot.ComputeTriangulation(holeDilationRadius));
               debugCanvas.DrawVisibilityGraph(visibilityGraph);
               //            debugCanvas.DrawWallPushGrid(terrainSnapshot, holeDilationRadius);

               //            DrawTestPathfindingQueries(debugCanvas, holeDilationRadius);
               //            DrawHighlightedEntityTriangles(terrainSnapshot, debugCanvas);
               //               DrawEntityPaths(debugCanvas);
            }
            //            DrawTestPathfindingQueries(debugCanvas, holeDilationRadius);
            DrawEntities(debugCanvas);

            foreach (var sector in terrainSnapshot.SectorSnapshots) {
               foreach (var waypoint in sector.ComputeVisibilityGraph(holeDilationRadius).Waypoints) {
                  var los = sector.ComputeLineOfSight(waypoint.XY.ToDoubleVector2(), holeDilationRadius);
//                  debugCanvas.DrawLineOfSight(los);
               }
            }

            foreach (var crossoverSnapshot in terrainSnapshot.CrossoverSnapshots) {
               debugCanvas.DrawLine(
                  crossoverSnapshot.Segment.First.ToDoubleVector3(),
                  crossoverSnapshot.Segment.Second.ToDoubleVector3(),
                  new StrokeStyle(Color.Gray, 3));
            }
         });
      }

      private void DrawEntityPaths(IDebugCanvas debugCanvas) {
         foreach (var entity in EntityService.EnumerateEntities()) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent == null) continue;
            var pathPoints = new[] { movementComponent.Position }.Concat(entity.MovementComponent.PathingBreadcrumbs).ToList();
            debugCanvas.DrawLineStrip(pathPoints, PathStroke);
         }
      }

      private void DrawEntities(IDebugCanvas debugCanvas) {
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

               if (movementComponent.DebugLines != null) {
                  debugCanvas.DrawLineList(
                     movementComponent.DebugLines.SelectMany(pair => new[] { pair.Item1, pair.Item2 }).ToList(),
                     new StrokeStyle(Color.Black));
               }
            }
         }
      }

      private void DrawHighlightedEntityTriangles(SectorSnapshot sectorSnapshot, DebugCanvas debugCanvas) {
         foreach (var entity in EntityService.EnumerateEntities()) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent != null) {
               var triangulation = sectorSnapshot.ComputeTriangulation(movementComponent.BaseRadius);
               TriangulationIsland island;
               int triangleIndex;
               if (triangulation.TryIntersect(movementComponent.Position.X, movementComponent.Position.Y, out island, out triangleIndex)) {
                  debugCanvas.DrawTriangle(island.Triangles[triangleIndex], HighlightStroke);
               }
            }
         }
      }

      private void DrawTestPathfindingQueries(IDebugCanvas debugCanvas, double holeDilationRadius) {
         var testPathFindingQueries = new[] {
            Tuple.Create(new DoubleVector3(60, 40, 0), new DoubleVector3(930, 300, 0)),
            Tuple.Create(new DoubleVector3(675, 175, 0), new DoubleVector3(825, 300, 0)),
            Tuple.Create(new DoubleVector3(50, 900, 0), new DoubleVector3(950, 475, 0)),
            Tuple.Create(new DoubleVector3(50, 500, 0), new DoubleVector3(80, 720, 0))
         };

         // scale 90%, above points are for [0,0] to [1000, 1000] but demo is now [0,0] to [900,900].
         for (var i = 0; i < testPathFindingQueries.Length; i++) {
            testPathFindingQueries[i] = new Tuple<DoubleVector3, DoubleVector3>(
               new DoubleVector3(
                  testPathFindingQueries[i].Item1.X * 0.9,
                  testPathFindingQueries[i].Item1.Y * 0.9,
                  testPathFindingQueries[i].Item1.Z * 0.9),
               new DoubleVector3(
                  testPathFindingQueries[i].Item2.X * 0.9,
                  testPathFindingQueries[i].Item2.Y * 0.9,
                  testPathFindingQueries[i].Item2.Z * 0.9)
            );
         }

         foreach (var query in testPathFindingQueries) {
            List<DoubleVector3> pathPoints;
            if (Game.PathfinderCalculator.TryFindPath(holeDilationRadius, query.Item1, query.Item2, out pathPoints)) {
               debugCanvas.DrawLineStrip(pathPoints, PathStroke);
            }
         }
      }

      public static void AttachToWithSoftwareRendering(Game game) {
         var rotation = 80 * Math.PI / 180.0;
         float scale = 1.0f;
         var displaySize = new Size((int)(1400 * scale), (int)(700 * scale));
         var projector = new PerspectiveProjector(
            new DoubleVector3(1050, 500, 0) + DoubleVector3.FromRadiusAngleAroundXAxis(600, rotation),
            new DoubleVector3(1050, 500, 0),
            DoubleVector3.FromRadiusAngleAroundXAxis(1, rotation + Math.PI / 2),
            displaySize.Width,
            displaySize.Height);
         //         projector = null;
         //         var debugMultiCanvasHost = new MonoGameCanvasHost();
         var debugMultiCanvasHost = Debugging.DebugMultiCanvasHost.CreateAndShowCanvas(
            displaySize,
            new Point(100, 100),
            projector);
         AttachTo(game, debugMultiCanvasHost);
      }

      public static void AttachTo(Game game, IDebugMultiCanvasHost debugMultiCanvasHost) {
         var debugger = new GameDebugger(game, debugMultiCanvasHost);
         game.Debuggers.Add(debugger);
      }
   }
}
