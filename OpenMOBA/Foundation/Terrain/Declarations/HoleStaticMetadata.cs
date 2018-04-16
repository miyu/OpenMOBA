using System;
using System.Linq;
using System.Numerics;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Declarations {
   public static class HoleStaticMetadata {
      public static IHoleStaticMetadata CreateRectangleHoleMetadata(int x, int y, int width, int height, double rotation) {
         var contour = Polygon2.CreateRect(-width / 2, -height / 2, width, height).Points;
         var transform = Matrix3x2.CreateRotation((float)rotation);
         contour = contour.Map(p => Vector2.Transform(p.ToDoubleVector2().ToDotNetVector(), transform).ToOpenMobaVector().LossyToIntVector2())
                          .Map(p => p + new IntVector2(x, y))
                          .ToList();

         var bounds = IntRect2.BoundingPoints(contour.ToArray()).ToDotNetRectangle();

         return new PrismHoleStaticMetadata(bounds, new[] { new Polygon2(contour) });
      }
   }
}