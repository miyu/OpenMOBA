using ClipperLib;
using OpenMOBA;
using Poly2Tri.Triangulation.Delaunay;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Forms;
using Poly2Tri.Triangulation;
using Poly2Tri.Triangulation.Polygon;
using Poly2Tri.Utility;
using Priority_Queue;

namespace ConsoleApplication {
   public class Program {
      public static void Main(string[] args) {
         var worldRectangle = Polygon.CreateRect(0, 0, 1000, 1000);
         var holeA = Polygon.CreateRect(100, 100, 300, 300);
         var holeB = Polygon.CreateRect(400, 200, 100, 100);
         var holeC = Polygon.CreateRect(200, -50, 100, 150);
         var holeD = Polygon.CreateRect(600, 600, 300, 300);
         var holeE = Polygon.CreateRect(700, 500, 100, 100);
         var holeF = Polygon.CreateRect(200, 700, 100, 100);
         var donutA = Polygon.CreateRect(600, 100, 300, 50);
         var donutB = Polygon.CreateRect(600, 150, 50, 200);
         var donutC = Polygon.CreateRect(850, 150, 50, 200);
         var donutD = Polygon.CreateRect(600, 350, 300, 50);
         var donutE = Polygon.CreateRect(700, 200, 100, 100);
         var holes = new[] { holeA, holeB, holeC, holeD, holeE, holeF, donutA, donutB, donutC, donutD, donutE };
         var punchResult = GeometryOperations.Punch()
                                             .Include(worldRectangle)
                                             .Exclude(holes)
                                             .Execute();
         var punchResultPolygons = punchResult.FlattenToPolygons();
         var display = GeometryDisplay.CreateShow();
         display.DrawPolygons(punchResultPolygons);

         var triangulator = new Triangulator();
         var meshes = triangulator.Triangulate(punchResult);
         Console.WriteLine(meshes.Count);

//         var holesUnionResult = GeometryOperations.CleanPolygons(
//            GeometryOperations.Union()
//                              .Include(holes)
//                              .Execute().FlattenToPolygons());
//         display.DrawPolygons(holesUnionResult.FlattenToPolygons());

         Visibility(display, punchResult);
//         var display = GeometryDisplay.CreateShow();
//         display.DrawMeshes(meshes);
//         Path(display, meshes, 60, 40, 930, 300);
      }

      /// <summary>
      /// This is sort of confusing.
      /// In the provided polynode, outers represent holes, holes represent holes in holes (land).
      /// In other words, the polygon provided contains outlines of holes.
      /// </summary>
      /// <param name="geometryDisplay"></param>
      /// <param name="hole"></param>
      private static void Visibility(GeometryDisplay display, PolyNode hole) {
         List<LineSegment> visibilityBarriers = new List<LineSegment>();
         FindVisibilityObstructionSegments(hole, visibilityBarriers);
         foreach (var visibilityBarrier in visibilityBarriers) {
            display.DrawLine(visibilityBarrier.X1, visibilityBarrier.Y1, visibilityBarrier.X2, visibilityBarrier.Y2, Color.Goldenrod, 1);
         }

         List<IntPoint> waypoints = new List<IntPoint>();
         FindWaypoints(hole, waypoints, true);
         foreach (var waypoint in waypoints) {
            display.DrawPoint(waypoint.X, waypoint.Y, Brushes.Red);
         }

         for (int i = 0; i < waypoints.Count - 1; i++) {
            for (int j = i + 1; j < waypoints.Count; j++) {
               var query = new LineSegment {
                  X1 = (int)waypoints[i].X,
                  Y1 = (int)waypoints[i].Y,
                  X2 = (int)waypoints[j].X,
                  Y2 = (int)waypoints[j].Y
               };
               
               if (visibilityBarriers.Any(vb => vb.Intersects(query))) {
                  continue;
               }

               display.DrawLine(query.X1, query.Y1, query.X2, query.Y2, Color.Cyan, 1);
            }
         }
      }

      public class LineSegment {
         public int X1 { get; set; }
         public int Y1 { get; set; }

