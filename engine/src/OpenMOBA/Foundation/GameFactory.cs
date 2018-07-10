using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation {
   public class GameFactory {
      public event EventHandler<Game> GameCreated;

      public Game Create() {
         var gameTimeService = new GameTimeManager(30);
         var gameLoop = new GameEventQueueManager(gameTimeService);
         var terrainServiceStore = new SectorGraphDescriptionStore();
         var terrainSnapshotBuilder = new TerrainSnapshotCompiler(terrainServiceStore);
         var terrainService = new TerrainFacade(terrainServiceStore, terrainSnapshotBuilder);
         var entityService = new EntityWorld();
         var statsCalculator = new StatsCalculator();
         var pathfinderCalculator = new PathfinderCalculator(terrainService, statsCalculator);

         var movementSystemService = new MovementSystem(entityService, gameTimeService, statsCalculator, terrainService, pathfinderCalculator);
         entityService.AddEntitySystem(movementSystemService);

         var gameLogicFacade = new GameLogicFacade(terrainService, movementSystemService);
         var gameInstance = new Game {
            GameTimeManager = gameTimeService,
            GameEventQueueManager = gameLoop,
            TerrainFacade = terrainService,
            EntityWorld = entityService,
            PathfinderCalculator = pathfinderCalculator,
            MovementSystem = movementSystemService,
            GameLogicFacade = gameLogicFacade
         };
         gameInstance.Initialize();
         GameCreated?.Invoke(this, gameInstance);
         return gameInstance;
      }

      //      private static MapConfiguration CreateDefaultMapConfiguration() {
      //         var holes = new[] {
      //            Polygon.CreateRectXY(100, 100, 300, 300, 0),
      //            Polygon.CreateRectXY(400, 200, 100, 100, 0),
      //            Polygon.CreateRectXY(200, -50, 100, 150, 0),
      //            Polygon.CreateRectXY(600, 600, 300, 300, 0),
      //            Polygon.CreateRectXY(700, 500, 100, 100, 0),
      //            Polygon.CreateRectXY(200, 700, 100, 100, 0),
      //            Polygon.CreateRectXY(600, 100, 300, 50, 0),
      //            Polygon.CreateRectXY(600, 150, 50, 200, 0),
      //            Polygon.CreateRectXY(850, 150, 50, 200, 0),
      //            Polygon.CreateRectXY(600, 350, 300, 50, 0),
      //            Polygon.CreateRectXY(700, 200, 100, 100, 0)
      //         };
      //
      ////         var holeSquiggle = PolylineOperations.ExtrudePolygon(
      ////            new[] {
      ////               new IntVector2(100, 50),
      ////               new IntVector2(100, 100),
      ////               new IntVector2(200, 100),
      ////               new IntVector2(200, 150),
      ////               new IntVector2(200, 200),
      ////               new IntVector2(400, 250),
      ////               new IntVector2(200, 300),
      ////               new IntVector2(400, 315),
      ////               new IntVector2(200, 330),
      ////               new IntVector2(210, 340),
      ////               new IntVector2(220, 350),
      ////               new IntVector2(220, 400),
      ////               new IntVector2(221, 400)
      ////            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygons();
      //
      //         return new MapConfiguration {
      //            Size = new Size(1000, 1000),
      //            StaticHolePolygons = holes.ToList()
      //         };
      //      }
   }
}
