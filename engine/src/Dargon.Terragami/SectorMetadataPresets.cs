using System.Collections.Generic;
using System.Drawing;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   // Clipper int range: [-32,767, 32,767]
   public static class SectorBlueprints {
      public const int DesiredSectorExtents = InternalTerrainCompilationConstants.DesiredSectorExtents;

      private const int CrossCirclePathWidth = 200;
      private const int CrossCircleInnerLandRadius = 400;
      private const int CrossCircleInnerHoleRadius = 200;

      public const int HashCircle2ScalingFactor = 1;

      private static SectorBlueprint New(string Name, Rectangle LocalBoundary, IEnumerable<Polygon2> LocalIncludedContours, IEnumerable<Polygon2> LocalExcludedContours) {
         var offsetX = -(LocalBoundary.X + LocalBoundary.Width / 2);
         var offsetY = -(LocalBoundary.Y + LocalBoundary.Height / 2);

         var scaleX = 2 * DesiredSectorExtents / (double)LocalBoundary.Width;
         var scaleY = 2 * DesiredSectorExtents / (double)LocalBoundary.Height;

         void TransformPolygonInPlace(Polygon2 poly) {
            for (var i = 0; i < poly.Points.Count; i++) {
               var p = poly.Points[i];
               var x = (int)((p.X + offsetX) * scaleX);
               var y = (int)((p.Y + offsetY) * scaleY);
               poly.Points[i] = new IntVector2(x, y);
            }
         }

         LocalBoundary.Offset(offsetX, offsetY);

         LocalBoundary = new Rectangle(
            (int)(LocalBoundary.X * scaleX), (int)(LocalBoundary.Y * scaleY),
            (int)(LocalBoundary.Width * scaleX), (int)(LocalBoundary.Height * scaleY));

         foreach (var contour in LocalIncludedContours) {
            TransformPolygonInPlace(contour);
         }
         foreach (var contour in LocalExcludedContours) {
            TransformPolygonInPlace(contour);
         }

         var punch = PolygonOperations.Punch()
                                      .Include(LocalIncludedContours)
                                      .Exclude(LocalExcludedContours)
                                      .Execute();

         return new SectorBlueprint {
            Name = Name,
            LocalBoundary = LocalBoundary,
            Root = punch,
         };
      }

      public static readonly SectorBlueprint Blank2D = New(
         Name: nameof(Blank2D),
         LocalBoundary: new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours: new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours: new List<Polygon2>()
         );

      public static readonly SectorBlueprint Test2D = New(
         Name: nameof(Test2D),
         LocalBoundary: new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours: new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours: new[] {
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
         });

      public static readonly SectorBlueprint TestMiyu = New(
         Name: nameof(TestMiyu),
         LocalBoundary: new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours: new[] {
            Polygon2.CreateRect(0, 0, 1000, 1000),
            Polygon2.CreateRect(200, 200, 600, 600, rev: true),
            Polygon2.CreateRect(400, 400, 200, 200),
         },
         LocalExcludedContours: new Polygon2[] { });

      public static readonly SectorBlueprint FourSquares2D = New(
         Name: nameof(FourSquares2D),
         LocalBoundary: new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours: new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours: new[] {
            Polygon2.CreateRect(200, 200, 200, 200),
            Polygon2.CreateRect(200, 600, 200, 200),
            Polygon2.CreateRect(600, 200, 200, 200),
            Polygon2.CreateRect(600, 600, 200, 200)
         });

      public static readonly SectorBlueprint CrossCircle = New(
         Name: nameof(CrossCircle),
         LocalBoundary: new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours: new[] {
            Polygon2.CreateRect((1000 - CrossCirclePathWidth) / 2, 0, CrossCirclePathWidth, 1000),
            Polygon2.CreateRect(0, (1000 - CrossCirclePathWidth) / 2, 1000, CrossCirclePathWidth),
            Polygon2.CreateCircle(500, 500, CrossCircleInnerLandRadius)
         },
         LocalExcludedContours: new[] {
            Polygon2.CreateCircle(500, 500, CrossCircleInnerHoleRadius)
         });

      public static readonly SectorBlueprint HashCircle1 = New(
         Name: nameof(HashCircle1),
         LocalBoundary: new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours: new[] {
            Polygon2.CreateRect(200, 0, 200, 1000),
            Polygon2.CreateRect(600, 0, 200, 1000),
            Polygon2.CreateRect(0, 200, 1000, 200),
            Polygon2.CreateRect(0, 600, 1000, 200),
            Polygon2.CreateCircle(500, 500, 105, false, 64),
            Polygon2.CreateRect(450, 300, 100, 400),
            Polygon2.CreateRect(300, 450, 400, 100)
         },
         LocalExcludedContours: new Polygon2[] { });

      public static readonly SectorBlueprint HashCircle2 = New(
         Name: nameof(HashCircle2),
         LocalBoundary: new Rectangle(0, 0, 1000 * HashCircle2ScalingFactor, 1000 * HashCircle2ScalingFactor),
         LocalIncludedContours: new[] {
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
         LocalExcludedContours: new[] {
            Polygon2.CreateCircle(500 * HashCircle2ScalingFactor, 500 * HashCircle2ScalingFactor, 200 * HashCircle2ScalingFactor)
         });


      private const int kLaneThickness = 141;
      private const int kLaneThicknessDivSqrt2 = 100;
      private const int kNearCorner = kLaneThickness + kLaneThicknessDivSqrt2;
      private const int kFarCorner = 1000 - kNearCorner;

      public static readonly SectorBlueprint DotaStyleMoba = New(
         Name: nameof(DotaStyleMoba),
         LocalBoundary: new Rectangle(0, 0, 1000, 1000),
         LocalIncludedContours: new[] { Polygon2.CreateRect(0, 0, 1000, 1000) },
         LocalExcludedContours: new[] {
            new Polygon2(Polygon2.ValidateHoleClockness(new List<IntVector2> {
               new IntVector2(kLaneThickness, kNearCorner),
               new IntVector2(kLaneThickness, 1000 - kLaneThickness),
               new IntVector2(kFarCorner, 1000 - kLaneThickness),
            })),
            new Polygon2(Polygon2.ValidateHoleClockness(new List<IntVector2> {
               new IntVector2(kNearCorner, kLaneThickness),
               new IntVector2(1000 - kLaneThickness, kFarCorner),
               new IntVector2(1000 - kLaneThickness, kLaneThickness),
            })),
         });
   }
}