         public int X2 { get; set; }
         public int Y2 { get; set; }

         public bool Intersects(LineSegment other) {
            int ax = X1, ay = Y1, bx = X2, by = Y2;
            int cx = other.X1, cy = other.Y1, dx = other.X2, dy = other.Y2;

            // http://stackoverflow.com/questions/3838329/how-can-i-check-if-two-segments-intersect
            var tl = Math.Sign((ax - cx) * (by - cy) - (ay - cy) * (bx - cx));
            var tr = Math.Sign((ax - dx) * (by - dy) - (ay - dy) * (bx - dx));
            var bl = Math.Sign((cx - ax) * (dy - ay) - (cy - ay) * (dx - ax));
            var br = Math.Sign((cx - bx) * (dy - by) - (cy - by) * (dx - bx));

            return tl == -tr && bl == -br;
         }
      }

      private static void FindVisibilityObstructionSegments(PolyNode hole, List<LineSegment> results) {
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
            var erodedChildPolytree = GeometryOperations.Offset()
                                                        .Include(childPolygons)
                                                        .Dilate(10)
                                                        .Execute();

            foreach (var polygon in erodedChildPolytree.FlattenToPolygons()) {
               foreach (var pair in polygon.Points.Zip(polygon.Points.Skip(1), Tuple.Create)) {
                  var x1 = (int)pair.Item1.X;
                  var y1 = (int)pair.Item1.Y;
                  var x2 = (int)pair.Item2.X;
                  var y2 = (int)pair.Item2.Y;

                  var dx = (float)(x2 - x1);
                  var dy = (float)(y2 - y1);
                  var mag = (float)Math.Sqrt(dx * dx + dy * dy);
                  dx = dx * kExpansionFactor / mag;
                  dy = dy * kExpansionFactor / mag;

                  results.Add(new LineSegment {
                     X1 = (int)(x1 - dx),
                     Y1 = (int)(y1 - dy),
                     X2 = (int)(x2 + dx),
                     Y2 = (int)(y2 + dy)
                  });
               }
            }

            foreach (var innerChild in child.Childs) {
               FindVisibilityObstructionSegments(innerChild, results);
            }
         }
      }

      private static void FindWaypoints(PolyNode hole, List<IntPoint> results, bool isHole) {
         foreach (var child in hole.Childs) {
            if (child.Contour.First() != child.Contour.Last()) {
               throw new InvalidOperationException("Expected closed");
            }

            var contour = child.Contour;
            var contourCount = child.Contour.Count - 1; // closed poly duplicates first vertex
            var waypointClockness = COUNTERCLOCKWISE;
            for (int i = 0; i < contourCount; i++) {
               var a = contour[i];
               var b = contour[(i + 1) % contourCount];
               var c = contour[(i + 2) % contourCount];

               var clockness = Clockness(a.X, a.Y, b.X, b.Y, c.X, c.Y);
               if (clockness == waypointClockness) {
                  results.Add(b);
               }
            }

            FindWaypoints(child, results, !isHole);
         }
      }

      private static void Path(GeometryDisplay display, IReadOnlyList<ConnectedMesh> meshes, double sx, double sy, double ex, double ey) {
         display.DrawPoint(sx, sy);
         display.DrawPoint(ex, ey);

         ConnectedMesh startMesh;
         DelaunayTriangle startTriangle;
         if (!GeometryOperations.TryIntersect(sx, sy, meshes, out startMesh, out startTriangle)) return;
         display.DrawTriangle(startTriangle);

         ConnectedMesh endMesh;
         DelaunayTriangle endTriangle;
         if (!GeometryOperations.TryIntersect(ex, ey, meshes, out endMesh, out endTriangle)) return;
         display.DrawTriangle(endTriangle);

         if (startMesh != endMesh) return;
         var mesh = startMesh;

         var trianglePath = AStar(startMesh, startTriangle, endTriangle);
         foreach (var triangle in trianglePath) {
            display.DrawTriangle(triangle);
         }

         var funnelEdges = FunnelEdges(display, trianglePath, ex, ey);
         var path = new List<TriangulationPoint>();
         FunnelAlgorithm(display, sx, sy, ex, ey, funnelEdges, 0, path);

         foreach (var segment in path.Zip(path.Skip(1), Tuple.Create)) {
            display.DrawLine(segment.Item1.X, segment.Item1.Y, segment.Item2.X, segment.Item2.Y, Color.Red);
         }
      }

