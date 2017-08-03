using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;
using cInt = System.Int64;

namespace OpenMOBA.Foundation.Visibility {
   public struct PolyNodeVisbilityGraphTreeData {
      public Dictionary<Crossover, PolyNode> CrossoverPolyNodes;
      public Dictionary<CrossoverSnapshot, PolyNode> CrossoverSnapshotPolyNodes;
   }

   public struct PolyNodeVisbilityGraphNodeData {
      public SectorSnapshot SectorSnapshot;
      public SectorSnapshotGeometryContext SectorSnapshotGeometryContext;

      public IntVector2[] ContourWaypoints;
      public IntVector2[] AggregateContourWaypoints;
      public IntLineSegment2[] ContourAndChildHoleBarriers;
      public SectorVisibilityGraph VisibilityDistanceMatrix;
      public VisibilityPolygon[] AggregateContourWaypointVisibilityPolygons;
      public List<CrossoverSnapshot> CrossoverSnapshots;
      public List<IntLineSegment2> ErodedCrossoverSegments;
      public int[][] CrossoverSeeingWaypointIndices;
      public HashSet<CrossoverSnapshot>[] CrossoversSeenByWaypointIndices;
   }

   public class Path {
      public Path(IntVector2[] points, float totalDistance) {
         Points = points;
         TotalDistance = totalDistance;
      }

      public IntVector2[] Points { get; }
      public float TotalDistance { get; }
   }

   public static class PolyNodeVisibilityGraphExtensions {
      // Note: Holes in polytree are in reverse clockness than lands.
      private static IntVector2[] FindContourWaypoints(this PolyNode node) {
         if (node.visibilityGraphNodeData.ContourWaypoints != null) return node.visibilityGraphNodeData.ContourWaypoints;

         var results = new List<IntVector2>();
         var contour = node.Contour;
         var contourIsOpen = contour.First() != contour.Last();
         var pointCount = contourIsOpen ? node.Contour.Count : node.Contour.Count - 1;
         for (var i = 0; i < pointCount; i++) {
            var a = contour[i];
            var b = contour[(i + 1) % pointCount];
            var c = contour[(i + 2) % pointCount];

            var clockness = GeometryOperations.Clockness(a.X, a.Y, b.X, b.Y, c.X, c.Y);
            if (clockness == Clockness.CounterClockwise) results.Add(b);
         }
         return node.visibilityGraphNodeData.ContourWaypoints = results.ToArray();
      }

      public static IntLineSegment2[] FindContourAndChildHoleBarriers(this PolyNode node) {
         if (node.visibilityGraphNodeData.ContourAndChildHoleBarriers != null) return node.visibilityGraphNodeData.ContourAndChildHoleBarriers;

         // dilation to move holes inward
         const int kDilationFactor = 5;

         // expansion to make corners hit
         const int kExpansionFactor = 2;

         var nodeAndChildrenContours = new[] { node.Contour }.Concat(node.Childs.Select(c => c.Contour));
         var dilatedNodeAndChildrenPolytree = PolygonOperations.Offset()
                                                               .Include(nodeAndChildrenContours)
                                                               .Dilate(kDilationFactor)
                                                               .Execute();

         var results = new List<IntLineSegment2>();
         foreach (var polygon in dilatedNodeAndChildrenPolytree.FlattenToPolygons()) {
            var pointCount = polygon.IsClosed ? polygon.Points.Count - 1 : polygon.Points.Count;

            // skip last point as it's a duplicate of the first.
            for (var i = 0; i < pointCount; i++) {
               var a = polygon.Points[i];
               var b = polygon.Points[(i + 1) % pointCount];

               var dx = b.X - a.X;
               var dy = b.Y - a.Y;
               var mag = (long)Math.Sqrt(dx * dx + dy * dy); // normalizing on xy plane.
               dx = dx * kExpansionFactor / mag;
               dy = dy * kExpansionFactor / mag;

               var p1 = new IntVector2(a.X - dx, a.Y - dy);
               var p2 = new IntVector2(b.X + dx, b.Y + dy);

               results.Add(new IntLineSegment2(p1, p2));
            }
         }
         return node.visibilityGraphNodeData.ContourAndChildHoleBarriers = results.ToArray();
      }

