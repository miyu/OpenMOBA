using ClipperLib;
using OpenMOBA.Geometry;
using OpenMOBA.Utilities;
using Poly2Tri;
using Poly2Tri.Triangulation.Delaunay;
using Poly2Tri.Triangulation.Polygon;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Polygon = Poly2Tri.Triangulation.Polygon.Polygon;

using cInt = System.Int64;

namespace OpenMOBA {
   // Connected Components. We avoid that term because 'Component' is pretty overloaded in meaning.
   public class TriangulationIsland {
      public Triangle[] Triangles { get; set; }
      public IntRect2 IntBounds { get; set; }
      public QuadTree<int> TriangleIndexQuadTree { get; set; }
   }

   public class Triangulation {
      public IReadOnlyList<TriangulationIsland> Islands { get; set; }
   }

   public struct Triangle {
      public const int NO_NEIGHBOR_INDEX = -1;

      public int Index;

      // in counterclockwise order
      public Array3<DoubleVector2> Points;

      public DoubleVector2 Centroid;

      // also in counterclockwise order, NO_NEIGHBOR_INDEX indicates no neighbor
      public Array3<int> NeighborOppositePointIndices;

      public IntRect2 IntPaddedBounds;
   }

   public struct Array3<T> : IReadOnlyList<T> {
      public Array3(T a, T b, T c) {
         A = a;
         B = b;
         C = c;
      }

      public T A;
      public T B;
      public T C;

      public int Count => 3;

      public T this[int index]
      {
         get
         {
            if (index == 0) return A;
            else if (index == 1) return B;
            else if (index == 2) return C;
            else throw new IndexOutOfRangeException();
         }
         set
         {
            if (index == 0) A = value;
            else if (index == 1) B = value;
            else if (index == 2) C = value;
            else throw new IndexOutOfRangeException();
         }
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public IEnumerator<T> GetEnumerator() {
         yield return A;
         yield return B;
         yield return C;
      }
   }

   public class Triangulator {
      public Triangulation Triangulate(PolyTree polyTree) {
         if (!polyTree.IsHole || polyTree.Contour.Any()) {
            throw new ArgumentException("Expected polytree to be contourless root hole!");
         }
         var islands = new List<TriangulationIsland>();
         foreach (var child in polyTree.Childs) {
            TriangulateHelper(child, islands);
         }
         return new Triangulation { Islands = islands };
      }

      private void TriangulateHelper(PolyNode node, List<TriangulationIsland> islands) {
         DebugPrint("Triangulate out");

         var cps = new Polygon(ConvertToTriangulationPoints(node.Contour));
         foreach (var hole in node.Childs) {
            cps.AddHole(new Polygon(ConvertToTriangulationPoints(hole.Contour)));
         }
         P2T.Triangulate(cps);

         var triangles = new Triangle[cps.Triangles.Count];
         var p2tTriangleToIndex = new Dictionary<DelaunayTriangle, int>();
         for (int i = 0; i < cps.Triangles.Count; i++) {
            triangles[i].Index = i;
            p2tTriangleToIndex[cps.Triangles[i]] = i;
         }

         for (var i = 0; i < cps.Triangles.Count; i++) {
            var p2tTriangle = cps.Triangles[i];
            triangles[i].Points = new Array3<DoubleVector2>(
               p2tTriangle.Points[0].ToOpenMobaPointD(),
               p2tTriangle.Points[1].ToOpenMobaPointD(),
               p2tTriangle.Points[2].ToOpenMobaPointD()
            );
            triangles[i].Centroid = (triangles[i].Points[0] + triangles[i].Points[1] + triangles[i].Points[2]) / 3.0;
            triangles[i].IntPaddedBounds = CreatePaddedIntAxisAlignedBoundingBox(ref triangles[i].Points);
            for (int j = 0; j < 3; j++) {
               if (p2tTriangle.Neighbors[j] != null && p2tTriangle.Neighbors[j].IsInterior)
                  triangles[i].NeighborOppositePointIndices[j] = p2tTriangleToIndex[p2tTriangle.Neighbors[j]];
               else
                  triangles[i].NeighborOppositePointIndices[j] = Triangle.NO_NEIGHBOR_INDEX;
#if DEBUG
               var p0 = triangles[i].Points[0];
               var p1 = triangles[i].Points[1];
               var p2 = triangles[i].Points[2];
               var centroid = triangles[i].Centroid;
               var cp0 = p0 - centroid;
               var cp1 = p1 - centroid;
               var cp2 = p2 - centroid;
               var cl01 = GeometryOperations.Clockness(cp0, cp1);
               var cl12 = GeometryOperations.Clockness(cp1, cp2);
               var cl20 = GeometryOperations.Clockness(cp2, cp0);
               var cond = cl01 == cl12 && cl12 == cl20 && cl20 == Clockness.CounterClockwise;
               if (!cond) {
                  throw new ArgumentException("P2T Triangle Clockness wasn't expected");
               }
#endif
            }
         }
         var islandBoundingBox = new IntRect2 {
            Left = triangles.Min(t => t.IntPaddedBounds.Left),
            Top = triangles.Min(t => t.IntPaddedBounds.Top),
            Right = triangles.Max(t => t.IntPaddedBounds.Right),
            Bottom = triangles.Max(t => t.IntPaddedBounds.Bottom)
         };
         var triangleIndexQuadTree = new QuadTree<int>(8, 8, islandBoundingBox);
         for (var i = 0; i < triangles.Length; i++) {
            triangleIndexQuadTree.Insert(i, triangles[i].IntPaddedBounds);
         }
         islands.Add(new TriangulationIsland {
            Triangles = triangles,
            IntBounds = islandBoundingBox,
            TriangleIndexQuadTree = triangleIndexQuadTree
         });
         foreach (var innerChild in node.Childs.SelectMany(hole => hole.Childs)) {
            TriangulateHelper(innerChild, islands);
         }
      }

      private IntRect2 CreatePaddedIntAxisAlignedBoundingBox(ref Array3<DoubleVector2> points) {
         cInt minX = cInt.MaxValue;
         cInt maxX = cInt.MinValue;
         cInt minY = cInt.MaxValue;
         cInt maxY = cInt.MinValue;
         for (int i = 0; i < points.Count; i++) {
            minX = Math.Min(minX, (cInt)Math.Floor(points[i].X) - 1);
            maxX = Math.Max(maxX, (cInt)Math.Ceiling(points[i].X) + 1);
            minY = Math.Min(minY, (cInt)Math.Floor(points[i].Y) - 1);
            maxY = Math.Max(maxY, (cInt)Math.Ceiling(points[i].Y) + 1);
         }
         return new IntRect2 {
            Left = minX,
            Top = minY,
            Right = maxX,
            Bottom = maxY
         };
      }

      private List<PolygonPoint> ConvertToTriangulationPoints(List<IntVector2> points) {
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
