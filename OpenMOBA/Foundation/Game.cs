using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation {
   public static class SectorMetadataPresets {
      public static readonly TerrainStaticMetadata Blank2D = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new List<Polygon2>()
      };

      public static readonly TerrainStaticMetadata Test2D = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new[] {
            Polygon2.CreateRect(100, 100, 300, 300),
            Polygon2.CreateRect(400, 200, 100, 100),
            Polygon2.CreateRect(200, -50, 100, 150),
            Polygon2.CreateRect(600, 600, 300, 300),
            Polygon2.CreateRect(700, 500, 100, 100),
            Polygon2.CreateRect(200, 700, 100, 100),
            Polygon2.CreateRect(600, 100, 300, 50),
            Polygon2.CreateRect(600, 150, 50, 200),
            Polygon2.CreateRect(850, 150, 50, 200),
            Polygon2.CreateRect(600, 350, 300, 50),
            Polygon2.CreateRect(700, 200, 100, 100)
         }
      };

      public static readonly TerrainStaticMetadata FourSquares2D = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new[] {
            Polygon2.CreateRect(200, 200, 200, 200),
            Polygon2.CreateRect(200, 600, 200, 200),
            Polygon2.CreateRect(600, 200, 200, 200),
            Polygon2.CreateRect(600, 600, 200, 200)
         }
      };

      private const int CrossCirclePathWidth = 200;
      private const int CrossCircleInnerLandRadius = 400;
      private const int CrossCircleInnerHoleRadius = 200;
      public static readonly TerrainStaticMetadata CrossCircle = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] {
            Polygon2.CreateRect((1000 - CrossCirclePathWidth) / 2, 0, CrossCirclePathWidth, 1000),
            Polygon2.CreateRect(0, (1000 - CrossCirclePathWidth) / 2, 1000, CrossCirclePathWidth),
            Polygon2.CreateCircle(500, 500, CrossCircleInnerLandRadius)
         },
         LocalExcludedContours = new[] {
            Polygon2.CreateCircle(500, 500, CrossCircleInnerHoleRadius)
         }
      };

      public static readonly TerrainStaticMetadata HashCircle1 = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] {
            Polygon2.CreateRect(200, 0, 200, 1000),
            Polygon2.CreateRect(600, 0, 200, 1000),
            Polygon2.CreateRect(0, 200, 1000, 200),
            Polygon2.CreateRect(0, 600, 1000, 200),
            Polygon2.CreateCircle(500, 500, 105, 64),
            Polygon2.CreateRect(450, 300, 100, 400),
            Polygon2.CreateRect(300, 450, 400, 100)
         },
         LocalExcludedContours = new Polygon2[] { }
      };


      public const int HashCircle2ScalingFactor = 1;
      public static readonly TerrainStaticMetadata HashCircle2 = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000 * HashCircle2ScalingFactor, 1000 * HashCircle2ScalingFactor),
         LocalIncludedContours = new[] {
            Polygon2.CreateRect(0 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor),
            Polygon2.CreateRect(200 * HashCircle2ScalingFactor, 0 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor),
            Polygon2.CreateRect(600 * HashCircle2ScalingFactor, 0 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor),
            Polygon2.CreateRect(600 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor),

            Polygon2.CreateRect(0 * HashCircle2ScalingFactor, 600 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor),
            Polygon2.CreateRect(200 * HashCircle2ScalingFactor, 600 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor),
            Polygon2.CreateRect(600 * HashCircle2ScalingFactor, 600 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor),
            Polygon2.CreateRect(600 * HashCircle2ScalingFactor, 600 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor),

            Polygon2.CreateCircle(500 * HashCircle2ScalingFactor, 500 * HashCircle2ScalingFactor, 400 * HashCircle2ScalingFactor)
         },
         LocalExcludedContours = new [] {
            Polygon2.CreateCircle(500 * HashCircle2ScalingFactor, 500 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor)
         }
      };
   }

   public interface IGameEventFactory {
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, DynamicTerrainHole temporaryHole);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, DynamicTerrainHole temporaryHole);
   }

   public class Game : IGameEventFactory {
      public DebugProfiler DebugProfiler { get; } = new DebugProfiler();
      public List<IGameDebugger> Debuggers { get; set; } = new List<IGameDebugger>(); // really should be concurrentset
      public GameTimeService GameTimeService { get; set; }
      public GameEventQueueService GameEventQueueService { get; set; }
      public TerrainService TerrainService { get; set; }
      public EntityService EntityService { get; set; }
      public PathfinderCalculator PathfinderCalculator { get; set; }
      public MovementSystemService MovementSystemService { get; set; }
      public GameLogicFacade GameLogicFacade { get; set; }

      public void Run() {
         var sector1 = TerrainService.CreateSector(SectorMetadataPresets.Test2D);
         TerrainService.AddSector(sector1);

         var sector2 = TerrainService.CreateSector(SectorMetadataPresets.FourSquares2D);
         sector2.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateRotationY(-0.0f), Matrix4x4.CreateTranslation(1000, 0, 0));
         TerrainService.AddSector(sector2);

         var sector3 = TerrainService.CreateSector(SectorMetadataPresets.HashCircle2);
         sector3.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(-1000, 0, 0));
         sector3.EnableDebugHighlight = true;
         TerrainService.AddSector(sector3);

         TerrainService.HackAddSectorCrossover(new Crossover {
            A = sector1,
            B = sector2,
            Segment = new IntLineSegment3(new IntVector3(1000, 200, 0), new IntVector3(1000, 400, 0)),
            AToBTransformation = Matrix3x2.CreateTranslation(-1000, 0),
            BToATransformation = Matrix3x2.CreateTranslation(1000, 0)
         });

         TerrainService.HackAddSectorCrossover(new Crossover {
            A = sector1,
            B = sector2,
            Segment = new IntLineSegment3(new IntVector3(1000, 600, 0), new IntVector3(1000, 800, 0)),
            AToBTransformation = Matrix3x2.CreateTranslation(-1000, 0),
            BToATransformation = Matrix3x2.CreateTranslation(1000, 0)
         });

         TerrainService.HackAddSectorCrossover(new Crossover {
            A = sector1,
            B = sector3,
            Segment = new IntLineSegment3(new IntVector3(0, 200, 0), new IntVector3(0, 400, 0)),
            AToBTransformation = Matrix3x2.CreateTranslation(1000, 0),
            BToATransformation = Matrix3x2.CreateTranslation(-1000, 0)
         });

         TerrainService.HackAddSectorCrossover(new Crossover {
            A = sector1,
            B = sector3,
            Segment = new IntLineSegment3(new IntVector3(0, 600, 0), new IntVector3(0, 800, 0)),
            AToBTransformation = Matrix3x2.CreateTranslation(1000, 0),
            BToATransformation = Matrix3x2.CreateTranslation(-1000, 0)
         });

         var r = new Random(1);
         for (int i = 0; i < 30; i++) {
            var poly = Polygon2.CreateRect(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200));
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new DynamicTerrainHole { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }
         
         r.NextBytes(new byte[1337]);

         for (int i = 0; i < 20; i++) {
            var w = r.Next(50, 100);
            var h = r.Next(50, 100);
            var poly = Polygon2.CreateRect(r.Next(800 + 80, 1100 - 80 - w) * 10 / 9, r.Next(520 - 40, 720 + 40 - h) * 10 / 9, w * 10 / 9, h * 10 / 9);
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new DynamicTerrainHole { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

         for (int i = 0; i < 20; i++) {
            var w = r.Next(50, 100);
            var h = r.Next(50, 100);
            var poly = Polygon2.CreateRect(r.Next(800 + 80, 1100 - 80 - w) * 10 / 9, r.Next(180 - 40, 360 + 40 - h) * 10 / 9, w * 10 / 9, h * 10 / 9);
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new DynamicTerrainHole { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

         var a = CreateTestEntity(new DoubleVector3(60, 40, 0), 15, 80);
         var b = CreateTestEntity(new DoubleVector3(675, 175, 0), 15, 70);
         var c = CreateTestEntity(new DoubleVector3(50, 900, 0), 15, 60);
         var d = CreateTestEntity(new DoubleVector3(50, 500, 0), 15, 50);

//         MovementSystemService.Pathfind(a, new DoubleVector3(930, 300, 0));
//         MovementSystemService.Pathfind(b, new DoubleVector3(825, 300, 0));
//         MovementSystemService.Pathfind(c, new DoubleVector3(950, 475, 0));
//         MovementSystemService.Pathfind(d, new DoubleVector3(80, 720, 0));

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

//         var optimal = CreateTestEntity(new DoubleVector3(50 + 9 * 10*2, 500, 0.0), 10, benchmarkUnitBaseSpeed);
//         MovementSystemService.Pathfind(optimal, benchmarkDestination);

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

            List<DoubleVector3> path;
            PathfinderCalculator.TryFindPath(15, new DoubleVector3(-600, 700, 0), new DoubleVector3(1500, 500, 0), out path);

            GameTimeService.IncrementTicks();
//            Console.WriteLine("At " + GameTimeService.Ticks + " " + TerrainService.BuildSnapshot().TemporaryHoles.Count);
            //            if (GameTimeService.Ticks > 80) return;
            if (GameTimeService.Ticks >= GameTimeService.TicksPerSecond * 1) {
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

      public GameEvent CreateAddTemporaryHoleEvent(GameTime time, DynamicTerrainHole dynamicTerrainHole) {
         return new AddTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHole);
      }

      public GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, DynamicTerrainHole dynamicTerrainHole) {
         return new RemoveTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHole);
      }
   }

   public class Swarm {
      public List<Entity> Entities { get; set; } = new List<Entity>();
      public DoubleVector3 Destination { get; set; }
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

      public void AddTemporaryHole(DynamicTerrainHole hole) {
         terrainService.AddTemporaryHole(hole);
         // todo: can optimize to only invalidate paths intersecting hole.
         movementSystemService.HandleHoleAdded(hole);
      }

      public void RemoveTemporaryHole(DynamicTerrainHole hole) {
         terrainService.RemoveTemporaryHole(hole);
         movementSystemService.InvalidatePaths();
      }
   }
}