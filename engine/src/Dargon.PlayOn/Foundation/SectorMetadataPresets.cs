using System.Collections.Generic;
using System.Drawing;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation {
   // Clipper int range: [-32,767, 32,767]
   public static class SectorMetadataPresets {
      public const int DesiredSectorExtents = InternalTerrainCompilationConstants.DesiredSectorExtents;

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
}