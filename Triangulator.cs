using ClipperLib;
using Poly2Tri;
using Poly2Tri.Triangulation.Delaunay;
using Poly2Tri.Triangulation.Polygon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenMOBA {
   public class ConnectedMesh {
      public List<DelaunayTriangle> Triangles { get; set; }
      public BoundingBox BoundingBox { get; set; }

      public static ConnectedMesh Create(List<DelaunayTriangle> triangles) {
         double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = Double.MinValue;
         foreach (var triangle in triangles) {
            foreach (var point in triangle.Points) {
               minX = Math.Min(point.X, minX);
               minY = Math.Min(point.Y, minY);
               maxX = Math.Max(point.X, minX);
               maxY = Math.Max(point.Y, minY);
            }
         }

         return new ConnectedMesh {
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

   public class Triangulator {
      public IReadOnlyList<ConnectedMesh> Triangulate(PolyTree polyTree) {
         var triangles = new List<DelaunayTriangle>();
         TriangulateHelper(polyTree, triangles);

         var visitedTriangles = new HashSet<DelaunayTriangle>();
         var meshes = new List<ConnectedMesh>();
         foreach (var triangle in triangles) {
            if (!triangle.IsInterior) continue;
            if (!visitedTriangles.Add(triangle)) continue;

            var connectedTriangles = new List<DelaunayTriangle>();
            var s = new Stack<DelaunayTriangle>();
            s.Push(triangle);
            while (s.Count > 0) {
               var current = s.Pop();
               connectedTriangles.Add(current);

               foreach (var neighbor in current.Neighbors.Where(n => n != null)) {
                  if (!neighbor.IsInterior) continue;
                  if (visitedTriangles.Add(neighbor)) {
                     s.Push(neighbor);
                  }
               }
            }
            meshes.Add(ConnectedMesh.Create(connectedTriangles));
         }
         return meshes;
      }

      private void TriangulateHelper(PolyNode node, List<DelaunayTriangle> results) {
         if (node.IsHole) {
            DebugPrint("Warning: Node was unexpectedly a hole.");
            foreach (var child in node.Childs) {
               DebugPrint(child.Parent == node);
               TriangulateHelper(child, results);
            }
            return;
         } else {
            DebugPrint("Good: Node was not a hole.");
         }

         if (node.Contour.Count > 0) {
            DebugPrint("Triangulate out");
            var cps = new Polygon(ConvertToTriangulationPoints(node.Contour, node.IsOpen));
            foreach (var child in node.Childs) {
               DebugPrint(child.Parent == node);
               DebugPrint("Triangulate hole");
               cps.AddHole(new Polygon(ConvertToTriangulationPoints(child.Contour, child.IsOpen)));

               foreach (var innerChild in child.Childs) {
                  DebugPrint("Go inner");
                  DebugPrint(innerChild.Parent == child);
                  TriangulateHelper(innerChild, results);
               }
            }
            P2T.Triangulate(cps);
            results.AddRange(cps.Triangles);
         }
      }

      private List<PolygonPoint> ConvertToTriangulationPoints(List<IntPoint> points, bool isOpen) {
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