      private static void FunnelAlgorithm(GeometryDisplay display, double sx, double sy, double ex, double ey, List<FunnelEdge> edges, int edgesStartIndex, List<TriangulationPoint> path) {
         var apex = new TriangulationPoint(sx, sy);
         path.Add(apex);
         display.DrawPoint(apex.X, apex.Y, Brushes.Red);
         Console.WriteLine("Enter at apex " + apex + " ESI " + edgesStartIndex);

         // advance extesStartIndex until the corresponding edge does not share a point with the apex.
         while (edgesStartIndex < edges.Count && (apex.Equals(edges[edgesStartIndex].Left) || apex.Equals(edges[edgesStartIndex].Right))) {
            Console.WriteLine("Skip edge sharing apex: " + edges[edgesStartIndex].Left + " " + edges[edgesStartIndex].Right);
            edgesStartIndex++;
         }


         if (edges.Count == edgesStartIndex) {
            goto terminatePath;
         }

         var currentLeft = edges[edgesStartIndex].Left;
         var currentRight = edges[edgesStartIndex].Right;
         Console.WriteLine("Final edge: " + currentLeft + " " + currentRight + " ESI " + edgesStartIndex);
         var lastEdgeIndex = edgesStartIndex;

         display.DrawLine(apex.X, apex.Y, currentLeft.X, currentLeft.Y, Color.Lime);
         display.DrawLine(apex.X, apex.Y, currentRight.X, currentRight.Y, Color.Orange);

         display.DrawLine(currentLeft.X, currentLeft.Y, currentRight.X, currentRight.Y, Color.Goldenrod);

         var edge = edges[edgesStartIndex];
         var lastCross = UnitCross(edge.Left, apex, edge.Right);
         var lastAngle = Angle(edge.Left, apex, edge.Right) * 180 / Math.PI;
         int lastLeftAdvancedEdgeIndex = edgesStartIndex;
         int lastRightAdvancedEdgeIndex = edgesStartIndex;
         while (true) {
            if (lastCross < 0) {
               throw new InvalidOperationException("lastCross was negative??");
            }

            var nextEdgeIndex = lastEdgeIndex + 1;
            bool isLeftAdvancement;
            TriangulationPoint nextPoint;
            if (nextEdgeIndex == edges.Count) {
               goto terminatePath;
            } else {
               var lastEdge = edges[lastEdgeIndex];
               var nextEdge = edges[nextEdgeIndex];
               if (ReferenceEquals(lastEdge.Left, nextEdge.Left)) {
                  isLeftAdvancement = false;
                  nextPoint = nextEdge.Right;
               } else {
                  isLeftAdvancement = true;
                  nextPoint = nextEdge.Left;
               }
            }

            var nextCross = isLeftAdvancement ? UnitCross(nextPoint, apex, currentRight) : UnitCross(currentLeft, apex, nextPoint);
            var nextAngle = isLeftAdvancement ? Angle(nextPoint, apex, currentRight) : Angle(currentLeft, apex, nextPoint);
            nextAngle *= 180 / Math.PI;

            Console.WriteLine("NP: " + nextPoint.X + " " + nextPoint.Y +  " Last: " + lastCross + " (" + lastAngle + "); Next: " + nextCross + " (" + nextAngle + ")");

            if (nextCross < 0) {
               var minLastAdvancedEdgeIndex = Math.Min(lastLeftAdvancedEdgeIndex, lastRightAdvancedEdgeIndex);
               Console.WriteLine("Recurse at lAEI " + minLastAdvancedEdgeIndex);
               if (isLeftAdvancement) {
                  display.DrawLine(apex.X, apex.Y, currentRight.X, currentRight.Y, Color.Red);
                  FunnelAlgorithm(display, currentRight.X, currentRight.Y, ex, ey, edges, minLastAdvancedEdgeIndex, path);
               } else {
                  display.DrawLine(apex.X, apex.Y, currentLeft.X, currentLeft.Y, Color.Red);
                  FunnelAlgorithm(display, currentLeft.X, currentLeft.Y, ex, ey, edges, minLastAdvancedEdgeIndex, path);
               }
               return;
            } else if (nextAngle < lastAngle) {
               if (isLeftAdvancement) {
                  currentLeft = nextPoint;
               } else {
                  currentRight = nextPoint;
               }
               display.DrawLine(currentLeft.X, currentLeft.Y, currentRight.X, currentRight.Y, Color.Goldenrod);
               lastCross = nextCross;
               lastAngle = nextAngle;
               lastLeftAdvancedEdgeIndex = nextEdgeIndex;
               lastRightAdvancedEdgeIndex = nextEdgeIndex;
            } else {
               if (isLeftAdvancement) {
                  lastLeftAdvancedEdgeIndex = nextEdgeIndex;
               } else {
                  lastRightAdvancedEdgeIndex = nextEdgeIndex;
               }
            }
            lastEdgeIndex = nextEdgeIndex;
         }

      terminatePath:
         var terminalPoint = new TriangulationPoint(ex, ey);
         if (!path.Last().Equals(terminalPoint)) {
            path.Add(terminalPoint);
         }
      }

