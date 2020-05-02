using System.Drawing;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using Xunit;
using static NMockito.NMockitoStatics;

namespace Dargon.Terragami.Tests {
   public class CoordinateSystemConventionsTests {
      /// <summary>
      /// Clockness is evaluated with an X/Y coordinate system that a human would draw on a piece of graph paper,
      /// where Y is up, not down like in screen coordinates.
      /// </summary>
      [Fact]
      public void HumanGraphPaperNotScreenCoordinatesClockness() {
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               new DoubleVector2(0, 0),
               new DoubleVector2(1, 0),
               new DoubleVector2(1, -1)));
      }

      /// <summary>
      /// The convention in graphics is that front-facing polygons are clockwise, and back-facing polygons
      /// are counter-clockwise.
      ///
      /// We have two choices on convention:
      /// 1. Land polygons are clockwise, back-facing polygons are counter-clockwise.
      /// 2. Front-facing segments are clockwise.
      ///
      /// We additionally have the choice of whether clockness is in relation to a Y-up or Y-down coordinate
      /// system... but as asserted prior, Terragami universally uses human Y-up coordinates. It's important
      /// to remember this, as dependencies like Clipper use SCREEN COORDINATES when referring to positive/negative
      /// clockness (how it computes winding count -- see http://glprogramming.com/red/chapter11.html).
      /// 
      ///     q______r 
      ///     |      | 
      /// a-> |  b-> | 
      ///     |______|
      ///     t      s
      ///
      /// Note atq is CCW, while brs is CW.
      ///
      /// For the purpose of terrain representation in polygon punching, positive (land) regions are clockwise,
      /// and negative (e.g. void/hole) polygons are counterclockwise. Because we orient ourselves in human/graph-paper
      /// coordinates as opposed to screen-coordinates, Clipper sees our positive regions as negative and our negative
      /// regions as positive.
      /// </summary>
      [Fact]
      public void TerrainDefinitionIsPositiveClockWiseNegativeCounterClockWise() {
         var poly = Polygon2.CreateRect(0, 0, 10, 10);

         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(poly.Points[0], poly.Points[1], poly.Points[2]));
         AssertEquals(1, PolygonOperations.Punch().Include(poly).Execute().Children.Length);
         AssertNotEquals(poly.Points[0], poly.Points[^1]);

         poly = Polygon2.CreateCircle(0, 0, 10);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(poly.Points[0], poly.Points[1], poly.Points[2]));
         AssertEquals(1, PolygonOperations.Punch().Include(poly).Execute().Children.Length);
         AssertNotEquals(poly.Points[0], poly.Points[^1]);
      }

      [Fact]
      public void PolygonUnionPunchOperationsOrientationsArentBorked_CircleSubjectTest() {
         var poly = Polygon2.CreateCircle(0, 0, 10);
         var union = PolygonOperations.Union().Include(poly).Execute();
         AssertEquals(1, union.Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               union.Children[0].Contour[0],
               union.Children[0].Contour[1],
               union.Children[0].Contour[2]));
         AssertNotEquals(union.Children[0].Contour[0], union.Children[0].Contour[^1]);

         var punch = PolygonOperations.Punch().Include(poly).Execute();
         AssertEquals(1, punch.Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               punch.Children[0].Contour[0],
               punch.Children[0].Contour[1],
               punch.Children[0].Contour[2]));
         AssertNotEquals(punch.Children[0].Contour[0], punch.Children[0].Contour[^1]);
      }

      [Fact]
      public void PolygonOffsetOperationsOrientationsArentBorked_CircleSubjectTest() {
         var poly = Polygon2.CreateCircle(0, 0, 100);
         var dilate = PolygonOperations.Offset().Include(poly).Dilate(2).Execute();
         AssertEquals(1, dilate.Children.Length);
         AssertEquals(0, dilate.Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               dilate.Children[0].Contour[0],
               dilate.Children[0].Contour[1],
               dilate.Children[0].Contour[2]));
         AssertNotEquals(dilate.Children[0].Contour[0], dilate.Children[0].Contour[^1]);

         var erode = PolygonOperations.Offset().Include(poly).Erode(2).Execute();
         AssertEquals(1, erode.Children.Length);
         AssertEquals(0, erode.Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               erode.Children[0].Contour[0],
               erode.Children[0].Contour[1],
               erode.Children[0].Contour[2]));
         AssertNotEquals(erode.Children[0].Contour[0], erode.Children[0].Contour[^1]);
      }

      [Fact]
      public void PolygonOffsetOperationsOrientationsArentBorked_DonutTests() {
         var outer = Polygon2.CreateCircle(0, 0, 100);
         var inner = Polygon2.CreateCircle(0, 0, 50); // Note: Don't use a small value like 5, or contour cleaning will cause this to be ignored.
         var dilate = PolygonOperations.Offset().Include(outer).Include((inner, true)).Dilate(2).Execute();
         AssertEquals(1, dilate.Children.Length);
         AssertEquals(1, dilate.Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               dilate.Children[0].Contour[0],
               dilate.Children[0].Contour[1],
               dilate.Children[0].Contour[2]));
         AssertNotEquals(dilate.Children[0].Contour[0], dilate.Children[0].Contour[^1]);

         var erode = PolygonOperations.Offset().Include(outer).Include((inner, true)).Erode(2).Execute();
         AssertEquals(1, erode.Children.Length);
         AssertEquals(1, erode.Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               erode.Children[0].Contour[0],
               erode.Children[0].Contour[1],
               erode.Children[0].Contour[2]));
         AssertNotEquals(erode.Children[0].Contour[0], erode.Children[0].Contour[^1]);
      }

      /// <summary>
      /// Four-Square / Donut Test is probably the hardest test to pass.
      /// </summary>
      [Fact]
      public void PolygonOperationsOrientationsArentBorked_FourSquareDonutTests() {
         var outer = new[] {
            Polygon2.CreateRect(0, 0, 1000, 1000),
            Polygon2.CreateRect(125, 125, 250, 250, true),
            Polygon2.CreateRect(625, 125, 250, 250, true),
            Polygon2.CreateRect(125, 625, 250, 250, true),
            Polygon2.CreateRect(625, 625, 250, 250, true),
         };
         var inner = new[] {
            Polygon2.CreateCircle(500, 500, 300),
            Polygon2.CreateCircle(500, 500, 150, true),
         }; // Note: Don't use a small value like 5, or contour cleaning will cause this to be ignored.

         // Union is a square with 4 square-intersect-circle cuts. 
         var union = PolygonOperations.Punch().Include(outer).Include(inner).Execute();
         AssertEquals(1, union.Children.Length);
         AssertEquals(4, union.Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               union.Children[0].Contour[0],
               union.Children[0].Contour[1],
               union.Children[0].Contour[2]));
         AssertNotEquals(union.Children[0].Contour[0], union.Children[0].Contour[^1]);

         // punch is 1 hole: a union of 4 squares & a circle, with a island circle inside.
         var punch = PolygonOperations.Punch().Include(outer).Exclude(inner).Execute();
         AssertEquals(1, punch.Children.Length);
         AssertEquals(1, punch.Children[0].Children.Length);
         AssertEquals(1, punch.Children[0].Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               punch.Children[0].Contour[0],
               punch.Children[0].Contour[1],
               punch.Children[0].Contour[2]));
         AssertNotEquals(punch.Children[0].Contour[0], punch.Children[0].Contour[^1]);

         // Document the behavior of union erode (which is different than union), though it's conceptually hard to reason about if not punching prior
         var erode = PolygonOperations.Offset().Include(outer).Include(inner.Map(x => (x, true))).Erode(2).Execute();
         AssertEquals(1, erode.Children.Length);
         AssertEquals(1, erode.Children[0].Children.Length);
         AssertEquals(1, erode.Children[0].Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               erode.Children[0].Contour[0],
               erode.Children[0].Contour[1],
               erode.Children[0].Contour[2]));
         AssertNotEquals(erode.Children[0].Contour[0], erode.Children[0].Contour[^1]);

         // dilate of punch works, same tree topology as punch
         var punchDilate = PolygonOperations.Offset().Include(punch.FlattenToPolygonAndIsHoles()).Dilate(50).Cleanup().Execute().Visualize();
         AssertEquals(1, punchDilate.Children.Length);
         AssertEquals(1, punchDilate.Children[0].Children.Length);
         AssertEquals(1, punchDilate.Children[0].Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               punchDilate.Children[0].Contour[0],
               punchDilate.Children[0].Contour[1],
               punchDilate.Children[0].Contour[2]));
         AssertNotEquals(punchDilate.Children[0].Contour[0], punchDilate.Children[0].Contour[^1]);

         // erode of punch works, same tree topology as punch
         var punchErode = PolygonOperations.Offset().Include(punch.FlattenToPolygonAndIsHoles()).Erode(50).Cleanup().Execute().Visualize();
         AssertEquals(1, punchErode.Children.Length);
         AssertEquals(1, punchErode.Children[0].Children.Length);
         AssertEquals(1, punchErode.Children[0].Children[0].Children.Length);
         AssertEquals(
            Clockness.ClockWise,
            GeometryOperations.Clockness(
               punchErode.Children[0].Contour[0],
               punchErode.Children[0].Contour[1],
               punchErode.Children[0].Contour[2]));
         AssertNotEquals(punchErode.Children[0].Contour[0], punchErode.Children[0].Contour[^1]);
      }
   }
}
