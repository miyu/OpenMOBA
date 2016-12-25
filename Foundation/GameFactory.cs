using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenMOBA.Foundation {
   public interface IGameEventFactory {
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
   }

   public class GameInstance : IGameEventFactory {
      public GameTimeService GameTimeService { get; set; }
      public GameEventQueueService GameEventQueueService { get; set; }
      public MapConfiguration MapConfiguration { get; set; }
      public TerrainService TerrainService { get; set; }
      public void Run() {
         var r = new Random();
         for (int i = 0; i < 10; i++) {
            var poly = Polygon.CreateRect(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200));
            var startTicks = r.Next(0, 100);
            var endTicks = r.Next(startTicks + 1, 120);
            var terrainHole = new TerrainHole { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

         var debugMultiCanvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(MapConfiguration.Size, new Point(100, 100));
         while (true) {
            GameEventQueueService.ProcessPendingGameEvents();
            HandleFrameEnd(debugMultiCanvasHost);

            GameTimeService.IncrementTicks();
            if (GameTimeService.Ticks > 120) return;
         }
      }

      private void HandleFrameEnd(DebugMultiCanvasHost debugMultiCanvasHost) {
         var terrainSnapshot = TerrainService.BuildSnapshot();
         var temporaryHolePolygons = terrainSnapshot.TemporaryHoles.SelectMany(th => th.Polygons).ToList();
         var visibilityGraph = VisibilityGraphOperations.CreateVisibilityGraph(
            MapConfiguration.Size,
            PolygonOperations.Offset()
                             .Include(temporaryHolePolygons)
                             .Dilate(15)
                             .Execute()
                             .FlattenToPolygons()
         );
         var debugCanvas = debugMultiCanvasHost.CreateAndAddCanvas(GameTimeService.Ticks);
         debugCanvas.DrawPolygons(temporaryHolePolygons, Color.Red);
         debugCanvas.DrawVisibilityGraph(visibilityGraph);
         var testPathFindingQueries = new[] {
            Tuple.Create(new IntVector2(60, 40), new IntVector2(930, 300)),
            Tuple.Create(new IntVector2(675, 175), new IntVector2(825, 300)),
            Tuple.Create(new IntVector2(50, 900), new IntVector2(950, 475)),
            Tuple.Create(new IntVector2(50, 500), new IntVector2(80, 720))
         };

         using (var pen = new Pen(Color.Lime, 2)) {
            foreach (var query in testPathFindingQueries) {
               var path = visibilityGraph.FindPath(query.Item1, query.Item2);
               if (path != null) {
                  debugCanvas.DrawLineStrip(path.Points, pen);
               }
            }
         }
      }

      public GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole terrainHole) {
         return new AddTemporaryHoleGameEvent(time, TerrainService, terrainHole);
      }

      public GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole terrainHole) {
         return new RemoveTemporaryHoleGameEvent(time, TerrainService, terrainHole);
      }
   }

   public class GameInstanceFactory {
      public GameInstance Create() {
         var gameTimeService = new GameTimeService(30);
         var gameLoop = new GameEventQueueService(gameTimeService);
         var mapConfiguration = CreateDefaultMapConfiguration();
         var terrainService = new TerrainService(mapConfiguration, gameTimeService);
         return new GameInstance {
            GameTimeService = gameTimeService,
            GameEventQueueService = gameLoop,
            MapConfiguration = mapConfiguration,
            TerrainService = terrainService
         };
      }

      private static MapConfiguration CreateDefaultMapConfiguration() {
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

//         var holeSquiggle = PolylineOperations.ExtrudePolygon(
//            new[] {
//               new IntVector2(100, 50),
//               new IntVector2(100, 100),
//               new IntVector2(200, 100),
//               new IntVector2(200, 150),
//               new IntVector2(200, 200),
//               new IntVector2(400, 250),
//               new IntVector2(200, 300),
//               new IntVector2(400, 315),
//               new IntVector2(200, 330),
//               new IntVector2(210, 340),
//               new IntVector2(220, 350),
//               new IntVector2(220, 400),
//               new IntVector2(221, 400)
//            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygons();

         return new MapConfiguration {
            Size = new Size(1000, 1000),
            StaticHolePolygons = holes.ToList()
         };
      }
   }
}
