using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation {
   public static class SectorPresets {
      public static Sector Blank2D() {
         return new Sector {
            AbsoluteBounds = new Rectangle(0, 0, 1000, 1000),
            StaticHolePolygons = new List<Polygon>()
         };
      }

      public static Sector Test2D() {
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
         return new Sector {
            AbsoluteBounds = new Rectangle(0, 0, 1000, 1000),
            StaticHolePolygons = holes.ToList()
         };
      }

      public static Sector FourSquares2D() {
         var holes = new[] {
            Polygon.CreateRectXY(200, 200, 200, 200, 0),
            Polygon.CreateRectXY(200, 600, 200, 200, 0),
            Polygon.CreateRectXY(600, 200, 200, 200, 0),
            Polygon.CreateRectXY(600, 600, 200, 200, 0)
         };
         return new Sector {
            AbsoluteBounds = new Rectangle(0, 0, 1000, 1000),
            StaticHolePolygons = holes.ToList()
         };
      }

      public static Sector TransformToRect(this Sector sector, Rectangle rect) {
         OffsetSector(sector, -sector.AbsoluteBounds.X, -sector.AbsoluteBounds.Y);
         RescaleSectorXY(sector, rect.Size);
         OffsetSector(sector, rect.X, rect.Y);
         return sector;
      }

      public static Sector Rotate90(this Sector sector) {
         var loc = sector.AbsoluteBounds.Location;
         OffsetSector(sector, -loc.X, -loc.Y);
         foreach (var poly in sector.StaticHolePolygons) {
            for (var i = 0; i < poly.Points.Count; i++) {
               poly.Points[i] = new IntVector3(
                  sector.AbsoluteBounds.Height - poly.Points[i].Y,
                  sector.AbsoluteBounds.Width - poly.Points[i].X,
                  poly.Points[i].Z);
            }
         }
         sector.AbsoluteBounds = new Rectangle(0, 0, sector.AbsoluteBounds.Height, sector.AbsoluteBounds.Width);
         OffsetSector(sector, loc.X, loc.Y);
         return sector;
      }

      public static Sector FlipXY(this Sector sector) {
         var loc = sector.AbsoluteBounds.Location;
         OffsetSector(sector, -loc.X, -loc.Y);
         foreach (var poly in sector.StaticHolePolygons) {
            for (var i = 0; i < poly.Points.Count; i++) {
               poly.Points[i] = new IntVector3(poly.Points[i].Y, poly.Points[i].X, poly.Points[i].Z);
            }
         }
         sector.AbsoluteBounds = new Rectangle(0, 0, sector.AbsoluteBounds.Height, sector.AbsoluteBounds.Width);
         OffsetSector(sector, loc.X, loc.Y);
         return sector;
      }

      private static void OffsetSector(Sector sector, int dx, int dy) {
         foreach (var poly in sector.StaticHolePolygons) {
            for (var i = 0; i < poly.Points.Count; i++) {
               poly.Points[i] += new IntVector3(dx, dy, 0);
            }
         }
         sector.AbsoluteBounds = new Rectangle(
            sector.AbsoluteBounds.X + dx,
            sector.AbsoluteBounds.Y + dy,
            sector.AbsoluteBounds.Width,
            sector.AbsoluteBounds.Height);
      }
      private static void RescaleSectorXY(Sector sector, Size size) {
         foreach (var poly in sector.StaticHolePolygons) {
            for (var i = 0; i < poly.Points.Count; i++) {
               poly.Points[i] = new IntVector3(
                  poly.Points[i].X * size.Width / sector.AbsoluteBounds.Width,
                  poly.Points[i].Y * size.Height / sector.AbsoluteBounds.Height,
                  poly.Points[i].Z);
            }
         }
         sector.AbsoluteBounds = new Rectangle(sector.AbsoluteBounds.Location, size);
      }

      private static List<Polygon> CreateHoleSquiggle2D() {
         return PolylineOperations.ExtrudePolygon(
            new[] {
                  new IntVector2(100, 50),
                  new IntVector2(100, 100),
                  new IntVector2(200, 100),
                  new IntVector2(200, 150),
                  new IntVector2(200, 200),
                  new IntVector2(400, 250),
                  new IntVector2(200, 300),
                  new IntVector2(400, 315),
                  new IntVector2(200, 330),
                  new IntVector2(210, 340),
                  new IntVector2(220, 350),
                  new IntVector2(220, 400),
                  new IntVector2(221, 400)
               }.Select(iv => new IntVector3(iv.X + 160, iv.Y + 200, 0))
                .ToArray(),
            10).FlattenToPolygons();
      }
   }

   public interface IGameEventFactory {
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
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
         var sector1 = SectorPresets.Test2D().TransformToRect(new Rectangle(0, 0, 900, 900));
         TerrainService.AddSector(sector1);

         var sector2 = SectorPresets.FourSquares2D().TransformToRect(new Rectangle(1000, 0, 900, 900));
         TerrainService.AddSector(sector2);

         var connector12A = SectorPresets.Blank2D().TransformToRect(new Rectangle(900, 180, 100, 180));
         var connector12B = SectorPresets.Blank2D().TransformToRect(new Rectangle(900, 540, 100, 180));
         TerrainService.AddSector(connector12A);
         TerrainService.AddSector(connector12B);

         var r = new Random(1);
         for (int i = 0; i < 30; i++) {
            var poly = Polygon.CreateRectXY(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200), 0);
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new TerrainHole { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

//         var a = CreateTestEntity(new DoubleVector3(60, 40, 0), 15, 80);
//         var b = CreateTestEntity(new DoubleVector3(675, 175, 0), 15, 70);
//         var c = CreateTestEntity(new DoubleVector3(50, 900, 0), 15, 60);
//         var d = CreateTestEntity(new DoubleVector3(50, 500, 0), 15, 50);
//
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
}