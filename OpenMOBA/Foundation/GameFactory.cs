using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using OpenMOBA.Utilities;

namespace OpenMOBA.Foundation {
   public interface IGameEventFactory {
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
   }

   public class Swarm {
      public List<Entity> Entities { get; set; } = new List<Entity>();
      public DoubleVector3 Destination { get; set; }
   }

   public class Game : IGameEventFactory {
      public DebugProfiler DebugProfiler { get; } = new DebugProfiler();
      public List<IGameDebugger> Debuggers { get; set; } = new List<IGameDebugger>();
      public GameTimeService GameTimeService { get; set; }
      public GameEventQueueService GameEventQueueService { get; set; }
      public MapConfiguration MapConfiguration { get; set; }
      public TerrainService TerrainService { get; set; }
      public EntityService EntityService { get; set; }
      public PathfinderCalculator PathfinderCalculator { get; set; }
      public MovementSystemService MovementSystemService { get; set; }
      public GameLogicFacade GameLogicFacade { get; set; }

      public void Run() {
         var r = new Random(1);
         for (int i = 0; i < 30; i++) {
            var poly = Polygon.CreateRectXY(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200), 0);
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new TerrainHole { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

//         var a = CreateTestEntity(new DoubleVector2(60, 40), 15, 80);
//         var b = CreateTestEntity(new DoubleVector2(675, 175), 15, 70);
//         var c = CreateTestEntity(new DoubleVector2(50, 900), 15, 60);
//         var d = CreateTestEntity(new DoubleVector2(50, 500), 15, 50);
//
//         MovementSystemService.Pathfind(a, new DoubleVector2(930, 300));
//         MovementSystemService.Pathfind(b, new DoubleVector2(825, 300));
//         MovementSystemService.Pathfind(c, new DoubleVector2(950, 475));
//         MovementSystemService.Pathfind(d, new DoubleVector2(80, 720));

         var benchmarkDestination = new DoubleVector3(950, 50, 0.0);
         var benchmarkUnitBaseSpeed = 50.0f;
         var swarm = new Swarm { Destination = benchmarkDestination };
         var swarmMeanRadius = 10.0f;
         for (var y = 0; y < 10; y++) {
            for (var x = 0; x < 10; x++) {
               // var swarmlingRadius = 10f;
               var swarmlingRadius = (float)Math.Round(5.0f + 10.0f * (float)r.NextDouble());
               var p = new DoubleVector3(50, 500, 0);
               var offset = new DoubleVector3(x * swarmMeanRadius * 2, y * swarmMeanRadius * 2, 0);
//               var swarmling = CreateTestEntity(p + offset, swarmlingRadius, benchmarkUnitBaseSpeed - 20 + 40 * (float)r.NextDouble());
//               swarmling.MovementComponent.Swarm = swarm;
//               swarm.Entities.Add(swarmling);
            }
         }

         var optimal = CreateTestEntity(new DoubleVector3(50 + 9 * 10*2, 500, 0.0), 10, benchmarkUnitBaseSpeed);
         MovementSystemService.Pathfind(optimal, benchmarkDestination);

         IntMath.Sqrt(0); // init static

         var sw = new Stopwatch();
         sw.Start();
         while (true) {
            DebugProfiler.EnterTick(GameTimeService.Ticks);

            int eventsProcessed;
            GameEventQueueService.ProcessPendingGameEvents(out eventsProcessed);
            EntityService.ProcessSystems();

            DebugProfiler.LeaveTick();

            foreach (var debugger in Debuggers) {
               debugger.HandleFrameEnd(new FrameEndStatistics {
                  EventsProcessed = eventsProcessed
               });
            }

            GameTimeService.IncrementTicks();
//            Console.WriteLine("At " + GameTimeService.Ticks + " " + TerrainService.BuildSnapshot().TemporaryHoles.Count);
            //            if (GameTimeService.Ticks > 80) return;
            if (GameTimeService.Ticks > GameTimeService.TicksPerSecond * 20) {
               Console.WriteLine($"Done! {sw.Elapsed.TotalSeconds}");
               break;
            }
         }

         var latch = new CountdownEvent(1);
         new Thread(() => {
            DebugProfiler.DumpToClipboard();
            latch.Signal();
         }) { ApartmentState = ApartmentState.STA }.Start();
         latch.Wait();
      }

      private Entity CreateTestEntity(DoubleVector3 initialPosition, float radius, float movementSpeed) {
         var entity = EntityService.CreateEntity();
         EntityService.AddEntityComponent(entity, new MovementComponent {
            Position = initialPosition,
            BaseRadius = radius,
            BaseSpeed = movementSpeed
         });
         return entity;
      }

      public GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole terrainHole) {
         return new AddTemporaryHoleGameEvent(time, GameLogicFacade, terrainHole);
      }

      public GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole terrainHole) {
         return new RemoveTemporaryHoleGameEvent(time, GameLogicFacade, terrainHole);
      }
   }

   public struct FrameEndStatistics {
      public int EventsProcessed;
   }

   public interface IGameDebugger {
      void HandleFrameEnd(FrameEndStatistics frameStatistics);
   }

   public class GameLogicFacade {
      private readonly TerrainService terrainService;
      private readonly MovementSystemService movementSystemService;

      public GameLogicFacade(TerrainService terrainService, MovementSystemService movementSystemService) {
         this.terrainService = terrainService;
         this.movementSystemService = movementSystemService;
      }

      public void AddTemporaryHole(TerrainHole hole) {
         terrainService.AddTemporaryHole(hole);
         // todo: can optimize to only invalidate paths intersecting hole.
         movementSystemService.HandleHoleAdded(hole);
      }

      public void RemoveTemporaryHole(TerrainHole hole) {
         terrainService.RemoveTemporaryHole(hole);
         movementSystemService.InvalidatePaths();
      }
   }

   public class GameFactory {
      public event EventHandler<Game> GameCreated; 

      public Game Create() {
         var gameTimeService = new GameTimeService(30);
         var gameLoop = new GameEventQueueService(gameTimeService);
         var mapConfiguration = CreateDefaultMapConfiguration();
         var terrainService = new TerrainService(mapConfiguration, gameTimeService);
         var entityService = new EntityService();
         var statsCalculator = new StatsCalculator();
         var pathfinderCalculator = new PathfinderCalculator(terrainService, statsCalculator);
         var movementSystemService = new MovementSystemService(entityService, gameTimeService, statsCalculator, terrainService, pathfinderCalculator);
         entityService.AddEntitySystem(movementSystemService);
         var gameLogicFacade = new GameLogicFacade(terrainService, movementSystemService);
         var gameInstance = new Game {
            GameTimeService = gameTimeService,
            GameEventQueueService = gameLoop,
            MapConfiguration = mapConfiguration,
            TerrainService = terrainService,
            EntityService = entityService,
            PathfinderCalculator = pathfinderCalculator,
            MovementSystemService = movementSystemService,
            GameLogicFacade = gameLogicFacade
         };
         GameCreated?.Invoke(this, gameInstance);
         return gameInstance;
      }

      private static MapConfiguration CreateDefaultMapConfiguration() {
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
