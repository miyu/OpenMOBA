using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation {
   public static class SectorMetadataPresets {
      private const int CrossCirclePathWidth = 200;
      private const int CrossCircleInnerLandRadius = 400;
      private const int CrossCircleInnerHoleRadius = 200;


      public const int HashCircle2ScalingFactor = 1;

      public static readonly TerrainStaticMetadata Blank2D = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new List<Polygon2>()
      };

      public static readonly TerrainStaticMetadata Test2D = new TerrainStaticMetadata {
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new[] {
            Polygon2.CreateRect(100, 600, 300, 300),
            Polygon2.CreateRect(400, 700, 100, 100),
            Polygon2.CreateRect(200, 900, 100, 150),
            Polygon2.CreateRect(600, 100, 300, 300),
            Polygon2.CreateRect(700, 400, 100, 100),
            Polygon2.CreateRect(200, 200, 100, 100),
            Polygon2.CreateRect(600, 850, 300, 50),
            Polygon2.CreateRect(600, 650, 50, 200),
            Polygon2.CreateRect(850, 650, 50, 200),
            Polygon2.CreateRect(600, 600, 300, 50),
            Polygon2.CreateRect(700, 700, 100, 100)
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
         LocalExcludedContours = new[] {
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

      public GameEvent CreateAddTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) {
         return new AddTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHoleDescription);
      }

      public GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) {
         return new RemoveTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHoleDescription);
      }

      public void Run() {
         Environment.CurrentDirectory = @"V:\my-repositories\miyu\derp\OpenMOBA.DevTool\bin\Debug\net461";
         LoadMeshAsMap("Assets/bunny.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0));

//         var sectorSpanWidth = 3;
//         var sectorSpanHeight = 1;
//         var sectors = new SectorNodeDescription[sectorSpanHeight, sectorSpanWidth];
//         for (var y = 0; y < sectorSpanHeight; y++) {
//            var rng = new Random(y);
//            for (var x = 0; x < sectorSpanWidth; x++) {
//               var presets = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Test2D, SectorMetadataPresets.FourSquares2D };
//               var preset = presets[x]; //rng.Next(presets.Length)];
//               var sector = sectors[y, x] = TerrainService.CreateSectorNodeDescription(preset);
//               sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(x * 1000 - 1500, y * 1000 - 500, 0));
//               TerrainService.AddSectorNodeDescription(sector);
//            }
//         }
//
//         var left1 = new IntLineSegment2(new IntVector2(0, 200), new IntVector2(0, 400));
//         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
//         var right1 = new IntLineSegment2(new IntVector2(1000, 200), new IntVector2(1000, 400));
//         var right2 = new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800));
//         for (var y = 0; y < sectorSpanHeight; y++)
//         for (var x = 1; x < sectorSpanWidth; x++) {
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right1, left1));
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right2, left2));
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left1, right1));
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left2, right2));
//         }
//
//         var up1 = new IntLineSegment2(new IntVector2(200, 0), new IntVector2(400, 0));
//         var up2 = new IntLineSegment2(new IntVector2(600, 0), new IntVector2(800, 0));
//         var down1 = new IntLineSegment2(new IntVector2(200, 1000), new IntVector2(400, 1000));
//         var down2 = new IntLineSegment2(new IntVector2(600, 1000), new IntVector2(800, 1000));
//         for (var y = 1; y < sectorSpanHeight; y++)
//         for (var x = 0; x < sectorSpanWidth; x++) {
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y - 1, x], sectors[y, x], down1, up1));
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y - 1, x], sectors[y, x], down2, up2));
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y - 1, x], up1, down1));
//            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y - 1, x], up2, down2));
//         }
//
//         var donutOriginX = 1250;
//         var donutOriginY = 300;
//         var donutThickness = 25;
//         var donutInnerSpan = 35;
//         var holeTsm = new TerrainStaticMetadata {
//            LocalBoundary = new Rectangle(donutOriginX, donutOriginY, 2 * donutThickness + donutInnerSpan, 2 * donutThickness + donutInnerSpan),
//            LocalIncludedContours = new[] { Polygon2.CreateRect(donutOriginX, donutOriginY, 2 * donutThickness + donutInnerSpan, 2 * donutThickness + donutInnerSpan) },
//            LocalExcludedContours = new List<Polygon2> {
//               Polygon2.CreateRect(donutOriginX + donutThickness, donutOriginY + donutThickness, donutInnerSpan, donutInnerSpan)
//            }
//         };
//         var hole = TerrainService.CreateHoleDescription(holeTsm);
//         hole.WorldTransform = Matrix4x4.Identity;
//         TerrainService.AddTemporaryHoleDescription(hole);

         var r = new Random(1);
         //for (int i = 0; i < 30; i++) {
         //   var poly = Polygon2.CreateRect(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200));
         //   var startTicks = r.Next(0, 500);
         //   var endTicks = r.Next(startTicks + 20, startTicks + 100);
         //   var terrainHole = new DynamicTerrainHoleDescription { Polygons = new[] { poly } };
         //   GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
         //   GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         //}
         //
         //r.NextBytes(new byte[1337]);
         //
         //for (int i = 0; i < 20; i++) {
         //   var w = r.Next(50, 100);
         //   var h = r.Next(50, 100);
         //   var poly = Polygon2.CreateRect(r.Next(800 + 80, 1100 - 80 - w) * 10 / 9, r.Next(520 - 40, 720 + 40 - h) * 10 / 9, w * 10 / 9, h * 10 / 9);
         //   var startTicks = r.Next(0, 500);
         //   var endTicks = r.Next(startTicks + 20, startTicks + 100);
         //   var terrainHole = new DynamicTerrainHoleDescription { Polygons = new[] { poly } };
         //   GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
         //   GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         //}
         //
         //for (int i = 0; i < 20; i++) {
         //   var w = r.Next(50, 100);
         //   var h = r.Next(50, 100);
         //   var poly = Polygon2.CreateRect(r.Next(800 + 80, 1100 - 80 - w) * 10 / 9, r.Next(180 - 40, 360 + 40 - h) * 10 / 9, w * 10 / 9, h * 10 / 9);
         //   var startTicks = r.Next(0, 500);
         //   var endTicks = r.Next(startTicks + 20, startTicks + 100);
         //   var terrainHole = new DynamicTerrainHoleDescription { Polygons = new[] { poly } };
         //   GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
         //   GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         //}

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
         for (var y = 0; y < 10; y++)
         for (var x = 0; x < 10; x++) {
            // var swarmlingRadius = 10f;
            var swarmlingRadius = (float)Math.Round(5.0f + 10.0f * (float)r.NextDouble());
            var p = new DoubleVector3(50, 500, 0);
            var offset = new DoubleVector3(x * swarmMeanRadius * 2, y * swarmMeanRadius * 2, 0);
            //               var swarmling = CreateTestEntity(p + offset, swarmlingRadius, benchmarkUnitBaseSpeed - 20 + 40 * (float)r.NextDouble());
            //               swarmling.MovementComponent.Swarm = swarm;
            //               swarm.Entities.Add(swarmling);
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

            foreach (var debugger in Debuggers)
               debugger.HandleFrameEnd(new FrameEndStatistics {
                  EventsProcessed = eventsProcessed
               });

//            List<DoubleVector3> objPath;
//            PathfinderCalculator.TryFindPath(15, new DoubleVector3(-600, 700, 0), new DoubleVector3(1500, 500, 0), out objPath);

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

      private void LoadMeshAsMap(string objPath, DoubleVector3 meshOffset, DoubleVector3 worldOffset, int scaling = 5000) {
         var lines = File.ReadLines(objPath);
         var verts = new List<DoubleVector3>();
         foreach (var line in lines.Select(l => l.Trim())) {
            if (line.StartsWith("#")) continue;
            var tokens = line.Split(' ');
            switch (tokens[0]) {
               case "v":
                  var v = meshOffset + new DoubleVector3(double.Parse(tokens[1]), double.Parse(tokens[2]), double.Parse(tokens[3]));
                  verts.Add(new DoubleVector3(v.X, -v.Z, v.Y));
                  break;
               case "f":
                  var v1 = verts[int.Parse(tokens[1]) - 1]; // origin
                  var v2 = verts[int.Parse(tokens[2]) - 1]; // a, x dim
                  var v3 = verts[int.Parse(tokens[3]) - 1]; // b, y dim

                  var a = v2 - v1; 
                  var b = v3 - v1;

                  var w = (int)(a.Norm2D() * scaling);
                  var h = (int)(b.Norm2D() * scaling);

                  var metadata = new TerrainStaticMetadata {
//                     LocalBoundary = new Rectangle(0, 0, 1000, 1000),
//                     LocalIncludedContours = new[] {
//                        Polygon2.CreateRect(200, 0, 200, 1000),
//                        Polygon2.CreateRect(600, 0, 200, 1000),
//                        Polygon2.CreateRect(0, 200, 1000, 200),
//                        Polygon2.CreateRect(0, 600, 1000, 200),
//                        Polygon2.CreateCircle(500, 500, 105, 64),
//                        Polygon2.CreateRect(450, 300, 100, 400),
//                        Polygon2.CreateRect(300, 450, 400, 100)
//                     },
//                     LocalExcludedContours = new Polygon2[] { }
                     LocalBoundary = new Rectangle(0, 0, w, h),
                     LocalIncludedContours = new List<Polygon2> {
                        new Polygon2(new List<IntVector2> {
                           new IntVector2(0, 0),
                           new IntVector2(0, h),
                           new IntVector2(w, 0),
                           new IntVector2(0, 0)
                        }, false)
                     },
                     LocalExcludedContours = new List<Polygon2>()
                  };
                  var snd = TerrainService.CreateSectorNodeDescription(metadata);

                  var triangleToWorld = Matrix4x4.Identity;

                  //                  matrix4x4.M41 = position.X;
                  //                  matrix4x4.M42 = position.Y;
                  //                  matrix4x4.M43 = position.Z;
                  //                  matrix4x4.M44 = 1f;


                  var alen = (float)a.Norm2D();
                  triangleToWorld.M11 = (float)a.X / alen;
                  triangleToWorld.M12 = (float)a.Y / alen;
                  triangleToWorld.M13 = (float)a.Z / alen;
                  triangleToWorld.M14 = 0.0f;

                  var blen = (float)b.Norm2D();
                  triangleToWorld.M21 = (float)b.X / blen;
                  triangleToWorld.M22 = (float)b.Y / blen;
                  triangleToWorld.M23 = (float)b.Z / blen;
                  triangleToWorld.M24 = 0.0f;

//                  triangleToWorld.M31 = 0.0f;
//                  triangleToWorld.M32 = 0.0f;
//                  triangleToWorld.M33 = 1.0f;
//                  triangleToWorld.M34 = 0.0f;

                  triangleToWorld.M41 = (float)v1.X * scaling + (float)worldOffset.X;
                  triangleToWorld.M42 = (float)v1.Y * scaling + (float)worldOffset.Y;
                  triangleToWorld.M43 = (float)v1.Z * scaling + (float)worldOffset.Z;
                  triangleToWorld.M44 = 1.0f;

                  //Console.WriteLine((v1 * scaling) + " " + Vector3.Transform(new Vector3(0, 0, 0), triangleToWorld));
                  //Console.WriteLine((v2 * scaling) + " " + Vector3.Transform(new Vector3(w, 0, 0), triangleToWorld));
                  //Console.WriteLine((v3 * scaling) + " " + Vector3.Transform(new Vector3(0, h, 0), triangleToWorld));
                  //Console.WriteLine(Vector3.Transform(new Vector3(0, 0, 1), triangleToWorld));

                  //                  triangleToWorld.Column1 = new Vector4(p2w - p1w, 0);
                  //                  triangleToWorld.Column2 = new Vector4(p3w - p1w, 0);
                  //                  triangleToWorld.Column3 = new Vector4(0, 0, 1, 0);
                  //                  triangleToWorld.Column4 = new Vector4(p1w, 1);

                  snd.WorldTransform = triangleToWorld;
                  TerrainService.AddSectorNodeDescription(snd);
                  break;
            }
         }

         var lowerbound = verts.Aggregate(new DoubleVector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity), (a, b) => new DoubleVector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)));
         var upperbound = verts.Aggregate(new DoubleVector3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity), (a, b) => new DoubleVector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
         Console.WriteLine(lowerbound + " " + upperbound);
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
      private readonly MovementSystemService movementSystemService;
      private readonly TerrainService terrainService;

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