      private static List<FunnelEdge> FunnelEdges(GeometryDisplay display, List<DelaunayTriangle> path, double terminalX, double terminalY) {
         var edges = new List<FunnelEdge>(path.Count - 1);
         for (int i = 0; i < path.Count - 1; i++) {
            var first = path[i];
            var second = path[i + 1];

            var excludedVertexIndex = FindSecondTriangleUniqueVertexIndex(first, second);
            var right = second.Points[(excludedVertexIndex + 1) % 3];
            var left = second.Points[(excludedVertexIndex + 2) % 3];
            var s = first.Centroid();
            if (Clockness(right, s, left) != CLOCKWISE) {
               var temp = right;
               right = left;
               left = temp;
            }
            edges.Add(new FunnelEdge { Left = left, Right = right });
            display.DrawPoint(right.X, right.Y, Brushes.Blue);
            display.DrawPoint(left.X, left.Y, Brushes.Lime);
            display.DrawText((left.X + right.X) / 2 - 20, (left.Y + right.Y) / 2 - 10, $"{i}");
         }

         // add terminal edge consisting of end point and one of the prior edge's nodes.
         var terminalPoint = new TriangulationPoint(terminalX, terminalY);
         edges.Add(new FunnelEdge {
            Left = edges.Last().Left,
            Right = terminalPoint
         });

         // add terminal edge consisting of end point solely
         edges.Add(new FunnelEdge {
            Left = terminalPoint,
            Right = terminalPoint
         });
         return edges;
      }

      public class FunnelEdge {
         public TriangulationPoint Left { get; set; }
         public TriangulationPoint Right { get; set; }
      }

      const int CLOCKWISE = -1;
      const int COUNTERCLOCKWISE = 1;

      private static int Clockness(TriangulationPoint a, TriangulationPoint b, TriangulationPoint c) {
         return Clockness(a.X, a.Y, b.X, b.Y, c.X, c.Y);
      }

      private static int Clockness(double ax, double ay, double bx, double by, double cx, double cy) {
         return Math.Sign(Cross(ax, ay, bx, by, cx, cy));
      }

      private static double Cross(TriangulationPoint a, TriangulationPoint b, TriangulationPoint c) {
         return Cross(a.X, a.Y, b.X, b.Y, c.X, c.Y);
      }


      private static double Cross(double ax, double ay, double bx, double by, double cx, double cy) {
         var bax = ax - bx;
         var bay = ay - by;
         var bcx = cx - bx;
         var bcy = cy - by;

         return Cross(bax, bay, bcx, bcy);
      }

