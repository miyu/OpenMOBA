using System;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami;
using cInt = System.Int32;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.DataStructures {
   public class AxisAlignedBoundingBox3 {
      public DoubleVector3 Center;
      public DoubleVector3 Extents;

      public bool Contains(ref DoubleVector3 point) {
         var d = point - Center;
         return CDoubleMath.Abs(d.X) < Extents.X &&
                CDoubleMath.Abs(d.Y) < Extents.Y &&
                CDoubleMath.Abs(d.Z) < Extents.Z;
      }

      public static AxisAlignedBoundingBox3 BoundingPoints(IntVector3[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = points.Length;
         cInt minX = cInt.MaxValue, minY = cInt.MaxValue, minZ = cInt.MaxValue, maxX = cInt.MinValue, maxY = cInt.MinValue, maxZ = cInt.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (points[i].X < minX) minX = points[i].X;
            if (points[i].Y < minY) minY = points[i].Y;
            if (points[i].Z < minZ) minZ = points[i].Z;
            if (maxX < points[i].X) maxX = points[i].X;
            if (maxY < points[i].Y) maxY = points[i].Y;
            if (maxZ < points[i].Z) maxZ = points[i].Z;
         }
         return FromExtents((cDouble)minX, (cDouble)minY, (cDouble)minZ, (cDouble)maxX, (cDouble)maxY, (cDouble)maxZ);
      }

      public static AxisAlignedBoundingBox3 BoundingPoints(DoubleVector3[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = points.Length;
         cDouble minX = cDouble.MaxValue, minY = cDouble.MaxValue, minZ = cDouble.MaxValue, maxX = cDouble.MinValue, maxY = cDouble.MinValue, maxZ = cDouble.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (points[i].X < minX) minX = points[i].X;
            if (points[i].Y < minY) minY = points[i].Y;
            if (points[i].Z < minZ) minZ = points[i].Z;
            if (maxX < points[i].X) maxX = points[i].X;
            if (maxY < points[i].Y) maxY = points[i].Y;
            if (maxZ < points[i].Z) maxZ = points[i].Z;
         }
         return FromExtents(minX, minY, minZ, maxX, maxY, maxZ);
      }

      public static AxisAlignedBoundingBox3 BoundingBoxes(AxisAlignedBoundingBox3 a, AxisAlignedBoundingBox3 b) {
         var minX = CDoubleMath.Min(a.Center.X - a.Extents.X, b.Center.X - b.Extents.X);
         var maxX = CDoubleMath.Max(a.Center.X + a.Extents.X, b.Center.X + b.Extents.X);

         var minY = CDoubleMath.Min(a.Center.Y - a.Extents.Y, b.Center.Y - b.Extents.Y);
         var maxY = CDoubleMath.Max(a.Center.Y + a.Extents.Y, b.Center.Y + b.Extents.Y);

         var minZ = CDoubleMath.Min(a.Center.Z - a.Extents.Z, b.Center.Z - b.Extents.Z);
         var maxZ = CDoubleMath.Max(a.Center.Z + a.Extents.Z, b.Center.Z + b.Extents.Z);

         return FromExtents(minX, minY, minZ, maxX, maxY, maxZ);
      }

      public static AxisAlignedBoundingBox3 FromExtents(DoubleVector3 mins, DoubleVector3 maxs) => FromExtents(mins.X, mins.Y, mins.Z, maxs.X, maxs.Y, maxs.Z);

      public static AxisAlignedBoundingBox3 FromExtents(cDouble minX, cDouble minY, cDouble minZ, cDouble maxX, cDouble maxY, cDouble maxZ) {
         return new AxisAlignedBoundingBox3 {
            Center = new DoubleVector3((maxX + minX) / CDoubleMath.c2, (maxY + minY) / CDoubleMath.c2, (maxZ + minZ) / CDoubleMath.c2),
            Extents = new DoubleVector3((maxX - minX) / CDoubleMath.c2, (maxY - minY) / CDoubleMath.c2, (maxZ - minZ) / CDoubleMath.c2)
         };
      }

      public bool Intersects(AxisAlignedBoundingBox3 other) {
         var lbn = Center - Extents;
         var rtf = Center + Extents;

         var otherLbn = other.Center - other.Extents;
         var otherRtf = other.Center + other.Extents;

         if (lbn.X > otherRtf.X) return false;
         if (otherLbn.X > rtf.X) return false;

         if (lbn.Y > otherRtf.Y) return false;
         if (otherLbn.Y > rtf.Y) return false;

         if (lbn.Z > otherRtf.Z) return false;
         if (otherLbn.Z > rtf.Z) return false;

         return true;
      }
   }

   // inclusive!
   public class AxisAlignedBoundingBox2 {
      public DoubleVector2 Center;
      public DoubleVector2 Extents;

      public bool Contains(in DoubleVector2 point) {
         var d = point - Center;
         return CDoubleMath.Abs(d.X) <= Extents.X &&
                CDoubleMath.Abs(d.Y) <= Extents.Y;
      }

      public AxisAlignedBoundingBox2 Offset(cDouble amount) {
         return new AxisAlignedBoundingBox2 {
            Center = Center,
            Extents = new DoubleVector2(Extents.X + amount, Extents.Y + amount)
         };
      }

      public static AxisAlignedBoundingBox2 BoundingPolygon(Polygon2 poly) {
         return BoundingPoints(poly.Points.ToArray());
      }

      public static AxisAlignedBoundingBox2 BoundingPolygonNode(PolygonNode node) {
         if (node.Contour != null) {
            return BoundingPoints(node.Contour);
         } else {
            AxisAlignedBoundingBox2 res = null;
            foreach (var child in node.Children) {
               var childBounds = BoundingPoints(child.Contour);
               res = res == null
                  ? childBounds
                  : res.Merge(childBounds);
            }
            return res;
         }
      }

      public static AxisAlignedBoundingBox2 BoundingPoints(IntVector2[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = points.Length;
         cInt minX = cInt.MaxValue, minY = cInt.MaxValue, maxX = cInt.MinValue, maxY = cInt.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (points[i].X < minX) minX = points[i].X;
            if (points[i].Y < minY) minY = points[i].Y;
            if (maxX < points[i].X) maxX = points[i].X;
            if (maxY < points[i].Y) maxY = points[i].Y;
         }
         return FromExtents((cDouble)minX, (cDouble)minY, (cDouble)maxX, (cDouble)maxY);
      }

      public static AxisAlignedBoundingBox2 BoundingPoints(DoubleVector2[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
         if (endIndexExclusive == -1) endIndexExclusive = points.Length;
         cDouble minX = cDouble.MaxValue, minY = cDouble.MaxValue, maxX = cDouble.MinValue, maxY = cDouble.MinValue;
         for (var i = startIndexInclusive; i < endIndexExclusive; i++) {
            if (points[i].X < minX) minX = points[i].X;
            if (points[i].Y < minY) minY = points[i].Y;
            if (maxX < points[i].X) maxX = points[i].X;
            if (maxY < points[i].Y) maxY = points[i].Y;
         }
         return FromExtents(minX, minY, maxX, maxY);
      }

      public static AxisAlignedBoundingBox2 BoundingBoxes(AxisAlignedBoundingBox2 a, AxisAlignedBoundingBox2 b) {
         var minX = CDoubleMath.Min(a.Center.X - a.Extents.X, b.Center.X - b.Extents.X);
         var maxX = CDoubleMath.Max(a.Center.X + a.Extents.X, b.Center.X + b.Extents.X);

         var minY = CDoubleMath.Min(a.Center.Y - a.Extents.Y, b.Center.Y - b.Extents.Y);
         var maxY = CDoubleMath.Max(a.Center.Y + a.Extents.Y, b.Center.Y + b.Extents.Y);

         return FromExtents(minX, minY, maxX, maxY);
      }

      public static AxisAlignedBoundingBox2 FromExtents(DoubleVector2 mins, DoubleVector2 maxs) => FromExtents(mins.X, mins.Y, maxs.X, maxs.Y);

      public static AxisAlignedBoundingBox2 FromExtents(cDouble minX, cDouble minY, cDouble maxX, cDouble maxY) {
         return new AxisAlignedBoundingBox2 {
            Center = new DoubleVector2((maxX + minX) / CDoubleMath.c2, (maxY + minY) / CDoubleMath.c2),
            Extents = new DoubleVector2((maxX - minX) / CDoubleMath.c2, (maxY - minY) / CDoubleMath.c2)
         };
      }

      public bool Intersects(AxisAlignedBoundingBox2 other) {
         var lbn = Center - Extents;
         var rtf = Center + Extents;

         var otherLbn = other.Center - other.Extents;
         var otherRtf = other.Center + other.Extents;

         if (lbn.X > otherRtf.X) return false;
         if (otherLbn.X > rtf.X) return false;

         if (lbn.Y > otherRtf.Y) return false;
         if (otherLbn.Y > rtf.Y) return false;

         return true;
      }

      public AxisAlignedBoundingBox2 Merge(AxisAlignedBoundingBox2 other) {
         var low1 = Center - Extents;
         var high1 = Center + Extents;
         var low2 = other.Center - other.Extents;
         var high2 = other.Center + other.Extents;

         var low = new DoubleVector2(
            Math.Min(low1.X, low2.X),
            Math.Min(low1.Y, low2.Y));

         var high = new DoubleVector2(
            Math.Max(high1.X, high2.X),
            Math.Max(high1.Y, high2.Y));

         return new AxisAlignedBoundingBox2 {
            Center = (low + high) / 2,
            Extents = (high - low) / 2,
         };
      }
   }
}
