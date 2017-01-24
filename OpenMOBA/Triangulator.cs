using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using Poly2Tri;
using Poly2Tri.Triangulation.Delaunay;
using Poly2Tri.Triangulation.Polygon;

namespace OpenMOBA {
   // Connected Components. We avoid that term because 'Component' is pretty overloaded in meaning.
   public class TriangulationIsland {
      public List<DelaunayTriangle> Triangles { get; set; }
      public BoundingBox BoundingBox { get; set; }

      public static TriangulationIsland Create(List<DelaunayTriangle> triangles) {
         double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
         foreach (var triangle in triangles) {
            foreach (var point in triangle.Points) {
               minX = Math.Min(point.X, minX);
               minY = Math.Min(point.Y, minY);
               maxX = Math.Max(point.X, minX);
               maxY = Math.Max(point.Y, minY);
            }
         }

         return new TriangulationIsland {
            Triangles = triangles,
            BoundingBox = new BoundingBox {
               MinX = minX,
               MinY = minY,
               MaxX = maxX,
               MaxY = maxY
            }
         };
      }
   }

   public class BoundingBox {
      public double MaxY { get; set; }
      public double MaxX { get; set; }
      public double MinY { get; set; }
      public double MinX { get; set; }
   }

   public class Triangulation {
      public IReadOnlyList<DelaunayTriangle> Triangles { get; set; }
      public IReadOnlyList<TriangulationIsland> Islands { get; set; }

   }

   public class Triangulator {
      public Triangulation Triangulate(PolyTree polyTree) {
         var triangles = new List<DelaunayTriangle>();
         if (!polyTree.IsHole || polyTree.Contour.Any()) {
            throw new ArgumentException("Expected polytree to be contourless root hole!");
         }
         foreach (var child in polyTree.Childs) {
            TriangulateHelper(child, triangles);
         }

         return new Triangulation { Triangles = triangles };

         var visitedTriangles = new HashSet<DelaunayTriangle>();
         var islands = new List<TriangulationIsland>();
         foreach (var triangle in triangles) {
            if (!triangle.IsInterior) continue;
            if (!visitedTriangles.Add(triangle)) continue;

            var islandTriangles = new List<DelaunayTriangle>();
            var s = new Stack<DelaunayTriangle>();
            s.Push(triangle);
            while (s.Count > 0) {
               var current = s.Pop();
               islandTriangles.Add(current);

               foreach (var neighbor in current.Neighbors.Where(n => n != null)) {
                  if (!neighbor.IsInterior) continue;
                  if (visitedTriangles.Add(neighbor)) {
                     s.Push(neighbor);
                  }
               }
            }
            islands.Add(TriangulationIsland.Create(islandTriangles));
         }

         return new Triangulation {
            Triangles = triangles,
            Islands = islands
         };
      }

      private void TriangulateHelper(PolyNode node, List<DelaunayTriangle> results) {
         DebugPrint("Triangulate out");

         var cps = new Polygon(ConvertToTriangulationPoints(node.Contour));
         foreach (var hole in node.Childs) {
            cps.AddHole(new Polygon(ConvertToTriangulationPoints(hole.Contour)));
         }
         P2T.Triangulate(cps);
         results.AddRange(cps.Triangles);

         foreach (var innerChild in node.Childs.SelectMany(hole => hole.Childs)) {
            TriangulateHelper(innerChild, results);
         }
      }

      private List<PolygonPoint> ConvertToTriangulationPoints(List<IntPoint> points) {
         var isOpen = points[0] != points[points.Count - 1];
         var results = new List<PolygonPoint>(points.Count);
         var limit = isOpen ? points.Count : points.Count - 1;
         for (var i = 0; i < limit; i++) {
            var p = points[i];
            DebugPrint($"P: {p.X}, {p.Y}");
            results.Add(new PolygonPoint(p.X, p.Y));
         }
         DebugPrint($"Last: {points.Last().X}, {points.Last().Y}");
         return results;
      }

      private void DebugPrint(object s) {
         //         Console.WriteLine(s);
      }
   }
}