      private static double UnitCross(TriangulationPoint a, TriangulationPoint b, TriangulationPoint c) {
         return UnitCross(a.X, a.Y, b.X, b.Y, c.X, c.Y);
      }


      private static double UnitCross(double ax, double ay, double bx, double by, double cx, double cy) {
         var bax = ax - bx;
         var bay = ay - by;
         var baMagnitude = Math.Sqrt(bax * bax + bay * bay);

         var bcx = cx - bx;
         var bcy = cy - by;
         var bcMagnitude = Math.Sqrt(bcx * bcx + bcy * bcy);

         return Cross(bax / baMagnitude, bay / baMagnitude, bcx / bcMagnitude, bcy / bcMagnitude);
      }

      private static double Cross(double bax, double bay, double bcx, double bcy) {
         return bax * bcy - bay * bcx;
      }

      private static double Angle(TriangulationPoint a, TriangulationPoint b, TriangulationPoint c) {
         return Angle(a.X, a.Y, b.X, b.Y, c.X, c.Y);
      }


      private static double Angle(double ax, double ay, double bx, double by, double cx, double cy) {
         var bax = ax - bx;
         var bay = ay - by;
         var baMagnitude = Math.Sqrt(bax * bax + bay * bay);

         var bcx = cx - bx;
         var bcy = cy - by;
         var bcMagnitude = Math.Sqrt(bcx * bcx + bcy * bcy);

         return Math.Acos(Dot(bax / baMagnitude, bay / baMagnitude, bcx / bcMagnitude, bcy / bcMagnitude));
      }

      private static double Dot(double bax, double bay, double bcx, double bcy) {
         return bax * bcx + bay * bcy;
      }

      private static int FindSecondTriangleUniqueVertexIndex(DelaunayTriangle a, DelaunayTriangle b) {
         if (b.Points.Item0 != a.Points.Item0 && b.Points.Item0 != a.Points.Item1 && b.Points.Item0 != a.Points.Item2) {
            return 0;
         } else if (b.Points.Item1 != a.Points.Item0 && b.Points.Item1 != a.Points.Item1 && b.Points.Item1 != a.Points.Item2) {
            return 1;
         } else {
            return 2;
         }
      }

      private static List<DelaunayTriangle> AStar(ConnectedMesh mesh, DelaunayTriangle start, DelaunayTriangle end) {
         var visited = new HashSet<DelaunayTriangle>();
         var q = new FastPriorityQueue<AStarNode>(Int16.MaxValue);
         q.Enqueue(new AStarNode { Triangle = start }, 0);

         var minTriangleDistance = new Dictionary<DelaunayTriangle, float>();

         while (q.Any()) {
            var current = q.Dequeue();

            // If the triangle has been visited, a shorter path has already gotten here.
            if (!visited.Add(current.Triangle)) {
               continue;
            }

            if (current.Triangle == end) {
               List<DelaunayTriangle> result = new List<DelaunayTriangle>();
               while (current != null) {
                  result.Add(current.Triangle);
                  current = current.Parent;
               }
               result.Reverse();
               return result;
            }

            foreach (var neighbor in current.Triangle.Neighbors.Where(n => n != null && n.IsInterior)) {
               if (visited.Contains(neighbor)) continue;

               var currentCentroid = current.Triangle.Centroid();
               var neighborCentroid = neighbor.Centroid();
               var distance = (float)Math.Sqrt(Math.Pow(currentCentroid.X - neighborCentroid.X, 2) + Math.Pow(currentCentroid.Y - neighborCentroid.Y, 2));

               float minDistance;
               if (minTriangleDistance.TryGetValue(neighbor, out minDistance)) {
                  if (minDistance < distance) {
                     continue;
                  }
               }

               minTriangleDistance[neighbor] = distance;
               q.Enqueue(new AStarNode { Parent = current, Triangle = neighbor }, current.Priority + distance);
            }
         }
         throw new InvalidOperationException();
      }

