using ClipperLib;
using OpenMOBA;
using Poly2Tri.Triangulation.Delaunay;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Priority_Queue;

namespace ConsoleApplication {
   public class Program {
      public static void Main(string[] args) {
         Console.WriteLine("!");
         var worldRectangle = Polygon.CreateRect(0, 0, 1000, 1000);
         var holeA = Polygon.CreateRect(100, 100, 300, 300);
         var holeB = Polygon.CreateRect(400, 200, 100, 100);
         var holeC = Polygon.CreateRect(200, -50, 100, 150);
         var holeD = Polygon.CreateRect(600, 600, 300, 300);
         var holeE = Polygon.CreateRect(700, 500, 100, 100);
         var holeF = Polygon.CreateRect(200, 700, 100, 100);
         var donutA = Polygon.CreateRect(600, 100, 300, 100);
         var donutB = Polygon.CreateRect(600, 200, 100, 100);
         var donutC = Polygon.CreateRect(800, 200, 100, 100);
         var donutD = Polygon.CreateRect(600, 300, 300, 100);
         var punchResult = GeometryOperations.Punch()
                                             .Include(worldRectangle)
                                             .Exclude(holeA, holeB, holeC, holeD, holeE, holeF, donutA, donutB, donutC, donutD)
                                             .Execute();
         GeometryDisplay.CreateShow().DrawPolygons(punchResult.FlattenToPolygons());

         var triangulator = new Triangulator();
         var meshes = triangulator.Triangulate(punchResult);
         Console.WriteLine(meshes.Count);
         var display = GeometryDisplay.CreateShow();
         display.DrawMeshes(meshes);

         Path(display, meshes, 50, 50, 700, 80);
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
//         foreach (var triangle in trianglePath) {
//            display.DrawTriangle(triangle);
//         }
      }

      private static List<DelaunayTriangle> AStar(ConnectedMesh mesh, DelaunayTriangle start, DelaunayTriangle end) {
         var visited = new HashSet<DelaunayTriangle>();
         var q = new FastPriorityQueue<AStarNode>(Int16.MaxValue);
         q.Enqueue(new AStarNode { Triangle = start }, 0);

         var minTriangleDistance = new Dictionary<DelaunayTriangle, float>();

         while (q.Any()) {
            Console.WriteLine(q.Count);
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

            foreach (var neighbor in current.Triangle.Neighbors.Where(n => n != null)) {
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
               q.Enqueue(new AStarNode { Parent = current }, current.Priority + distance);
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

      public void DrawPolygon(Polygon polygon) {
         lock (synchronization) {
            using (var g = Graphics.FromImage(bitmap)) {
               for (var i = 0; i < polygon.Points.Count; i++) {
                  var a = polygon.Points[i];
                  var b = polygon.Points[(i + 1) % polygon.Points.Count];
                  var pen = polygon.IsHole ? Pens.Red : Pens.Black;
                  g.DrawLine(pen, a.X + drawPadding.X, a.Y + drawPadding.Y, b.X + drawPadding.X, b.Y + drawPadding.Y);

                  if (i != polygon.Points.Count - 1) {
                     g.DrawString($"{i}", Form.Font, Brushes.Black, a.X + drawPadding.X, a.Y + drawPadding.Y);
                  }
               }
            }
         }
         UpdateDisplay();
      }

      public void DrawPolygons(IReadOnlyList<Polygon> polygons) {
         foreach (var polygon in polygons) {
            DrawPolygon(polygon);
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
         new Thread(() => {
            Application.Run(display.Form);
         }) { ApartmentState = ApartmentState.STA }.Start();
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

      public void DrawMeshes(IReadOnlyList<ConnectedMesh> meshes) {
         foreach (var mesh in meshes) {
            DrawMesh(mesh);
         }
      }

      public void DrawPoint(double x, double y) {
         const float radius = 9f;
         const float width = 1.0f + 2 * radius;
         lock (synchronization) {
            using (var g = Graphics.FromImage(bitmap)) {
               g.FillEllipse(Brushes.Magenta, (float)x - radius + drawPadding.X, (float)y - radius + drawPadding.Y, width, width);
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

         public PolyTree Execute() {
            var polytree = new PolyTree();
            clipper.Execute(ClipType.ctDifference, polytree, PolyFillType.pftPositive, PolyFillType.pftPositive);

            return CleanPolygons(FlattenToPolygons(polytree));
         }
      }

      public static OffsetOperation Offset() => new OffsetOperation();

      public class OffsetOperation {
         private readonly List<double> offsets = new List<double>();
         private readonly List<Polygon> includedPolygons = new List<Polygon>(); 

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

      public static List<Polygon> FlattenToPolygons(this PolyTree polytree) {
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