      public static IntVector2[] FindAggregateContourCrossoverWaypoints(this PolyNode node) {
         if (node.visibilityGraphNodeData.AggregateContourWaypoints != null) {
            return node.visibilityGraphNodeData.AggregateContourWaypoints;
         }
         var sources = new List<IEnumerable<IntVector2>>();
         sources.Add(FindContourWaypoints(node));
         sources.Add(node.Childs.SelectMany(FindContourWaypoints));
         if (node.visibilityGraphNodeData.CrossoverSnapshots != null) {
            sources.Add(node.visibilityGraphNodeData.ErodedCrossoverSegments.SelectMany(c => c.Points));
         }
         return node.visibilityGraphNodeData.AggregateContourWaypoints = sources.SelectMany(x => x).ToArray();
      }

      public static SectorVisibilityGraph ComputeVisibilityGraph(this PolyNode landNode) {
         if (landNode.visibilityGraphNodeData.VisibilityDistanceMatrix != null) return landNode.visibilityGraphNodeData.VisibilityDistanceMatrix;
         var waypoints = FindAggregateContourCrossoverWaypoints(landNode);
         var barriers = FindContourAndChildHoleBarriers(landNode);
         return landNode.visibilityGraphNodeData.VisibilityDistanceMatrix = SectorVisibilityGraph.Construct(waypoints, barriers);
      }

      public static VisibilityPolygon[] ComputeWaypointVisibilityPolygons(this PolyNode landNode) {
         if (landNode.visibilityGraphNodeData.AggregateContourWaypointVisibilityPolygons != null) return landNode.visibilityGraphNodeData.AggregateContourWaypointVisibilityPolygons;

         var waypoints = FindAggregateContourCrossoverWaypoints(landNode);
         var barriers = FindContourAndChildHoleBarriers(landNode);
         var visibilityPolygons = waypoints.Map(waypoint => VisibilityPolygon.Create(waypoint.ToDoubleVector2(), barriers));
         return landNode.visibilityGraphNodeData.AggregateContourWaypointVisibilityPolygons = visibilityPolygons;
      }

      public static int[] ComputeCrossoverSeeingWaypoints(this PolyNode landNode, CrossoverSnapshot crossover) {
         var crossoverIndex = landNode.visibilityGraphNodeData.CrossoverSnapshots.IndexOf(crossover);
         if (crossoverIndex < 0) {
            throw new ArgumentException("Specified crossover not in land node");
         }

         if (landNode.visibilityGraphNodeData.CrossoverSeeingWaypointIndices == null) {
            landNode.visibilityGraphNodeData.CrossoverSeeingWaypointIndices = new int[landNode.visibilityGraphNodeData.CrossoverSnapshots.Count][];
         }

         if (landNode.visibilityGraphNodeData.CrossoverSeeingWaypointIndices[crossoverIndex] != null) {
            return landNode.visibilityGraphNodeData.CrossoverSeeingWaypointIndices[crossoverIndex];
         }

         var crossoverSeeingWaypoints = new List<int>();
         var waypointVisibilityPolygons = ComputeWaypointVisibilityPolygons(landNode);
         for (int waypointIndex = 0; waypointIndex < waypointVisibilityPolygons.Length; waypointIndex++) {
            var waypoint = landNode.visibilityGraphNodeData.AggregateContourWaypoints[waypointIndex];
            var waypointVisibilityPolygon = waypointVisibilityPolygons[waypointIndex];
            var waypointVisibilityPolygonBarriers = waypointVisibilityPolygon.Get();

            var erodedCrossoverSegment = landNode.visibilityGraphNodeData.ErodedCrossoverSegments[crossoverIndex];
            if (erodedCrossoverSegment.First == waypoint || erodedCrossoverSegment.Second == waypoint) {
               continue; // TODO?
            }
            var erodedCrossoverSegmentWaypointDistanceSquared = waypoint.ToDoubleVector2().To((erodedCrossoverSegment.First + erodedCrossoverSegment.Second).ToDoubleVector2() / 2.0).SquaredNorm2D();

            var segmentsIndices = waypointVisibilityPolygon.RangeStab(erodedCrossoverSegment);
            var crossoverSeen = false;
            for (var i = 0; i < segmentsIndices.Length && !crossoverSeen; i++) {
               var (rangeStartIndex, rangeEndIndex) = segmentsIndices[i];
               for (var j = rangeStartIndex; j <= rangeEndIndex; j++) {
                  if (waypointVisibilityPolygonBarriers[j].MidpointDistanceToOriginSquared >= erodedCrossoverSegmentWaypointDistanceSquared) {
                     crossoverSeen = true;
                  }
               }
            }

            if (crossoverSeen) {
               crossoverSeeingWaypoints.Add(waypointIndex);
            }
         }
         return landNode.visibilityGraphNodeData.CrossoverSeeingWaypointIndices[crossoverIndex] = crossoverSeeingWaypoints.ToArray();
      }

