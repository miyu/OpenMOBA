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
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Foundation {
   // Clipper int range: [-32,767, 32,767]
   public static class SectorMetadataPresets {
      private const int DesiredSectorExtents = (ClipperBase.hiRange / 10000) * 5000;

      private const int CrossCirclePathWidth = 200;
      private const int CrossCircleInnerLandRadius = 400;
      private const int CrossCircleInnerHoleRadius = 200;

      public const int HashCircle2ScalingFactor = 1;

      public static readonly TerrainStaticMetadata Blank2D = new TerrainStaticMetadata {
         Name = nameof(Blank2D),
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new List<Polygon2>()
      }.Twitch();

      internal static TerrainStaticMetadata Twitch(this TerrainStaticMetadata tsm) {
         var offsetX = -(tsm.LocalBoundary.X + tsm.LocalBoundary.Width / 2);
         var offsetY = -(tsm.LocalBoundary.Y + tsm.LocalBoundary.Height / 2);

         var scaleX = 2 * DesiredSectorExtents / (double)tsm.LocalBoundary.Width;
         var scaleY = 2 * DesiredSectorExtents / (double)tsm.LocalBoundary.Height;

         void TransformPolygonInPlace(Polygon2 poly) {
            for (var i = 0; i < poly.Points.Count; i++) {
               var p = poly.Points[i];
               var x = (int)((p.X + offsetX) * scaleX);
               var y = (int)((p.Y + offsetY) * scaleY);
               poly.Points[i] = new IntVector2(x, y);
            }
         }

         tsm.LocalBoundary.Offset(offsetX, offsetY);

         tsm.LocalBoundary = new Rectangle(
            (int)(tsm.LocalBoundary.X * scaleX), (int)(tsm.LocalBoundary.Y * scaleY),
            (int)(tsm.LocalBoundary.Width * scaleX), (int)(tsm.LocalBoundary.Height * scaleY));

         foreach (var contour in tsm.LocalIncludedContours) {
            TransformPolygonInPlace(contour);
         }
         foreach (var contour in tsm.LocalExcludedContours) {
            TransformPolygonInPlace(contour);
         }
         return tsm;
      }

      public static readonly TerrainStaticMetadata Test2D = new TerrainStaticMetadata {
         Name = nameof(Test2D),
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new[] {
            Polygon2.CreateRect(100, 600, 300, 300),
            Polygon2.CreateRect(400, 700, 100, 100),
            Polygon2.CreateRect(200, 900, 100, 101), // 101 was 150
            Polygon2.CreateRect(600, 100, 300, 300),
            Polygon2.CreateRect(700, 400, 100, 100),
            Polygon2.CreateRect(200, 200, 100, 100),
            Polygon2.CreateRect(600, 850, 300, 50),
            Polygon2.CreateRect(600, 650, 50, 200),
            Polygon2.CreateRect(850, 650, 50, 200),
            Polygon2.CreateRect(600, 600, 300, 50),
            Polygon2.CreateRect(700, 700, 100, 100)
         }
      }.Twitch();

      public static readonly TerrainStaticMetadata FourSquares2D = new TerrainStaticMetadata {
         Name = nameof(FourSquares2D),
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours = new[] {
            Polygon2.CreateRect(200, 200, 200, 200),
            Polygon2.CreateRect(200, 600, 200, 200),
            Polygon2.CreateRect(600, 200, 200, 200),
            Polygon2.CreateRect(600, 600, 200, 200)
         }
      }.Twitch();

      public static readonly TerrainStaticMetadata CrossCircle = new TerrainStaticMetadata {
         Name = nameof(CrossCircle),
         LocalBoundary = new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours = new[] {
            Polygon2.CreateRect((1000 - CrossCirclePathWidth) / 2, 0, CrossCirclePathWidth, 1000),
            Polygon2.CreateRect(0, (1000 - CrossCirclePathWidth) / 2, 1000, CrossCirclePathWidth),
            Polygon2.CreateCircle(500, 500, CrossCircleInnerLandRadius)
         },
         LocalExcludedContours = new[] {
            Polygon2.CreateCircle(500, 500, CrossCircleInnerHoleRadius)
         }
      }.Twitch();

      public static readonly TerrainStaticMetadata HashCircle1 = new TerrainStaticMetadata {
         Name = nameof(HashCircle1),
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
      }.Twitch();

      public static readonly TerrainStaticMetadata HashCircle2 = new TerrainStaticMetadata {
         Name = nameof(HashCircle2),
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
      }.Twitch();
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

      public void Initialize() {
         // shift by something like -300, 0, 2700
         //TerrainService.LoadMeshAsMap("Assets/bunny.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 30000);
         // TerrainService.LoadMeshAsMap("Assets/bunny_decimate_0_03.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 30000);

         // var sphereHole = TerrainService.CreateHoleDescription(new SphereHoleStaticMetadata { Radius = 500 });
         // sphereHole.WorldTransform = Matrix4x4.CreateTranslation(-561.450012207031f, -1316.31005859375f, -116.25f);
         // GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(0), sphereHole));
         // GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(200), sphereHole));

         // TerrainService.LoadMeshAsMap("Assets/dragon.obj", new DoubleVector3(0.015, -0.10, 0.0), new DoubleVector3(0, 0, 0), 500);
         // TerrainService.LoadMeshAsMap("Assets/dragon_simp_15deg_decimate_collapse_0.01.obj", new DoubleVector3(0.015, -0.10, 0), new DoubleVector3(300, 0, -2700), 500);

         /*
         TerrainService.LoadMeshAsMap("Assets/cube.obj", new DoubleVector3(0, 0, 0), new DoubleVector3(0, 0, 0), 500);
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

         /**/
         var sectorSpanWidth = 1;
         var sectorSpanHeight = 1;
         var sectors = new SectorNodeDescription[sectorSpanHeight, sectorSpanWidth];
         for (var y = 0; y < sectorSpanHeight; y++) {
            var rng = new Random(y);
            for (var x = 0; x < sectorSpanWidth; x++) {
//               var presets = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Test2D, SectorMetadataPresets.FourSquares2D };
//               var presets = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Test2D, SectorMetadataPresets.HashCircle2 };
//               var presets = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Blank2D, SectorMetadataPresets.HashCircle2 };
//               var presets = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Test2D, SectorMetadataPresets.HashCircle2 };
               var presets = new[] { SectorMetadataPresets.Test2D };
//               var presets = new[] { SectorMetadataPresets.Blank2D, SectorMetadataPresets.Blank2D, SectorMetadataPresets.Blank2D };
               var preset = presets[x]; //rng.Next(presets.Length)];
               var sector = sectors[y, x] = TerrainService.CreateSectorNodeDescription(preset);
               //sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1000.0f / 60000.0f), Matrix4x4.CreateTranslation(x * 1000 - 1000, y * 1000, 0));
               sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1000.0f / 30000.0f), Matrix4x4.CreateTranslation(0, 0, 0));
               sector.WorldToLocalScalingFactor = (cDouble)30000 / (cDouble)1000;
               TerrainService.AddSectorNodeDescription(sector);
            }
         }


         var left1 = new IntLineSegment2(new IntVector2(-30000 / 2, -18000 / 2), new IntVector2(-30000 / 2, -6000 / 2));
         var left2 = new IntLineSegment2(new IntVector2(-30000 / 2, 6000 / 2), new IntVector2(-30000 / 2, 18000 / 2));
         var right1 = new IntLineSegment2(new IntVector2(30000 / 2, -18000 / 2), new IntVector2(30000 / 2, -6000 / 2));
         var right2 = new IntLineSegment2(new IntVector2(30000 / 2, 6000 / 2), new IntVector2(30000 / 2, 18000 / 2));

//         var left1 = new IntLineSegment2(new IntVector2(0, 200), new IntVector2(0, 400));
//         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
//         var right1 = new IntLineSegment2(new IntVector2(1000, 200), new IntVector2(1000, 400));
//         var right2 = new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800));
         for (var y = 0; y < sectorSpanHeight; y++)
         for (var x = 1; x < sectorSpanWidth; x++) {
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right1, Clockness.CounterClockwise, left1, Clockness.Clockwise));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x - 1], sectors[y, x], right2, Clockness.CounterClockwise, left2, Clockness.Clockwise));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left1, Clockness.Clockwise, right1, Clockness.CounterClockwise));
            TerrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sectors[y, x], sectors[y, x - 1], left2, Clockness.Clockwise, right2, Clockness.CounterClockwise));
         }
         
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
         for (int i = 0; i < 30; i++) {
            break;
            var left = r.Next(0, 800);
            var top = r.Next(0, 800);
            var width = r.Next(100, 200);
            var height = r.Next(100, 200);
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);

            // account for test2d being flipped on Y back in the day
            top = 1000 - top - height;

            // account for create rectangle hole taking center x/y
            left += width / 2;
            top += height / 2;

            // center
            left -= 500;
            top -= 500;

            var holeMetadata = (PrismHoleStaticMetadata)HoleStaticMetadata.CreateRectangleHoleMetadata(left, top, width, height, 0);
            Trace.Assert(holeMetadata.LocalIncludedContours.Count == 1);
            var str = string.Join("; ", holeMetadata.LocalIncludedContours.First().Points.Select(p => p.ToString()));
            Console.WriteLine($"{left} {top} {width} {height} {startTicks} {endTicks} {str}");

            var terrainHole = TerrainService.CreateHoleDescription(holeMetadata);
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

         //         for (int i = 0; i < 50; i++) {
         //            var x = r.Next(0, 3000) - 1500;
         //            var y = r.Next(0, 1000) - 500;
         //            var width = r.Next(50, 200);
         //            var height = r.Next(50, 200);
         //            var startTicks = r.Next(0, 500);
         //            var endTicks = r.Next(startTicks + 20, startTicks + 100);
         //         
         //            //if (i < 83 || i >= 85) continue;
         //            //if (i != 83) continue;
         //         
         //            //var holeTsm = new TerrainStaticMetadata {
         //            //   LocalBoundary = new Rectangle(x, y, width, height),
         //            //   LocalIncludedContours = new[] { Polygon2.CreateRect(x, y, width, height) }
         //            //};
         //            var terrainHole = TerrainService.CreateHoleDescription(HoleStaticMetadata.CreateRectangleHoleMetadata(x, y, width, height, 0));
         //            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
         //            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         //            Console.WriteLine($"Event: {x} {y}, {width} {height} @ {startTicks}-{endTicks}");
         //            //if (i == 5) break;
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

         //var a = CreateTestEntity(new DoubleVector3(60 - 500, (1000 - 40) - 500, 0), 15, 80); //675 - 500 - 10, 175 - 500 - 10
         //var b = CreateTestEntity(new DoubleVector3(675 - 500, (1000 - 175) - 500, 0), 15, 70); //675 - 500 - 10, 175 - 500 - 10
         //var c = CreateTestEntity(new DoubleVector3(50 - 500, (1000 - 900) - 500, 0), 25, 60); //675 - 500 - 10, 175 - 500 - 10
         //var d = CreateTestEntity(new DoubleVector3(50 - 500, (1000 - 500) - 500, 0), 25, 50); //675 - 500 - 10, 175 - 500 - 10
         //var e = CreateTestEntity(new DoubleVector3(-1350, 200, 0), 30, 100);
         //         var e = CreateTestEntity(new DoubleVector3(-650, 180, 0), 30, 100);
         //         var c = CreateTestEntity(new DoubleVector3(50 - 500, 900 - 500, 0), 15, 60);
         //         var d = CreateTestEntity(new DoubleVector3(50 - 500, 500 - 500, 0), 15, 50);

         //MovementSystemService.Pathfind(a, new DoubleVector3(930 - 500, (1000 - 300) - 500, 0));
         //MovementSystemService.Pathfind(b, new DoubleVector3(825 - 500, (1000 - 300) - 500, 0));
         //MovementSystemService.Pathfind(c, new DoubleVector3(950 - 500, (1000 - 475) - 500, 0));
         //MovementSystemService.Pathfind(d, new DoubleVector3(80 - 500, (1000 - 720) - 500, 0));
         //MovementSystemService.Pathfind(e, new DoubleVector3(1250, -80, 0));
         //         MovementSystemService.Pathfind(c, new DoubleVector3(950 - 500, 475 - 500, 0));
         //         MovementSystemService.Pathfind(d, new DoubleVector3(80 - 500, 720 - 500, 0));

