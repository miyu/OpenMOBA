using ClipperLib;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenMOBA.Utilities;

namespace OpenMOBA.Foundation.Visibility {
   public class VisibilityGraph {
      public VisibilityGraph(IntLineSegment2[] barriers, IntVector2[] waypoints, DistanceMatrix distances) {
         Barriers = barriers;
         Waypoints = waypoints;
         Distances = distances;
      }

      public IntLineSegment2[] Barriers { get; }
      public IntVector2[] Waypoints { get; }
      public DistanceMatrix Distances { get; }
   }

   /// <summary>
   /// Represents a lower-triangular distance matrix.
   /// M[i,j] == M[j,i] by definition.
   /// </summary>
   public struct DistanceMatrix {
      public DistanceMatrix(int sideLength, float[] storage) {
         SideLength = sideLength;
         Storage = storage;
      }

      public int SideLength { get; }
      public float[] Storage { get; }

      public float this[int row, int col]
      {
         get { return Storage[ComputeIndex(row, col)]; }
         set { Storage[ComputeIndex(row, col)] = value; }
      }

      private int ComputeIndex(int row, int col) {
         if (col > row) {
            var temp = row;
            row = col;
            col = temp;
         }
         return CountElementsFromRows(row) + col;
      }

      public static DistanceMatrix CreateZeroedToNaN(int sideLength) {
         int elementCount = CountElementsFromRows(sideLength);
         var storage = new float[elementCount];
         for (int i = 0; i < storage.Length; i++) {
            storage[i] = float.NaN;
         }
         return new DistanceMatrix(sideLength, storage);
      }

      private static int CountElementsFromRows(int row) => row * (row + 1) / 2;

      /// <summary>
      /// NumElements = NumRows * (NumRows + 1) / 2
      /// 2 NumElements = NumRows * (NumRows + 1)
      /// 2 NumElements = NumRows**2 + NumRows
      /// 0 = 1 NumRows**2 + 1 NumRows + (-2NumElements)
      /// 
      /// NumRows = (-1 +/- sqrt(1*1 - 4(1)(-2NumElements))) / (2 * 1)
      ///         = (-1 + sqrt(1 - 8 NumElements)) / 2
      /// 
      /// Take the + for the +/- (this is a parabola zero-intersect, one will
      /// result in a negative count).
      /// </summary>
      private static int CountRowsFromElements(int elements) => (-1 + (int)Math.Round(Math.Sqrt(1 + 8 * elements))) / 2;
      
      public DistanceMatrix CopyExpandedNotZeroedToNaN(int expansionFactor) {
         var previousRowCount = CountRowsFromElements(Storage.Length);
         if (CountElementsFromRows(previousRowCount) != Storage.Length) {
            throw new InvalidOperationException("Math failed.");
         }

         var newRowCount = previousRowCount + expansionFactor;
         var newStorage = new float[CountElementsFromRows(newRowCount)];
         for (var i = 0; i < Storage.Length; i++) {
            newStorage[i] = Storage[i];
         }
         return new DistanceMatrix(SideLength, newStorage);
      }
   }

   public class Path {
      public Path(IntVector2[] points, float totalDistance) {
         Points = points;
         TotalDistance = totalDistance;
      }

      public IntVector2[] Points { get; }
      public float TotalDistance { get; }
   }

   public static class VisibilityGraphOperations {
      public static VisibilityGraph CreateVisibilityGraph(Size mapDimensions, IReadOnlyList<Polygon> holePolygons) {
         var landPoly = Polygon.CreateRect(0, 0, mapDimensions.Width, mapDimensions.Height);
         var punchResult = PolygonOperations.Punch()
                                            .Include(landPoly)
                                            .Exclude(holePolygons)
                                            .Execute();

         var tempBarriers = new List<IntLineSegment2>();
         FindVisibilityObstructionSegments(punchResult, tempBarriers);
         var barriers = tempBarriers.ToArray();

         var tempWaypoints = new List<IntVector2>();
         FindWaypoints(punchResult, tempWaypoints, true);
         var waypoints = tempWaypoints.ToArray();
         
         var sideLength = waypoints.Length;
         var distances = DistanceMatrix.CreateZeroedToNaN(sideLength);
         
         for (var i = 0; i < waypoints.Length - 1; i++) {
            for (var j = i + 1; j < waypoints.Length; j++) {
               UpdateDistanceMatrix(waypoints, barriers, i, j, distances);
            }
         }

         return new VisibilityGraph(barriers, waypoints, distances);
      }

      private static void UpdateDistanceMatrix(IntVector2[] waypoints, IntLineSegment2[] barriers,  int firstWaypointIndex, int secondWaypointIndex, DistanceMatrix distances) {
         var a = waypoints[firstWaypointIndex];
         var b = waypoints[secondWaypointIndex];
         var segment = new IntLineSegment2(a, b);

         for (var i = 0; i < barriers.Length; i++) {
            if (barriers[i].Intersects(segment)) {
               distances[firstWaypointIndex, secondWaypointIndex] = float.NaN;
               return;
            }
         }

         distances[firstWaypointIndex, secondWaypointIndex] = (a - b).Norm2F();
      }

