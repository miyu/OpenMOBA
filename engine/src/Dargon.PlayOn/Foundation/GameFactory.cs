using System;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.ECS.Utils;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;

namespace Dargon.PlayOn.Foundation {
   public class GameFactory {
      public event EventHandler<Game> GameCreated;

      public Game Create() {
         var gameTimeManager = new GameTimeManager(60);
         var gameEventQueueManager = new GameEventQueueManager(gameTimeManager);
         var sectorGraphDescriptionStore = new SectorGraphDescriptionStore();

         // Entity and Statistics
         var entityWorld = new EntityWorld();
         var entityGridFacade = new EntityGridFacade(new EntityGridRangeCalculator());
         var statisticsCalculator = new StatisticsCalculator();

         // Terrain
         var terrainSnapshotCompiler = new TerrainSnapshotCompiler(sectorGraphDescriptionStore);
         var terrainFacade = new TerrainFacade(sectorGraphDescriptionStore, terrainSnapshotCompiler);

         // Motion
         var motionStateContainer = new AssociatedStateContainer<object>();
         var pathfinderCalculator = new PathfinderCalculator(terrainFacade, statisticsCalculator);
         var triangulationWalker = new TriangulationWalker();
         var flockingSimulator = new FlockingSimulator(entityGridFacade, statisticsCalculator, pathfinderCalculator, terrainFacade, triangulationWalker);
         var motionSystem = new MotionSystem(entityWorld, gameTimeManager, flockingSimulator, motionStateContainer);
         var motionOperations = new MotionOperations(terrainFacade, pathfinderCalculator, statisticsCalculator);
         var motionFacade = new MotionFacade(terrainFacade, pathfinderCalculator, statisticsCalculator, motionOperations);
         entityWorld.AddEntitySystem(motionSystem);
         entityWorld.AddStepHandler(StepHandlerPriority.Flocking, motionSystem.ExecuteFlocking);

         var gameLogicFacade = new GameLogicFacade(terrainFacade, motionFacade);
         var gameInstance = new Game {
            GameTimeManager = gameTimeManager,
            GameEventQueueManager = gameEventQueueManager,
            TerrainFacade = terrainFacade,
            EntityWorld = entityWorld,
            PathfinderCalculator = pathfinderCalculator,
            MotionSystem = motionSystem,
            MotionFacade = motionFacade,
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