      public static HashSet<CrossoverSnapshot>[] ComputeCrossoversSeenByWaypoints(this PolyNode landNode) {
         if (landNode.visibilityGraphNodeData.CrossoversSeenByWaypointIndices != null) {
            return landNode.visibilityGraphNodeData.CrossoversSeenByWaypointIndices;
         }

         var waypoints = landNode.FindAggregateContourCrossoverWaypoints();
         var crossoversSeenByWaypointIndex = waypoints.Map(x => new HashSet<CrossoverSnapshot>());
         if (landNode.visibilityGraphNodeData.CrossoverSnapshots != null) {
            foreach (var crossoverSnapshot in landNode.visibilityGraphNodeData.CrossoverSnapshots) {
               var waypointIndices = landNode.ComputeCrossoverSeeingWaypoints(crossoverSnapshot);
               foreach (var waypointIndex in waypointIndices) {
                  crossoversSeenByWaypointIndex[waypointIndex].Add(crossoverSnapshot);
               }
            }
         }
         return landNode.visibilityGraphNodeData.CrossoversSeenByWaypointIndices = crossoversSeenByWaypointIndex;
      }

      //
         //         tree.PickDeepestPolynode(to, out PolyNode toPolyNode, out bool isToHole);
         //         tree.PickDeepestPolynode(from, out PolyNode fromPolyNode, out bool isFromHole);

         //      private static bool TryComputePath(double holeDilationRadius, IntVector2 from, IntVector2 to, PolyTree tree, out List<IntVector2> path) {
         //         if (isFromHole || isToHole || fromPolyNode != toPolyNode) {
         //            path = null;
         //            return false;
         //         }
         //
         //         var landNode = fromPolyNode;
         //         var barriers = FindContourAndChildHoleBarriers(landNode);
         //         ComputeVisibilityMatrix()
         //      }
      }

      public struct PathLink {
      public int PriorIndex;
      public float TotalCost;
   }

   public struct EdgeLink {
      public int NextIndex;
      public float Cost;

      public EdgeLink(int nextIndex, float cost) {
         NextIndex = nextIndex;
         Cost = cost;
      }
   }

   public struct DijkstrasIntermediate {
      public int Prior;
      public int Current;
      public float TotalCost;

      public DijkstrasIntermediate(int prior, int current, float totalCost) {
         Prior = prior;
         Current = current;
         TotalCost = totalCost;
      }
   }

   public class SectorVisibilityGraph {
      public readonly IntLineSegment2[] Barriers;
      public readonly EdgeLink[] Edges;
      public readonly Dictionary<IntVector2, int> IndicesByWaypoint;
      public readonly int[] Offsets;
      public readonly IntVector2[] Waypoints;

      private SectorVisibilityGraph(IntVector2[] waypoints, int[] offsets, EdgeLink[] edges, IntLineSegment2[] barriers) {
         Waypoints = waypoints;
         Offsets = offsets;
         Edges = edges;
         Barriers = barriers;
         IndicesByWaypoint = waypoints.Enumerate().ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
      }

      public PathLink[] Dijkstras(IntVector2[] fromWaypoints, int[] terminals = null) {
         return Dijkstras(fromWaypoints.Map(IndicesByWaypoint.Get).Map(i => new DijkstrasIntermediate(i, i, 0.0f)));
      }

      public PathLink[] Dijkstras(DijkstrasIntermediate[] nearestAndDistances, int[] terminals = null) {
         var s = new PriorityQueue<DijkstrasIntermediate>((a, b) => a.TotalCost.CompareTo(b.TotalCost));
         foreach (var nearestAndDistance in nearestAndDistances) s.Enqueue(nearestAndDistance);

         var results = Waypoints.Map(x => new PathLink { PriorIndex = -1, TotalCost = float.NaN });
         var terminalsToCompletion = terminals?.Length ?? 0;
         while (!s.IsEmpty) {
            // no-op if waypoint already visited
            var x = s.Dequeue();
            var current = x.Current;
            if (results[current].PriorIndex != -1) continue;

            // mark waypoint as visited
            var prior = x.Prior;
            var totalCost = x.TotalCost;
            results[current] = new PathLink { PriorIndex = prior, TotalCost = totalCost };
            if (terminals != null && Array.BinarySearch(terminals, current) >= 0) {
               terminalsToCompletion--;
               if (terminalsToCompletion == 0) return results;
            }

            // schedule neighbor visits.
            var offset = Offsets[current];
            var end = Offsets[current + 1];
            for (var i = offset; i < end; i++) {
               var t = Edges[i];
               var next = t.NextIndex;
               var currentToNextDistance = t.Cost;
               if (results[next].PriorIndex != -1) continue;
               s.Enqueue(new DijkstrasIntermediate(current, next, totalCost + currentToNextDistance));
            }
         }
         return results;
      }

