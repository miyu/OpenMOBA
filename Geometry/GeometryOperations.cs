using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using Poly2Tri.Triangulation.Delaunay;
using Poly2Tri.Triangulation.Polygon;

namespace OpenMOBA.Geometry {
   public static class GeometryOperations {
      public static Clockness Clockness(IntVector2 a, IntVector2 b, IntVector2 c) => Clockness(b - a, b - c);
      public static Clockness Clockness(IntVector2 ba, IntVector2 bc) => Clockness(ba.X, ba.Y, bc.X, bc.Y);
      public static Clockness Clockness(int ax, int ay, int bx, int by, int cx, int cy) => Clockness(bx - ax, by - ay, bx - cx, by - cy);
      public static Clockness Clockness(int bax, int bay, int bcx, int bcy) => (Clockness)Math.Sign(Cross(bax, bay, bcx, bcy));

      public static int Cross(IntVector2 ba, IntVector2 bc) => Cross(ba.X, ba.Y, bc.X, bc.Y);
      public static int Cross(int bax, int bay, int bcx, int bcy) => bax * bcy - bay * bcx;

      public static double Cross(double bax, double bay, double bcx, double bcy) => bax * bcy - bay * bcx;


      public static bool TryIntersect(double x, double y, IReadOnlyList<ConnectedMesh> meshes, out ConnectedMesh mesh, out DelaunayTriangle triangle) {
         foreach (var m in meshes) {
            if (m.TryIntersect(x, y, out triangle)) {
               mesh = m;
               return true;
            }
         }
         mesh = null;
         triangle = null;
         return false;
      }

      public static bool TryIntersect(this ConnectedMesh mesh, double x, double y, out DelaunayTriangle triangle) {
         if (x < mesh.BoundingBox.MinX || y < mesh.BoundingBox.MinY ||
             x > mesh.BoundingBox.MaxX || y > mesh.BoundingBox.MaxY) {
            triangle = null;
            return false;
         }
         triangle = mesh.Triangles.FirstOrDefault(tri => IsPointInTriangle(x, y, tri));
         return triangle != null;
      }

      public static bool IsPointInTriangle(double px, double py, DelaunayTriangle triangle) {
         // Barycentric coordinates for PIP w/ triangle test http://blackpawn.com/texts/pointinpoly/

         var ax = triangle.Points.Item0.X;
         var ay = triangle.Points.Item0.Y;
         var bx = triangle.Points.Item1.X;
         var by = triangle.Points.Item1.Y;
         var cx = triangle.Points.Item2.X;
         var cy = triangle.Points.Item2.Y;

         var v0x = cx - ax;
         var v0y = cy - ay;
         var v1x = bx - ax;
         var v1y = by - ay;
         var v2x = px - ax;
         var v2y = py - ay;

         var dot00 = v0x * v0x + v0y * v0y;
         var dot01 = v0x * v1x + v0y * v1y;
         var dot02 = v0x * v2x + v0y * v2y;
         var dot11 = v1x * v1x + v1y * v1y;
         var dot12 = v1x * v2x + v1y * v2y;

         var invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
         var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
         var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

         return (u >= 0) && (v >= 0) && (u + v < 1);
      }
   }
}