      private class AStarNode : FastPriorityQueueNode {
         public AStarNode Parent { get; set; }
         public DelaunayTriangle Triangle { get; set; }
      }
   }

   public class GeometryDisplay {
      private readonly object synchronization = new object();
      private readonly Bitmap bitmap;
      private readonly PictureBox pb;
      private readonly Point drawPadding;
      private readonly Size displaySize;

      public GeometryDisplay(Size inputDisplaySize = default(Size), Point inputDrawPadding = new Point()) {
         this.displaySize = inputDisplaySize == default(Size) ? new Size(1025, 1025) : inputDisplaySize;
         this.drawPadding = inputDrawPadding == default(Point) ? new Point(100, 100) : inputDrawPadding;

         var paddedSize = new Size(displaySize.Width + 2 * drawPadding.X, displaySize.Height + 2 * drawPadding.Y);
         Form = new Form { ClientSize = paddedSize };
         pb = new PictureBox { Size = paddedSize };
         Form.Controls.Add(pb);

         bitmap = new Bitmap(paddedSize.Width, paddedSize.Height);
      }

      public Form Form { get; set; }

      public void DrawPolygon(Polygon polygon, bool alternateColors) {
         var color = alternateColors ? (polygon.IsHole ? Color.Lime : Color.DarkGoldenrod) : (polygon.IsHole ? Color.Red : Color.Black);
         DrawPolygon(polygon.Points, color);
      }

      public void DrawPolygons(IReadOnlyList<Polygon> polygons, bool alternateColors = false) {
         foreach (var polygon in polygons) {
            DrawPolygon(polygon, alternateColors);
         }
      }

      public void DrawPolygon(List<IntPoint> polygon, Color color) {
         lock (synchronization) {
            using (var pen = new Pen(color))
            using (var g = Graphics.FromImage(bitmap)) {
               for (var i = 0; i < polygon.Count; i++) {
                  var a = polygon[i];
                  var b = polygon[(i + 1) % polygon.Count];
                  g.DrawLine(pen, a.X + drawPadding.X, a.Y + drawPadding.Y, b.X + drawPadding.X, b.Y + drawPadding.Y);

                  if (i != polygon.Count - 1) {
                     g.DrawString($"{i}", Form.Font, Brushes.Black, a.X + drawPadding.X, a.Y + drawPadding.Y);
                  }
               }
            }
            UpdateDisplay();
         }
      }

      public void DrawText(double x, double y, string text) {
         lock (synchronization) {
            using (var g = Graphics.FromImage(bitmap)) {
               g.DrawString(text, Form.Font, Brushes.Black, (float)x + drawPadding.X, (float)y + drawPadding.Y);
            }
            UpdateDisplay();
         }
      }

      private void UpdateDisplay() {
         var displayBitmap = (Image)bitmap.Clone();

         Form.BeginInvoke(new Action(() => {
            lock (synchronization) {
               pb.Image = displayBitmap;
            }
         }));
      }

      public static GeometryDisplay CreateShow(Size displaySize = default(Size)) {
         var display = new GeometryDisplay(displaySize);
         var shownLatch = new ManualResetEvent(false);
         display.Form.Shown += (s, e) => shownLatch.Set();
         new Thread(() => {
            Application.Run(display.Form);
         }) { ApartmentState = ApartmentState.STA }.Start();
         shownLatch.WaitOne();
         return display;
      }

      public void DrawMesh(ConnectedMesh mesh) {
         lock (synchronization) {
            using (var g = Graphics.FromImage(bitmap)) {
               foreach (var triangle in mesh.Triangles) {
                  if (!triangle.IsInterior) {
                     continue;
                  }
                  
                  DrawTriangleInternal(g, triangle, triangle.IsInterior ? Pens.Cyan : Pens.Red);

                  foreach (var neighbor in triangle.Neighbors.Where(n => n != null)) {
                     if (!neighbor.IsInterior) {
                        continue;
                     }

                     g.DrawLine(
                        Pens.Gray,
                        triangle.Centroid().Xf + drawPadding.X,
                        triangle.Centroid().Yf + drawPadding.Y,
                        neighbor.Centroid().Xf + drawPadding.X,
                        neighbor.Centroid().Yf + drawPadding.Y);
                  }
               }
            }
            UpdateDisplay();
         }
      }

      public void DrawTriangle(DelaunayTriangle triangle) {
         lock (synchronization) {
            using (var pen = new Pen(Color.Purple, 5))
            using (var g = Graphics.FromImage(bitmap)) {
               DrawTriangleInternal(g, triangle, pen);
            }
            UpdateDisplay();
         }
      }

      private void DrawTriangleInternal(Graphics g, DelaunayTriangle triangle, Pen pen) {
         for (var i = 0; i < 3; i++) {
            g.DrawLine(
               pen,
               triangle.Points[i].Xf + drawPadding.X,
               triangle.Points[i].Yf + drawPadding.Y,
               triangle.Points[(i + 1) % 3].Xf + drawPadding.X,
               triangle.Points[(i + 1) % 3].Yf + drawPadding.Y);
         }
      }

      public void DrawLine(double sx, double sy, double ex, double ey, Color color = default(Color), int thickness = 3) {
         lock (synchronization) {
            using (var pen = new Pen(color == default(Color) ? Color.Black : color, thickness))
            using (var g = Graphics.FromImage(bitmap)) {
               g.DrawLine(
                  pen,
                  (float)sx + drawPadding.X,
                  (float)sy + drawPadding.Y,
                  (float)ex + drawPadding.X,
                  (float)ey + drawPadding.Y);
            }
            UpdateDisplay();
         }
      }

      public void DrawMeshes(IReadOnlyList<ConnectedMesh> meshes) {
         foreach (var mesh in meshes) {
            DrawMesh(mesh);
         }
      }

      public void DrawPoint(double x, double y, Brush brush = null) {
         const float radius = 9f;
         const float width = 1.0f + 2 * radius;
         lock (synchronization) {
            using (var g = Graphics.FromImage(bitmap)) {
               g.FillEllipse(brush ?? Brushes.Magenta, (float)x - radius + drawPadding.X, (float)y - radius + drawPadding.Y, width, width);
            }
            UpdateDisplay();
         }
      }
   }

   public static class GeometryOperations {
      public static UnionOperation Union() => new UnionOperation();

      public class UnionOperation {
         private readonly Clipper clipper = new Clipper { StrictlySimple = true };

         public UnionOperation Include(params Polygon[] polygons) => Include((IReadOnlyList<Polygon>)polygons);

         public UnionOperation Include(IReadOnlyList<Polygon> polygons) {
            foreach (var polygon in polygons) {
               clipper.AddPath(polygon.Points, PolyType.ptSubject, polygon.IsClosed);
            }
            return this;
         }

         public PolyTree Execute() {
            var polytree = new PolyTree();
            clipper.Execute(ClipType.ctUnion, polytree, PolyFillType.pftPositive, PolyFillType.pftPositive);
            return polytree;
         }
      }

      public static PunchOperation Punch() => new PunchOperation();

      public class PunchOperation {
         private readonly Clipper clipper = new Clipper { StrictlySimple = true };

         public PunchOperation Include(params Polygon[] polygons) => Include((IReadOnlyList<Polygon>)polygons);

         public PunchOperation Include(IReadOnlyList<Polygon> polygons) {
            foreach (var polygon in polygons) {
               clipper.AddPath(polygon.Points, PolyType.ptSubject, polygon.IsClosed);
            }
            return this;
         }

         public PunchOperation Exclude(params Polygon[] polygons) => Exclude((IReadOnlyList<Polygon>)polygons);

         public PunchOperation Exclude(IReadOnlyList<Polygon> polygons) {
            foreach (var polygon in polygons) {
               clipper.AddPath(polygon.Points, PolyType.ptClip, polygon.IsClosed);
            }
            return this;
         }

         public PolyTree Execute(double additionalErosionDilation = 0.0) {
            var polytree = new PolyTree();
            clipper.Execute(ClipType.ctDifference, polytree, PolyFillType.pftPositive, PolyFillType.pftPositive);

            // Used to remove degeneracies where additionalErosion is 0.
            const double baseErosion = 0.05;
            return GeometryOperations.Offset()
                                     .Include(FlattenToPolygons(polytree))
                                     .Erode(baseErosion)
                                     .Dilate(baseErosion)
                                     .ErodeOrDilate(additionalErosionDilation)
                                     .Execute();
         }
      }

      public static OffsetOperation Offset() => new OffsetOperation();

      public class OffsetOperation {
         private readonly List<double> offsets = new List<double>();
         private readonly List<Polygon> includedPolygons = new List<Polygon>(); 

         /// <param name="delta">Positive dilates, negative erodes</param>
         public OffsetOperation ErodeOrDilate(double delta) {
            offsets.Add(delta);
            return this;
         }

         public OffsetOperation Erode(double delta) {
            if (delta < 0) {
               throw new ArgumentOutOfRangeException();
            }

            offsets.Add(-delta);
            return this;
         }

         public OffsetOperation Dilate(double delta) {
            if (delta < 0) {
               throw new ArgumentOutOfRangeException();
            }

            offsets.Add(delta);
            return this;
         }

         public OffsetOperation Include(params Polygon[] polygons) => Include((IReadOnlyList<Polygon>)polygons);

         public OffsetOperation Include(IReadOnlyList<Polygon> polygons) {
            includedPolygons.AddRange(polygons);
            return this;
         }

         public PolyTree Execute() {
            var currentPolygons = includedPolygons;
            for (int i = 0; i < offsets.Count; i++) {
               PolyTree polytree = new PolyTree();
               var clipper = new ClipperOffset();
               foreach (var polygon in currentPolygons) {
                  clipper.AddPath(polygon.Points, JoinType.jtSquare, EndType.etClosedPolygon);
               }
               clipper.Execute(ref polytree, offsets[i]);
               if (i + 1 == offsets.Count) {
                  return polytree;
               } else {
                  currentPolygons = FlattenToPolygons(polytree);
               }
            }
            throw new ArgumentException("Must specify some polygons to include!");
         }
      }

      public static PolyTree CleanPolygons(List<Polygon> polygons) {
         return GeometryOperations.Offset()
                                  .Include(polygons)
                                  .Erode(0.05)
                                  .Dilate(0.05)
                                  .Execute();

      }

      public static List<Polygon> FlattenToPolygons(this PolyNode polytree) {
         var results = new List<Polygon>();
         FlattenPolyTreeToPolygonsHelper(polytree, polytree.IsHole, results);
         return results;
      }
      
      private static void FlattenPolyTreeToPolygonsHelper(PolyNode current, bool isHole, List<Polygon> results) {
         if (current.Contour.Count > 0) {
            results.Add(new Polygon(current.Contour, isHole));
         }
         foreach (var child in current.Childs) {
            // We avoid node.isHole as that traverses upwards recursively and wastefully.
            FlattenPolyTreeToPolygonsHelper(child, !isHole, results);
         }
      }

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

      private static bool IsPointInTriangle(double px, double py, DelaunayTriangle triangle) {
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

   public class Polygon {
      public Polygon(List<IntPoint> points, bool isHole) {
         if (points[0] != points.Last()) {
            Console.WriteLine("Warn: Polygon took open (non-closed) poly");
            points.Add(points[0]);
         }

         this.Points = points;
         this.IsHole = isHole;
      }

      public List<IntPoint> Points { get; set; }
      public bool IsHole { get; set; }
      public bool IsClosed => true;

      public static Polygon CreateRect(int x, int y, int width, int height) {
         var points = new List<IntPoint> {
            new IntPoint(x, y),
            new IntPoint(x + width, y),
            new IntPoint(x + width, y + height),
            new IntPoint(x, y + height),
            new IntPoint(x, y)
         };
         return new Polygon(points, true);
      }
   }
}