      private static void FindVisibilityObstructionSegments(PolyNode hole, List<IntLineSegment2> results) {
         if (!hole.IsHole) {
            throw new InvalidOperationException("Provided 'hole' was not a hole.");
         }

         // union all the children, who are connectable.
         foreach (var child in hole.Childs) {
            // dilation to move holes inward
            const int kDilationFactor = 10;

            // expansion to make corners hit
            const int kExpansionFactor = 5;

            var childPolygons = child.FlattenToPolygons();
            var erodedChildPolytree = PolygonOperations.Offset()
                                                       .Include(childPolygons)
                                                       .Dilate(kDilationFactor)
                                                       .Execute();

            foreach (var polygon in erodedChildPolytree.FlattenToPolygons()) {
               var pointCount = polygon.IsClosed ? polygon.Points.Count - 1 : polygon.Points.Count;

               // skip last point as it's a duplicate of the first.
               for (int i = 0; i < pointCount; i++) {
                  var a = polygon.Points[i];
                  var b = polygon.Points[(i + 1) % pointCount];

                  var dx = b.X - a.X;
                  var dy = b.Y - a.Y;
                  var mag = (int)Math.Sqrt(dx * dx + dy * dy);
                  dx = dx * kExpansionFactor / mag;
                  dy = dy * kExpansionFactor / mag;

                  var p1 = new IntVector2(a.X - dx, a.Y - dy);
                  var p2 = new IntVector2(b.X + dx, b.Y + dy);

                  results.Add(new IntLineSegment2(p1, p2));
               }
            }

            foreach (var innerChild in child.Childs) {
               FindVisibilityObstructionSegments(innerChild, results);
            }
         }
      }

      private static void FindWaypoints(PolyNode hole, List<IntVector2> results, bool isHole) {
         foreach (var child in hole.Childs) {
            var contour = child.Contour;
            var contourIsOpen = contour.First() != contour.Last();
            var pointCount = contourIsOpen ? child.Contour.Count : child.Contour.Count - 1;
            var waypointClockness = Clockness.CounterClockwise;
            for (int i = 0; i < pointCount; i++) {
               var a = contour[i].ToOpenMobaPoint();
               var b = contour[(i + 1) % pointCount].ToOpenMobaPoint();
               var c = contour[(i + 2) % pointCount].ToOpenMobaPoint();

               var clockness = GeometryOperations.Clockness(a.X, a.Y, b.X, b.Y, c.X, c.Y);
               if (clockness == waypointClockness) {
                  results.Add(b);
               }
            }

            FindWaypoints(child, results, !isHole);
         }
      }

      public static Path FindPath(this VisibilityGraph visibilityGraph, IntVector2 start, IntVector2 end) {
         return visibilityGraph.FindPath(start.X, start.Y, end.X, end.Y);
      }

      public static Path FindPath(this VisibilityGraph visibilityGraph, int sx, int sy, int ex, int ey) {
         var startNode = new IntVector2(sx, sy);
         var endNode = new IntVector2(ex, ey);

         var waypointCount = visibilityGraph.Waypoints.Length;
         var startNodeIndex = waypointCount;
         var endNodeIndex = waypointCount + 1;

         var waypoints = new IntVector2[waypointCount + 2];
         for (int i = 0; i < waypointCount; i++) {
            waypoints[i] = visibilityGraph.Waypoints[i];
         }
         waypoints[startNodeIndex] = startNode;
         waypoints[endNodeIndex] = endNode;

         var distances = visibilityGraph.Distances.CopyExpandedNotZeroedToNaN(2);
         for (int i = 0; i < waypointCount; i++) {
            UpdateDistanceMatrix(waypoints, visibilityGraph.Barriers, i, startNodeIndex, distances);
            UpdateDistanceMatrix(waypoints, visibilityGraph.Barriers, i, endNodeIndex, distances);
         }
         UpdateDistanceMatrix(waypoints, visibilityGraph.Barriers, startNodeIndex, endNodeIndex, distances);
         distances[startNodeIndex, startNodeIndex] = float.NaN;
         distances[endNodeIndex, endNodeIndex] = float.NaN;

         var q = new PriorityQueue<VisibilityGraphPathfindingNode>();
         q.Enqueue(new VisibilityGraphPathfindingNode(startNodeIndex, 0.0f, null));

         var visitedNodeIds = new HashSet<int>();
         var minDistanceByNodeId = new Dictionary<int, float> { [startNodeIndex] = 0 };

         while (q.Count != 0) {
            var node = q.Dequeue();

            if (!visitedNodeIds.Add(node.WaypointIndex)) {
               continue;
            }

            if (node.WaypointIndex == endNodeIndex) {
               var result = new List<IntVector2>();
               var current = node;
               while (current != null) {
                  result.Add(waypoints[current.WaypointIndex]);
                  current = current.Previous;
               }
               result.Reverse();
               return new Path(result.ToArray(), node.Distance);
            }

            for (int j = 0; j < waypointCount + 2; j++) {
               var distance = distances[node.WaypointIndex, j];
               if (float.IsNaN(distance)) {
                  continue;
               }

               var totalDistance = node.Distance + distance;
               float minDistance;
               if (minDistanceByNodeId.TryGetValue(j, out minDistance)) {
                  if (minDistance <= totalDistance) {
                     continue;
                  }
               }
               minDistanceByNodeId[j] = totalDistance;
               q.Enqueue(new VisibilityGraphPathfindingNode(j, totalDistance, node));
            }
         }
         return null;
      }

      public class VisibilityGraphPathfindingNode : IComparable<VisibilityGraphPathfindingNode> {
         public VisibilityGraphPathfindingNode(int waypointIndex, float distance, VisibilityGraphPathfindingNode previous) {
            WaypointIndex = waypointIndex;
            Distance = distance;
            Previous = previous;
         }

         public int WaypointIndex { get; set; }
         public float Distance { get; set; }
         public VisibilityGraphPathfindingNode Previous { get; set; }

         public int CompareTo(VisibilityGraphPathfindingNode other) => Distance.CompareTo(other.Distance);
      }
   }
}