      public static SectorVisibilityGraph Construct(IntVector2[] waypoints, IntLineSegment2[] barriers) {
         var neighborsToCosts = new SortedDictionary<int, float>[waypoints.Length];
         for (var i = 0; i < waypoints.Length; i++) neighborsToCosts[i] = new SortedDictionary<int, float>();
         for (var i = 0; i < waypoints.Length - 1; i++) {
            var a = waypoints[i];
            for (var j = i + 1; j < waypoints.Length; j++) {
               var b = waypoints[j];
               var query = new IntLineSegment2(a, b);
               if (!barriers.Any(query.Intersects)) {
                  var cost = a.To(b).Norm2F();
                  neighborsToCosts[i][j] = neighborsToCosts[j][i] = cost;
               }
            }
         }
         var offsets = new int[waypoints.Length + 1];
         var edgeCount = neighborsToCosts.Sum(dict => dict.Count);
         var edges = new EdgeLink[edgeCount];
         var edgeIndex = 0;
         for (var i = 0; i < waypoints.Length; i++) {
            offsets[i] = edgeIndex;
            foreach (var edge in neighborsToCosts[i]) {
               edges[edgeIndex] = new EdgeLink(edge.Key, edge.Value);
               edgeIndex++;
            }
         }
         offsets[waypoints.Length] = edgeIndex;
         return new SectorVisibilityGraph(waypoints, offsets, edges, barriers);
      }
   }

   /// <summary>
   ///    Represents a mirrored triangular distance matrix.
   ///    M[i,j] == M[j,i] by definition.
   /// </summary>
   public class DistanceMatrix {
      public DistanceMatrix(int sideLength, float[] storage) {
         SideLength = sideLength;
         Storage = storage;
      }

      public int SideLength { get; }
      public float[] Storage { get; }

      public float this[int row, int col] { get => Storage[ComputeIndex(row, col)]; set => Storage[ComputeIndex(row, col)] = value; }

      private int ComputeIndex(int row, int col) {
         if (col > row) {
            var temp = row;
            row = col;
            col = temp;
         }
         return CountElementsFromRows(row) + col;
      }

      public static DistanceMatrix CreateZeroed(int sideLength) {
         var elementCount = CountElementsFromRows(sideLength);
         var storage = new float[elementCount];
         return new DistanceMatrix(sideLength, storage);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static int CountElementsFromRows(int row) {
         return row * (row + 1) / 2;
      }

      /// <summary>
      ///    NumElements = NumRows * (NumRows + 1) / 2
      ///    2 NumElements = NumRows * (NumRows + 1)
      ///    2 NumElements = NumRows**2 + NumRows
      ///    0 = 1 NumRows**2 + 1 NumRows + (-2NumElements)
      ///    NumRows = (-1 +/- sqrt(1*1 - 4(1)(-2NumElements))) / (2 * 1)
      ///    = (-1 + sqrt(1 - 8 NumElements)) / 2
      ///    Take the + for the +/- (this is a parabola zero-intersect, one will
      ///    result in a negative count).
      /// </summary>
      private static int CountRowsFromElements(int elements) {
         return (-1 + (int)Math.Round(Math.Sqrt(1 + 8 * elements))) / 2;
      }

      public DistanceMatrix CopyExpandedNotZeroedToNaN(int expansionFactor) {
         var previousRowCount = CountRowsFromElements(Storage.Length);
         if (CountElementsFromRows(previousRowCount) != Storage.Length) throw new InvalidOperationException("Math failed.");

         var newRowCount = previousRowCount + expansionFactor;
         var newStorage = new float[CountElementsFromRows(newRowCount)];
         for (var i = 0; i < Storage.Length; i++) newStorage[i] = Storage[i];
         return new DistanceMatrix(SideLength, newStorage);
      }
   }
}
