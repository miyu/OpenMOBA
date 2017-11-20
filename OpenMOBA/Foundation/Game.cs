using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
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
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription temporaryHoleDescription);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription temporaryHoleDescription);
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
//         var sector1 = TerrainService.CreateSectorNodeDescription(SectorMetadataPresets.HashCircle2);
//         sector1.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(-1000, 0, 0));
//         TerrainService.AddSectorNodeDescription(sector1);
//
//         var sector2 = TerrainService.CreateSectorNodeDescription(SectorMetadataPresets.Test2D);
//         sector2.EnableDebugHighlight = true;
//         TerrainService.AddSectorNodeDescription(sector2);
//
//         var sector3 = TerrainService.CreateSectorNodeDescription(SectorMetadataPresets.FourSquares2D);
//         sector3.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateRotationY(-0.0f), Matrix4x4.CreateTranslation(1000, 0, 0));
//         TerrainService.AddSectorNodeDescription(sector3);
//
//         var left1 = new IntLineSegment2(new IntVector2(0, 200), new IntVector2(0, 400));
//         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
//         var right1 = new IntLineSegment2(new IntVector2(1000, 200), new IntVector2(1000, 400));
//         var right2 = new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800));
//
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector1, sector2, right1, left1));
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector1, sector2, right2, left2));
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector2, sector1, left1, right1));
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector2, sector1, left2, right2));
//
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector2, sector3, right1, left1));
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector2, sector3, right2, left2));
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector3, sector2, left1, right1));
//         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector3, sector2, left2, right2));

         var sectorSpanWidth = 5;
         var sectorSpanHeight = 5;
         var sectors = new SectorNodeDescription[sectorSpanHeight, sectorSpanWidth];
         for (var y = 0; y < sectorSpanHeight; y++) {
            var rng = new Random(y);
            for (var x = 0; x < sectorSpanWidth; x++) {
               var presets = new[]{ SectorMetadataPresets.FourSquares2D, SectorMetadataPresets.HashCircle2  };
               var sector = sectors[y, x] = TerrainService.CreateSectorNodeDescription(presets[rng.Next(presets.Length)]);
               sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(x * 1000, y * 1000, 0));
               TerrainService.AddSectorNodeDescription(sector);
            }
         }

         var left1 = new IntLineSegment2(new IntVector2(0, 200), new IntVector2(0, 400));
         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         var right1 = new IntLineSegment2(new IntVector2(1000, 200), new IntVector2(1000, 400));
         var right2 = new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800));
         for (var y = 0; y < sectorSpanHeight; y++) {
            for (var x = 1; x < sectorSpanWidth; x++) {
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right1, left1));
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right2, left2));
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left1, right1));
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left2, right2));
            }
         }

         var up1 = new IntLineSegment2(new IntVector2(200, 0), new IntVector2(400, 0));
         var up2 = new IntLineSegment2(new IntVector2(600, 0), new IntVector2(800, 0));
         var down1 = new IntLineSegment2(new IntVector2(200, 1000), new IntVector2(400, 1000));
         var down2 = new IntLineSegment2(new IntVector2(600, 1000), new IntVector2(800, 1000));
         for (var y = 1; y < sectorSpanHeight; y++) {
            for (var x = 0; x < sectorSpanWidth; x++) {
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y - 1, x], sectors[y, x], down1, up1));
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y - 1, x], sectors[y, x], down2, up2));
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y - 1, x], up1, down1));
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y - 1, x], up2, down2));
            }
         }

         var r = new Random(1);
         for (int i = 0; i < 30; i++) {
            var poly = Polygon2.CreateRect(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200));
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new DynamicTerrainHoleDescription { Polygons = new[] { poly } };
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
            var terrainHole = new DynamicTerrainHoleDescription { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

         for (int i = 0; i < 20; i++) {
            var w = r.Next(50, 100);
            var h = r.Next(50, 100);
            var poly = Polygon2.CreateRect(r.Next(800 + 80, 1100 - 80 - w) * 10 / 9, r.Next(180 - 40, 360 + 40 - h) * 10 / 9, w * 10 / 9, h * 10 / 9);
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new DynamicTerrainHoleDescription { Polygons = new[] { poly } };
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
            if (GameTimeService.Ticks >= GameTimeService.TicksPerSecond * 5) {
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

      public GameEvent CreateAddTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) {
         return new AddTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHoleDescription);
      }

      public GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) {
         return new RemoveTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHoleDescription);
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

      public void AddTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainService.AddTemporaryHoleDescription(holeDescription);
         // todo: can optimize to only invalidate paths intersecting hole.
         movementSystemService.HandleHoleAdded(holeDescription);
      }

      public void RemoveTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainService.RemoveTemporaryHoleDescription(holeDescription);
         movementSystemService.InvalidatePaths();
      }
   }
}