//         var benchmarkDestination = new DoubleVector3(1000, 325, 0.0);
         return;
         var benchmarkDestination = new DoubleVector3(425, 425, 0);
         var benchmarkUnitBaseSpeed = (cDouble)100;
         var swarm = new Swarm { Destination = benchmarkDestination };
         var swarmMeanRadius = (cDouble)10;
         for (var y = 0; y < 10; y++) {
            for (var x = 0; x < 10; x++) {
               // var swarmlingRadius = 10f;
               var swarmlingRadius = CDoubleMath.Round(CDoubleMath.c5 + (cDouble)10 * r.NextCDouble());
               var p = new DoubleVector3(-450, -150, 0);
               var offset = new DoubleVector3((cDouble)x * swarmMeanRadius * CDoubleMath.c2, (cDouble)y * swarmMeanRadius * CDoubleMath.c2, CDoubleMath.c0);
               var swarmling = CreateTestEntity(p + offset, swarmlingRadius, benchmarkUnitBaseSpeed);
               swarmling.MovementComponent.Swarm = swarm;
               swarm.Entities.Add(swarmling);
            }
         }

         //var optimal = CreateTestEntity(new DoubleVector3(50 + 9 * 10*2, 500, 0.0), 10, benchmarkUnitBaseSpeed);
         //MovementSystemService.Pathfind(optimal, benchmarkDestination);
      }

      public void Run() {
         var sw = new Stopwatch();
         sw.Start();

         IntMath.Sqrt(0); // init static

         while (true) {
            if (GameTimeService.Ticks >= GameTimeService.TicksPerSecond * 20) {
               Console.WriteLine($"Done! {sw.Elapsed.TotalSeconds} at tick {GameTimeService.Ticks}");
               break;
            }
            Tick();
         }

         var latch = new CountdownEvent(1);
         new Thread(() => {
            DebugProfiler.DumpToClipboard();
            latch.Signal();
         }) { ApartmentState = ApartmentState.STA }.Start();
         latch.Wait();
      }

      public void Tick() {
         DebugProfiler.EnterTick(GameTimeService.Ticks);

         GameEventQueueService.ProcessPendingGameEvents(out var eventsProcessed);

         EntityService.ProcessSystems();

         DebugProfiler.LeaveTick();

         foreach (var debugger in Debuggers) {
            debugger.HandleFrameEnd(new FrameEndStatistics {
               EventsProcessed = eventsProcessed
            });
         }

         GameTimeService.IncrementTicks();
      }

      private Entity CreateTestEntity(DoubleVector3 initialPosition, cDouble radius, cDouble movementSpeed) {
         var entity = EntityService.CreateEntity();
         EntityService.AddEntityComponent(entity, new MovementComponent {
            WorldPosition = initialPosition,
            BaseRadius = radius,
            BaseSpeed = movementSpeed
         });
         return entity;
      }
   }

   public class Swarm {
      private readonly Dictionary<int, (TerrainOverlayNetworkNode, PathfinderResultContext)> pathfinderResultContextByComputedRadius = new Dictionary<int, (TerrainOverlayNetworkNode, PathfinderResultContext)>();
      private DoubleVector3 destination;

      public List<Entity> Entities { get; set; } = new List<Entity>();

      public DoubleVector3 Destination {
         get => destination;
         set => SetDestination(value);
      }

      public void SetDestination(DoubleVector3 value) {
         destination = value;
         pathfinderResultContextByComputedRadius.Clear();
      }

      public PathfinderResultContext GetPriorPathfinderResultContextOrNull(int computedRadius, TerrainOverlayNetworkNode destinationNode) {
         if (!pathfinderResultContextByComputedRadius.TryGetValue(computedRadius, out var tuple) ||
             tuple.Item1 != destinationNode) {
            return null;
         }
         Trace.Assert(tuple.Item2 != null);
         return tuple.Item2;
      }

      public void SetPriorPathfinderResultContext(int computedRadius, TerrainOverlayNetworkNode destinationNode, PathfinderResultContext pathfinderResultContext) {
         pathfinderResultContextByComputedRadius[computedRadius] = (destinationNode, pathfinderResultContext);
      }
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
