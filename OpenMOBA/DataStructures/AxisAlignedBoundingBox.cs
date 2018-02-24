using OpenMOBA.Geometry;
using System;
using cInt = System.Int32;

namespace OpenMOBA.DataStructures {
   public class AxisAlignedBoundingBox {
      public DoubleVector3 Center;
      public DoubleVector3 Extents;

      public bool Contains(ref DoubleVector3 point) {
         var d = point - Center;
         return Math.Abs(d.X) < Extents.X &&
                Math.Abs(d.Y) < Extents.Y &&
                Math.Abs(d.Z) < Extents.Z;
      }

      public static AxisAlignedBoundingBox BoundingBoxes(AxisAlignedBoundingBox a, AxisAlignedBoundingBox b) {
         var minX = Math.Min(a.Center.X - a.Extents.X, b.Center.X - b.Extents.X);
         var maxX = Math.Max(a.Center.X + a.Extents.X, b.Center.X + b.Extents.X);

         var minY = Math.Min(a.Center.Y - a.Extents.Y, b.Center.Y - b.Extents.Y);
         var maxY = Math.Max(a.Center.Y + a.Extents.Y, b.Center.Y + b.Extents.Y);

         var minZ = Math.Min(a.Center.Z - a.Extents.Z, b.Center.Z - b.Extents.Z);
         var maxZ = Math.Max(a.Center.Z + a.Extents.Z, b.Center.Z + b.Extents.Z);

         return FromExtents(minX, minY, minZ, maxX, maxY, maxZ);
      }

      public static AxisAlignedBoundingBox FromExtents(DoubleVector3 mins, DoubleVector3 maxs) => FromExtents(mins.X, mins.Y, mins.Z, maxs.X, maxs.Y, maxs.Z);

      public static AxisAlignedBoundingBox FromExtents(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) {
         return new AxisAlignedBoundingBox {
            Center = new DoubleVector3((maxX + minX) / 2, (maxY + minY) / 2, (maxZ + minZ) / 2),
            Extents = new DoubleVector3((maxX - minX) / 2, (maxY - minY) / 2, (maxZ - minZ) / 2)
         };
      }
   }
}
