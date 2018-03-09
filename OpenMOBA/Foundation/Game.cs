using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Canvas3D;
using ClipperLib;
using OpenMOBA.DataStructures;
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
         var sw = new Stopwatch();
         sw.Start();

         Environment.CurrentDirectory = @"V:\my-repositories\miyu\derp\OpenMOBA.DevTool\bin\Debug\net461";
         // shift by something like -300, 0, 2700
         LoadMeshAsMap("Assets/bunny.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 30000);
//         LoadMeshAsMap("Assets/bunny_decimate_0_03.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 30000);

         var sphereHole = TerrainService.CreateHoleDescription(new SphereHoleStaticMetadata { Radius = 500 });
         sphereHole.WorldTransform = Matrix4x4.CreateTranslation(-561.450012207031f, -1316.31005859375f, -116.25f);
         GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(100), sphereHole));
//         GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(200), sphereHole));

         //LoadMeshAsMap("Assets/dragon.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 500);
         //LoadMeshAsMap("Assets/dragon_simp_15deg_decimate_collapse_0.01.obj", new DoubleVector3(0.015, -0.10, 0), new DoubleVector3(300, 0, -2700), 500);

         /*
         LoadMeshAsMap("Assets/cube.obj", new DoubleVector3(0, 0, 0), new DoubleVector3(0, 0, 0), 500);
         var holeDescription = TerrainService.CreateHoleDescription(new TerrainStaticMetadata {
         	LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         	LocalIncludedContours = new List<Polygon2> {
         		Polygon2.CreateCircle(500, 500, 800)
         	}
         });
         holeDescription.WorldTransform = Matrix4x4.CreateTranslation(-500, -500, 500);
         TerrainService.AddTemporaryHoleDescription(holeDescription); 
         //[]
         */


         /*
         var sector = TerrainService.CreateSectorNodeDescription(SectorMetadataPresets.Blank2D);
         sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(1 * 1000 - 1500, 0 * 1000 - 500, 0));
         TerrainService.AddSectorNodeDescription(sector);

         var left1 = new IntLineSegment2(new IntVector2(0, 200), new IntVector2(0, 400));
         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         var right1 = new IntLineSegment2(new IntVector2(1000, 200), new IntVector2(1000, 400));
         var right2 = new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800));
         TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector, sector, left2, left2));
         */

         /*
         var sectorSpanWidth = 3;
         var sectorSpanHeight = 1;
         var sectors = new SectorNodeDescription[sectorSpanHeight, sectorSpanWidth];
         for (var y = 0; y < sectorSpanHeight; y++) {
            var rng = new Random(y);
            for (var x = 0; x < sectorSpanWidth; x++) {
//               var presets = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Test2D, SectorMetadataPresets.FourSquares2D };
               var presets = new[] { SectorMetadataPresets.Blank2D, SectorMetadataPresets.Blank2D, SectorMetadataPresets.Blank2D };
               var preset = presets[x]; //rng.Next(presets.Length)];
               var sector = sectors[y, x] = TerrainService.CreateSectorNodeDescription(preset);
               sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(x * 1000 - 1500, y * 1000 - 500, 0));
               TerrainService.AddSectorNodeDescription(sector);
            }
         }
         
         var left1 = new IntLineSegment2(new IntVector2(0, 200), new IntVector2(0, 400));
         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         var right1 = new IntLineSegment2(new IntVector2(1000, 200), new IntVector2(1000, 400));
         var right2 = new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800));
         for (var y = 0; y < sectorSpanHeight; y++)
         for (var x = 1; x < sectorSpanWidth; x++) {
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right1, left1));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right2, left2));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left1, right1));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left2, right2));
         }
         
         var up1 = new IntLineSegment2(new IntVector2(200, 0), new IntVector2(400, 0));
         var up2 = new IntLineSegment2(new IntVector2(600, 0), new IntVector2(800, 0));
         var down1 = new IntLineSegment2(new IntVector2(200, 1000), new IntVector2(400, 1000));
         var down2 = new IntLineSegment2(new IntVector2(600, 1000), new IntVector2(800, 1000));
         for (var y = 1; y < sectorSpanHeight; y++)
         for (var x = 0; x < sectorSpanWidth; x++) {
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y - 1, x], sectors[y, x], down1, up1));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y - 1, x], sectors[y, x], down2, up2));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y - 1, x], up1, down1));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y - 1, x], up2, down2));
         }
         
//         var donutOriginX = 0;
//         var donutOriginY = 0;
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
         /**/

         var r = new Random(1);
         //         for (int i = 0; i < 300; i++) {
         //            var x = r.Next(0, 3000) - 1500;
         //            var y = r.Next(0, 1000) - 500;
         //            var width = r.Next(100, 200);
         //            var height = r.Next(100, 200);
         //            var startTicks = r.Next(0, 500);
         //            var endTicks = r.Next(startTicks + 20, startTicks + 100);
         //
         ////            if (i < 83 || i >= 85) continue;
         ////            if (i != 83) continue;
         //
         //            var holeTsm = new TerrainStaticMetadata {
         //               LocalBoundary = new Rectangle(x, y, width, height),
         //               LocalIncludedContours = new[] { Polygon2.CreateRect(x, y, width, height) }
         //            };
         //            var terrainHole = TerrainService.CreateHoleDescription(holeTsm);
         //            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
         //            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         //            Console.WriteLine($"Event: {x} {y}, {width} {height} @ {startTicks}-{endTicks}");
         ////            if (i == 5) break;
         //         }

         /*
         for (int i = 0; i < 150; i++) {
            break;
            var x = r.Next(-520, -480);
            var y = r.Next(80, 320);
            var width = r.Next(10, 20);
            var height = r.Next(10, 20);
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var rotation = r.NextDouble() * 2 * Math.PI;

            var contour = Polygon2.CreateRect(-width / 2, -height / 2, width, height).Points;
            var transform = Matrix3x2.CreateRotation((float)rotation);
            contour = contour.Map(p => Vector2.Transform(p.ToDoubleVector2().ToDotNetVector(), transform).ToOpenMobaVector().LossyToIntVector2())
                             .Map(p => p + new IntVector2(x, y))
                             .ToList();

            var bounds = IntRect2.BoundingPoints(contour.ToArray()).ToDotNetRectangle();

            var holeTsm = new PrismHoleStaticMetadata {
               LocalBoundary = bounds,
               LocalIncludedContours = new[] { new Polygon2(contour, false) }
            };
            var terrainHole = TerrainService.CreateHoleDescription(holeTsm);
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));

            Console.WriteLine($"Event: {x} {y}, {width} {height} {BitConverter.DoubleToInt64Bits(rotation)} @ {startTicks}-{endTicks}");
            //            if (i == 5) break;
         }

         for (var i = 0; i < 40; i++) {
            var sphereHole = TerrainService.CreateHoleDescription(new SphereHoleStaticMetadata { Radius = 100 });
            sphereHole.WorldTransform = Matrix4x4.CreateTranslation(-500, 200, -120 + 240 * i / 40);
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(i * 15), sphereHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(i * 15 + 14), sphereHole));
         }
         /**/

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
            if (GameTimeService.Ticks >= GameTimeService.TicksPerSecond * 20) {
               Console.WriteLine($"Done! {sw.Elapsed.TotalSeconds} at tick {GameTimeService.Ticks}");
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

      private void LoadMeshAsMap(string objPath, DoubleVector3 meshOffset, DoubleVector3 worldOffset, int scaling = 50000) {
         var lines = File.ReadLines(objPath);
         var verts = new List<DoubleVector3>();
         var previousEdges = new Dictionary<(int, int), (SectorNodeDescription, IntLineSegment2)>();

         void Herp(SectorNodeDescription node, int a, int b, IntLineSegment2 seg) {
            if (a > b) {
               (a, b) = (b, a); // a < b
               seg = new IntLineSegment2(seg.Second, seg.First);
            }

            if (previousEdges.TryGetValue((a, b), out var prev)) {
               var (prevNode, prevSeg) = prev;
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(node, prevNode, seg, prevSeg));
               TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(prevNode, node, prevSeg, seg));
            } else {
               previousEdges.Add((a, b), (node, seg));
            }
         }

         foreach (var (i, line) in lines.Select(l => l.Trim()).Enumerate()) {
            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;
            var tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            switch (tokens[0]) {
               case "v":
                  var v = meshOffset + new DoubleVector3(double.Parse(tokens[1]), double.Parse(tokens[2]), double.Parse(tokens[3]));
                  v = new DoubleVector3(v.X, -v.Z, v.Y);
                  v = v * scaling + worldOffset;
                  // todo: flags for dragon / bunny to switch handiness + rotate
                  verts.Add(v);
//                  verts.Add(new DoubleVector3(v.X, v.Y, v.Z));
                  break;
               case "f":
//                  Console.WriteLine($"Loading face of line {i}");
                  var i1 = int.Parse(tokens[1]) - 1;
                  var i2 = int.Parse(tokens[2]) - 1;
                  var i3 = int.Parse(tokens[3]) - 1;

                  var v1 = verts[i1]; // origin
                  var v2 = verts[i2]; // a, x dim
                  var v3 = verts[i3]; // b, y dim

                  /***
                   *            ___      
                   *    /'.      |     ^
                   * b /.t '.    | h   | vert
                   *  /__'___'. _|_    |
                   *     a
                   *  |-------| w
                   *  |---| m
                   *  
                   *          ___      
                   *  \.       |     ^
                   * b \'.     | h   | vert
                   *    \_'.  _|_    |
                   *    |--| w
                   *  |-| m
                   */
                  var a = v2 - v1; 
                  var b = v3 - v1;
                  var theta = Math.Acos(a.Dot(b) / (a.Norm2D() * b.Norm2D())); // a.b =|a||b|cos(theta)

                  var w = a.Norm2D();
                  var h = b.Norm2D() * Math.Sin(theta);
                  var m = b.Norm2D() * Math.Cos(theta);

                  var scaleBound = 1000; //ClipperBase.loRange
                  var localUpscale = scaleBound * 0.9f / (float)Math.Max(Math.Abs(m), Math.Max(Math.Abs(h), w));
                  var globalDownscale = 1.0f / localUpscale;
                  // Console.WriteLine(localUpscale + " " + (int)(m * localUpscale) + " " + (int)(h * localUpscale) + " " + (int)(w * localUpscale));

                  var po = new IntVector2(0, 0);
                  var pa = new IntVector2((int)(w * localUpscale), 0);
                  var pb = new IntVector2((int)(m * localUpscale), (int)(h * localUpscale));
                  var metadata = new TerrainStaticMetadata {
                     LocalBoundary = m < 0 ? new Rectangle((int)(m * localUpscale), 0, (int)((w - m) * localUpscale), (int)(h * localUpscale)) : new Rectangle(0, 0, (int)(w * localUpscale), (int)(h * localUpscale)),
                     LocalIncludedContours = new List<Polygon2> {
                        new Polygon2(new List<IntVector2> { po, pb, pa, po }, false)
                     },
                     LocalExcludedContours = new List<Polygon2>()
                  };

                  foreach (var zzz in metadata.LocalIncludedContours) {
                     foreach (var p in zzz.Points) {
                        if (Math.Abs(p.X) >= ClipperBase.loRange || Math.Abs(p.Y) >= ClipperBase.loRange) {
                           throw new Exception("!!!!");
                        }
                     }
                  }

                  var snd = TerrainService.CreateSectorNodeDescription(metadata);
                  var triangleToWorld = Matrix4x4.Identity;

                  var alen = (float)a.Norm2D();
                  triangleToWorld.M11 = globalDownscale * (float)a.X / alen;
                  triangleToWorld.M12 = globalDownscale * (float)a.Y / alen;
                  triangleToWorld.M13 = globalDownscale * (float)a.Z / alen;
                  triangleToWorld.M14 = 0.0f;

                  var n = a.Cross(b).ToUnit();
                  var vert = n.Cross(a).ToUnit();
//                  var blen = (float)b.Norm2D();
                  triangleToWorld.M21 = globalDownscale * (float)vert.X;
                  triangleToWorld.M22 = globalDownscale * (float)vert.Y;
                  triangleToWorld.M23 = globalDownscale * (float)vert.Z;
                  triangleToWorld.M24 = 0.0f;

                  triangleToWorld.M31 = globalDownscale * (float)n.X;
                  triangleToWorld.M32 = globalDownscale * (float)n.Y;
                  triangleToWorld.M33 = globalDownscale * (float)n.Z;
                  triangleToWorld.M34 = 0.0f;

                  triangleToWorld.M41 = (float)v1.X;
                  triangleToWorld.M42 = (float)v1.Y;
                  triangleToWorld.M43 = (float)v1.Z;
                  triangleToWorld.M44 = 1.0f;

                  snd.WorldTransform = triangleToWorld;
                  snd.WorldToLocalScalingFactor = localUpscale;
                  TerrainService.AddSectorNodeDescription(snd);

//                  var store = new SectorGraphDescriptionStore();
//                  var ts = new TerrainService(store, new TerrainSnapshotCompiler(store));
//                  ts.AddSectorNodeDescription(snd);
//                  ts.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(snd, snd, new IntLineSegment2(po, pa), new IntLineSegment2(po, pa)));
//                  ts.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(snd, snd, new IntLineSegment2(pa, pb), new IntLineSegment2(pa, pb)));
//                  ts.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(snd, snd, new IntLineSegment2(pb, po), new IntLineSegment2(pb, po)));
//                  ts.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(0.0);

                  Herp(snd, i1, i2, new IntLineSegment2(po, pa));
                  Herp(snd, i2, i3, new IntLineSegment2(pa, pb));
                  Herp(snd, i3, i1, new IntLineSegment2(pb, po));
                  break;
            }
         }

         var lowerbound = verts.Aggregate(new DoubleVector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity), (a, b) => new DoubleVector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)));
         var upperbound = verts.Aggregate(new DoubleVector3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity), (a, b) => new DoubleVector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
         Console.WriteLine(lowerbound + " " + upperbound + " " + (upperbound + lowerbound) / 2 + " " + (upperbound - lowerbound));
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
