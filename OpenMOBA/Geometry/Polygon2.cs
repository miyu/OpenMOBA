using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace OpenMOBA.Geometry {
   public class Polygon3 {
      public Polygon3(List<IntVector3> points, bool isHole) {
         // enforce closed poly
         if (points[0] != points.Last()) {
            //            Console.WriteLine("Warn: Polygon took open (non-closed) poly");
            points.Add(points[0]);
         }

         //#if DEBUG
         //         // This test doesn't work while polyline dilate doesn't know about holeness
         //         for (var i = 0; i < points.Count - 2; i++) {
         //            var clockness = GeometryOperations.Clockness(points[i].XY, points[i + 1].XY, points[i + 2].XY);
         //            var expectedClockness = isHole ? Clockness.CounterClockwise : Clockness.Clockwise;
         //            Debug.Assert(clockness == expectedClockness);
         //         }
         //#endif

         Points = points;
         IsHole = isHole;
      }

      public List<IntVector3> Points { get; set; }
      public bool IsHole { get; set; }
      public bool IsClosed => true;

      public static Polygon3 CreateRectXY(int x, int y, int width, int height, int z) {
         var points = new List<IntVector3> {
            new IntVector3(x, y, z),
            new IntVector3(x, y + height, z),
            new IntVector3(x + width, y + height, z),
            new IntVector3(x + width, y, z),
            new IntVector3(x, y, z)
         };
         return new Polygon3(points, true);
      }
   }

   public class Polygon2 {
      public Polygon2(List<IntVector2> points, bool isHole) {
         // enforce closed poly
         if (points[0] != points.Last()) {
            //            Console.WriteLine("Warn: Polygon took open (non-closed) poly");
            points.Add(points[0]);
         }

         //#if DEBUG
         //// This test doesn't work while polyline dilate doesn't know about holeness
         //for (var i = 0; i < points.Count - 2; i++) {
         //   var clockness = GeometryOperations.Clockness(points[i].XY, points[i + 1].XY, points[i + 2].XY);
         //   var expectedClockness = isHole ? Clockness.CounterClockwise : Clockness.Clockwise;
         //   Debug.Assert(clockness == expectedClockness);
         //}
         //#endif

         Points = points;
         IsHole = isHole;
      }

      public List<IntVector2> Points { get; set; }
      public bool IsHole { get; set; }
      public bool IsClosed => true;

      public static Polygon2 CreateRect(Rectangle rectangle) => CreateRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

      public static Polygon2 CreateRect(int x, int y, int width, int height) {
         var points = new List<IntVector2> {
            new IntVector2(x, y),
            new IntVector2(x, y + height),
            new IntVector2(x + width, y + height),
            new IntVector2(x + width, y)
         };
         return new Polygon2(points, true);
      }

      public static Polygon2 CreateCircle(int x, int y, int radius, int n = 16) {
         var points = new List<IntVector2>();
         for (var i = 0; i < n; i++) {
            points.Add(new DoubleVector2(x + radius * Math.Sin(i * Math.PI * 2 / n), y + radius * Math.Cos(i * Math.PI * 2 / n)).LossyToIntVector2());
         }
         return new Polygon2(points, true);
      }
   }
}
