using OpenMOBA.Geometry;
using System;
using cInt = System.Int32;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.DataStructures {
   public class AxisAlignedBoundingBox {
      public DoubleVector3 Center;
      public DoubleVector3 Extents;

      public bool Contains(ref DoubleVector3 point) {
         var d = point - Center;
         return CDoubleMath.Abs(d.X) < Extents.X &&
                CDoubleMath.Abs(d.Y) < Extents.Y &&
                CDoubleMath.Abs(d.Z) < Extents.Z;
      }

      public static AxisAlignedBoundingBox BoundingPoints(IntVector3[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
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

      public static AxisAlignedBoundingBox BoundingPoints(DoubleVector3[] points, int startIndexInclusive = 0, int endIndexExclusive = -1) {
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

      public static AxisAlignedBoundingBox BoundingBoxes(AxisAlignedBoundingBox a, AxisAlignedBoundingBox b) {
         var minX = CDoubleMath.Min(a.Center.X - a.Extents.X, b.Center.X - b.Extents.X);
         var maxX = CDoubleMath.Max(a.Center.X + a.Extents.X, b.Center.X + b.Extents.X);

         var minY = CDoubleMath.Min(a.Center.Y - a.Extents.Y, b.Center.Y - b.Extents.Y);
         var maxY = CDoubleMath.Max(a.Center.Y + a.Extents.Y, b.Center.Y + b.Extents.Y);

         var minZ = CDoubleMath.Min(a.Center.Z - a.Extents.Z, b.Center.Z - b.Extents.Z);
         var maxZ = CDoubleMath.Max(a.Center.Z + a.Extents.Z, b.Center.Z + b.Extents.Z);

         return FromExtents(minX, minY, minZ, maxX, maxY, maxZ);
      }

      public static AxisAlignedBoundingBox FromExtents(DoubleVector3 mins, DoubleVector3 maxs) => FromExtents(mins.X, mins.Y, mins.Z, maxs.X, maxs.Y, maxs.Z);

      public static AxisAlignedBoundingBox FromExtents(cDouble minX, cDouble minY, cDouble minZ, cDouble maxX, cDouble maxY, cDouble maxZ) {
         return new AxisAlignedBoundingBox {
            Center = new DoubleVector3((maxX + minX) / CDoubleMath.c2, (maxY + minY) / CDoubleMath.c2, (maxZ + minZ) / CDoubleMath.c2),
            Extents = new DoubleVector3((maxX - minX) / CDoubleMath.c2, (maxY - minY) / CDoubleMath.c2, (maxZ - minZ) / CDoubleMath.c2)
         };
      }

      public bool Intersects(AxisAlignedBoundingBox other) {
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
}
