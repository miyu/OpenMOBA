using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Debugging;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace VisibilityPolygonQueries {
   public class Program {
      private static readonly Size bounds = new Size(1000, 1000);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(
         bounds,
         new Point(50, 50),
         new OrthographicXYProjector(
            1.8, 
            new IntVector2(500, 500),
            new IntVector2(bounds.Width / 2, bounds.Height / 2),
            true));
      private static int nextFrameIndex = 0;

      public static void Main(string[] args) {
         random.NextBytes(new byte[21337]);
         RenderArrangements(1);
         random.NextBytes(new byte[1337]);
         // RenderArrangements(2);
         // random.NextBytes(new byte[1337]);
         // RenderArrangements(3);
         // RenderArrangements(4);
         
         // RenderTestQueries(SectorMetadataPresets.Blank2D);
         // RenderTestQueries(SectorMetadataPresets.Test2D);
         // RenderTestQueries(SectorMetadataPresets.CrossCircle);
         // RenderTestQueries(SectorMetadataPresets.FourSquares2D);
         // RenderTestQueries(SectorMetadataPresets.HashCircle1);
         // RenderTestQueries(SectorMetadataPresets.DotaStyleMoba);

         // host.DumpScreenshotsToDocumentsPictures();
      }

      private static void RenderTestQueries(TerrainStaticMetadata sectorMetadataPresets) {
         var terrainStaticMetadata = new TerrainStaticMetadata {
            LocalBoundary = sectorMetadataPresets.LocalBoundary,
            LocalIncludedContours = sectorMetadataPresets.LocalIncludedContours,
            LocalExcludedContours = sectorMetadataPresets.LocalExcludedContours
         };
         var localGeometryJob = new LocalGeometryJob(terrainStaticMetadata, new HashSet<(IntLineSegment2 segment, Clockness inClockness)>());
         var localGeometryViewManager = new LocalGeometryViewManager(localGeometryJob);
         var actorRadius = 1;
         var localGeometryView = localGeometryViewManager.GetErodedView(actorRadius);

         var canvas = host.CreateAndAddCanvas(nextFrameIndex++);
         canvas.Transform = Matrix4x4.CreateScale(1000 / 60000.0f) * Matrix4x4.CreateTranslation(500, 500, 0);
         canvas.DrawPolyNode(localGeometryView.PunchedLand, StrokeStyle.BlackHairLineSolid, StrokeStyle.RedHairLineSolid);
         canvas.DrawTriangulation(localGeometryView.Triangulation, StrokeStyle.CyanHairLineDashed5);
         foreach (var (i, island) in localGeometryView.Triangulation.Islands.Enumerate()) {
            var poly = ConvertTriangulationToWeaklySimplePolygon(island, canvas);
            var eroded = PolygonOperations.Offset()
                                          .Include(new Polygon2(poly.Select(x => x.LossyToIntVector2()).ToList()))
                                          .Erode(500)
                                          .Execute();
            canvas.DrawPolyNode(eroded, StrokeStyle.CyanThick5Solid);
         }
      }

      private static void RenderArrangements(int i = 0) {
         var canvas = host.CreateAndAddCanvas(nextFrameIndex++);
         // canvas.Transform = Matrix4x4.CreateScale(1000 / 60000.0f) * Matrix4x4.CreateTranslation(500, 500, 0);

         var c = new DoubleVector2(250, 250);
         var segments = i == 0 ? new[] {
            new DoubleLineSegment2(c + new DoubleVector2(100, 100), c + new DoubleVector2(400, 300)),
            new DoubleLineSegment2(c + new DoubleVector2(200, 500), c + new DoubleVector2(400, 250)),
            new DoubleLineSegment2(c + new DoubleVector2(150, 70), c + new DoubleVector2(250, 550))
         } : RandomInput(10).Item2.Map(x => new DoubleLineSegment2(c + x.First.ToDoubleVector2() / 2, c + x.Second.ToDoubleVector2() / 2));

         canvas.BatchDraw(() => {
            ComputeArrangement(segments, canvas);
         });
      }

      /// <summary>
      /// Builds MST of triangulation island graph { Nodes = Triangles, Edges = Neighbor Relationships }
      /// Then walks MST CCW emitting a closed weakly simple polygon boundary.
      /// </summary>
      private static List<DoubleVector2> ConvertTriangulationToWeaklySimplePolygon(TriangulationIsland island, IDebugCanvas canvas = null) {
         const int TREE_ROOT_TRIANGLE_INDEX = 0;
         const int NONE = Triangle3.NO_NEIGHBOR_INDEX;

         var tris = island.Triangles;

         // Build any spanning tree (in this case via DFS), tracking parents (search predecessors)
         var preds = new int[tris.Length];
         for (var i = 0; i < preds.Length; i++) {
            preds[i] = NONE;
         }

         void VisitDFS(int curti, int predti) {
            ref var triangle = ref tris[curti];

            preds[curti] = predti;

            for (var i = 0; i < 3; i++) {
               var succti = triangle.NeighborOppositePointIndices[i];
               if (succti == NONE || succti == TREE_ROOT_TRIANGLE_INDEX) continue;
               if (preds[succti] != NONE) continue;
               VisitDFS(succti, curti);
            }
         }

         VisitDFS(TREE_ROOT_TRIANGLE_INDEX, NONE);

         canvas?.BatchDraw(() => {
            for (var ti = 0; ti < tris.Length; ti++) {
               if (preds[ti] != NONE) {
                  canvas.DrawLine(tris[ti].Centroid, tris[preds[ti]].Centroid, StrokeStyle.RedThick5Solid);
               }
            }
         });

         // Walk spanning tree hugging CCW to emit weak simple polygon boundary.
         // Additional 2 boundary points for root triangle.
         var boundary = new List<DoubleVector2>(tris.Length * 2 + 2);

         // For a given triangle and predecessor-shared edge, emit CCW boundary
         // Assume boudnary point from predecessor-shared edge is already emitted.
         void WalkMSTAndEmitSimplePolygonBoundary(int curti, int predti) {
            ref var triangle = ref tris[curti];

            var isCurrentTriangleRoot = predti == NONE;
            var predei = isCurrentTriangleRoot ? NONE
               : triangle.NeighborOppositePointIndices.A == predti ? 0
                  : triangle.NeighborOppositePointIndices.B == predti ? 1 : 2;

            // For the root triangle, repeat the emitted point of the last iteration
            // in the below for loop to form a closed polygon.
            if (isCurrentTriangleRoot) {
               boundary.Add(triangle.Points.A);
            }

            // Loop invariant: the CCmost point of the next-to-emit edge is already emitted.
            // triangles are CCW, polygon2s are CCW.
            var startEdgeIndexOffset = isCurrentTriangleRoot ? 0 : 1;
            for (var i = startEdgeIndexOffset; i < 3; i++) {
               var succei = (predei + i + 3) % 3;
               var succti = triangle.NeighborOppositePointIndices[succei];
               if (succti != NONE && preds[succti] == curti) {
                  WalkMSTAndEmitSimplePolygonBoundary(succti, curti);
               }
               var pointIndexCounterClockWisemostOfEdge = (predei + i + 2) % 3;
               boundary.Add(triangle.Points[pointIndexCounterClockWisemostOfEdge]);
            }
         }
         
         WalkMSTAndEmitSimplePolygonBoundary(TREE_ROOT_TRIANGLE_INDEX, NONE);
         Trace.Assert(boundary.Count == boundary.Capacity);
         Trace.Assert(boundary[0] == boundary[boundary.Count - 1]);

         return boundary;
      }

      private static (int, int) PickDiagonalInWeaklySimplePolygon(List<DoubleVector2> dv2) {
         return default((int, int));
      }

      private static void ComputeArrangement(DoubleLineSegment2[] lines, IDebugCanvas debugCanvas = null) {
         // pq with intersection site events
         var q = new PriorityQueue<(DoubleVector2 p, int i1, int i2)>((a, b) => a.p.Y.CompareTo(b.p.Y));
         for (var i = 0; i < lines.Length; i++) {
            var a = lines[i];
            for (var j = i + 1; j < lines.Length; j++) {
               var b = lines[j];
               if (GeometryOperations.TryFindLineLineIntersection(a.First, a.Second,b.First, b.Second, out var x)) {
                  q.Enqueue((x, i, j));
                  Trace.Assert(GeometryOperations.IsReal(x));
                  debugCanvas?.DrawPoint(x, StrokeStyle.RedThick10Solid);
               }
            }
            // if (lines[i].PointAtX(-50).To(lines[i].PointAtX(bounds.Width + 50)).Norm2D() > bounds.Width * 1.3) {
            //    debugCanvas?.DrawLine(lines[i].PointAtY(-50), lines[i].PointAtY(bounds.Height + 50), StrokeStyle.BlackHairLineDashed5);
            // } else {
            //    debugCanvas?.DrawLine(lines[i].PointAtX(-50), lines[i].PointAtX(bounds.Width + 50), StrokeStyle.BlackHairLineDashed5);
            // }
         }
         Console.WriteLine("Setup initial");
         // Start vertical line sweep above first site.
         var initialSweepY = q.Peek().p.Y - 10;
         var initialLineXs = lines.Map(line => line.PointAtY(initialSweepY).X);

         // Build initial beachfront (order segments intersecting sweepY).
         var qq = new PriorityQueue<(double x, int i)>((a, b) => a.x.CompareTo(b.x));
         for (var i = 0; i < lines.Length; i++) {
            qq.Enqueue((initialLineXs[i], i));
         }
         var qqa = Util.Generate(lines.Length, qq.Dequeue);

         var cells = new List<(List<(DoubleVector2 p, int li)> left, List<(DoubleVector2 p, int li)> right)>();
         var lineIndexToLeftCellIndex = lines.Map(_ => -1);
         var lineIndexToRightCellIndex = lines.Map(_ => -1);
         for (var i = 0; i <= qqa.Length; i++) {
            (List<(DoubleVector2, int)> left, List<(DoubleVector2, int)> right) cell = default;

            if (i != 0) {
               cell.left = new List<(DoubleVector2, int)> { (new DoubleVector2(qqa[i - 1].x, initialSweepY), qqa[i - 1].i) };
               lineIndexToRightCellIndex[qqa[i - 1].i] = cells.Count;
            }

            if (i != qqa.Length) {
               cell.right = new List<(DoubleVector2, int)> { (new DoubleVector2(qqa[i].x, initialSweepY), qqa[i].i) };
               lineIndexToLeftCellIndex[qqa[i].i] = cells.Count;
            }

            cells.Add(cell);
         }

         (DoubleVector2 p, int i1, int i2) Pick(double? expectedY = null) {
            var t = q.Dequeue();
            var (p, first, second) = t;

            Trace.Assert(!expectedY.HasValue || Math.Abs(p.Y - expectedY.Value) < 0.001);

            // enforce line a left of b at start, crossing to right of b.
            if (initialLineXs[first] > initialLineXs[second]) (first, second) = (second, first);
            var priorARightCi = lineIndexToRightCellIndex[first];
            var priorBLeftCi = lineIndexToLeftCellIndex[second];
            if (priorARightCi == priorBLeftCi) {
               return t;
            } else {
               var res = Pick(p.Y);
               q.Enqueue(t);
               return res;
            }
            // Console.WriteLine(p + " " + priorARightCi + " " + priorBLeftCi + " then " + (q.Count > 0 ? q.Peek().ToString() : "done"));
            // Trace.Assert(priorARightCi == priorBLeftCi);
         }

         Console.WriteLine("Sweep");
         const int NONE = -1;
         // foreach (var t in q) {
         while (q.Count > 0) {
            var (p, first, second) = Pick();
            if (initialLineXs[first] > initialLineXs[second]) (first, second) = (second, first);
            var priorARightCi = lineIndexToRightCellIndex[first];
            var priorBLeftCi = lineIndexToLeftCellIndex[second];

            // end cell.
            cells[priorARightCi].left.Add((p, NONE));
            cells[priorARightCi].right.Add((p, NONE));

            // and add point to neighbors
            cells[lineIndexToLeftCellIndex[first]].right.Add((p, second));
            cells[lineIndexToRightCellIndex[second]].left.Add((p, first));

            // update cell & neighboring line indices
            lineIndexToRightCellIndex[first] = lineIndexToRightCellIndex[second];
            lineIndexToLeftCellIndex[second] = lineIndexToLeftCellIndex[first];
            lineIndexToLeftCellIndex[first] = cells.Count;
            lineIndexToRightCellIndex[second] = cells.Count;

            // start cell
            (List<(DoubleVector2, int)> left, List<(DoubleVector2, int)> right) cell = default;
            cell.left = new List<(DoubleVector2, int)> { (p, second) };
            cell.right = new List<(DoubleVector2, int)> { (p, first) };
            cells.Add(cell);
         }

         Console.WriteLine("Ren " + cells.Count);
         foreach (var (i, cell) in cells.Enumerate()) {
            if (false && cell.left != null) {
               debugCanvas.DrawLineStrip(
                  cell.left.Map(x => x.p),
                  StrokeStyle.CyanThick25Solid);
            }
            if (false && cell.right != null) {
               debugCanvas.DrawLineStrip(
                  cell.right.Map(x => x.p),
                  StrokeStyle.LimeThick25Solid);
            }

            // should be ccw
            var l = new List<DoubleVector2>();
            bool big = false;
            if (cell.left != null) {
               if (cell.right == null || cell.right[0].p != cell.left[0].p) {
                  var line = lines[cell.left[0].li];
                  if (line.First.Y != line.Second.Y) l.Add(line.PointAtY(-50));
                  big = true;
               }
               l.AddRange(cell.left.Map(x => x.p));
               if (cell.right == null || cell.left.Count == 1 || cell.right.Last().p != cell.left.Last().p) {
                  var line = lines[cell.left.Last().li];
                  if (line.First.Y != line.Second.Y) l.Add(line.PointAtY(1000));
                  big = true;
               }
            }
            l.Reverse();
            if (cell.right != null) {
               if (cell.left == null || cell.left[0].p != cell.right[0].p) {
                  var line = lines[cell.right[0].li];
                  if (line.First.Y != line.Second.Y) l.Add(line.PointAtY(-50));
                  big = true;
               }
               if (l.Count > 0 && l[l.Count - 1] == cell.right[0].p) l.RemoveAt(l.Count - 1);
               l.AddRange(cell.right.Map(x => x.p));
               if (cell.left == null || cell.right.Count == 1 || cell.left.Last().p != cell.right.Last().p) {
                  var line = lines[cell.right.Last().li];
                  if (line.First.Y != line.Second.Y) l.Add(line.PointAtY(1000));
                  big = true;
               }
            }

            if (debugCanvas == null) continue;

            var colors = new[] {
               Color.Red,
               Color.Lime,
               Color.Cyan,
               Color.Magenta,
               Color.Orange,
               Color.Yellow,
               Color.Aqua,
               Color.Chocolate,
               Color.DarkOliveGreen,
               Color.Maroon,
               Color.LawnGreen,
            };
            if (l.Count >= 3) {
               if (big && i == 0) {
                  // Debugger.Break();
                  // continue;
               }
               if (l.Any(p => !GeometryOperations.IsReal(p))) {
                  // Debugger.Break();
               }
               var fillColor = colors[random.Next(colors.Length)];
               // if (i % 3 == 0) continue;
               // if (i != 3064) continue;
               // if (i <= 400) continue;
               // if (i > 4793) break;
               var poly = new Polygon2(l.Select(p => p.LossyToIntVector2()).ToList());
               if (PolygonOperations.TryConvexClip(poly, Polygon2.CreateRect(-50, -50, bounds.Width + 100, bounds.Height + 100), out var clip)) {
                  var pt = PolygonOperations.Offset()
                                            .Include(clip)
                                            .Erode(0)
                                            .Execute();
                  var tris = new Triangulator().TriangulateRoot(pt);
                  debugCanvas?.FillTriangulation(tris, new FillStyle(fillColor));
                  // if (tris.Islands.Any()) {
                  //    var b = tris.Islands[0].IntBounds;
                  //    // if (i % 3 == 1)
                  //    // debugCanvas?.DrawText(i.ToString(), new IntVector2((b.Left + b.Right) / 2, (b.Top + b.Bottom) / 2));
                  // }
                  // debugCanvas?.FillPolygonTriangulation(clip, new FillStyle(fillColor));
               }
            }
         }

         Console.WriteLine("Done");
      }

      private static (DoubleVector2, IntLineSegment2[]) RandomInput(int nsegs) {
         var segments = new IntLineSegment2[nsegs];
         for (var i = 0; i < segments.Length; i++) {
            var s = RandomSegment();
            while (segments.Take(i).Any(s.Intersects) || s.First.Y == s.Second.Y) s = RandomSegment();
            segments[i] = s;
         }
         var p = RandomPoint();
         return (p.ToDoubleVector2(), segments);
      }

      private static IntVector2 RandomPoint() {
         return new IntVector2(random.Next(0, bounds.Width), random.Next(0, bounds.Height));
      }

      private static IntLineSegment2 RandomSegment() {
         var b = RandomPoint();
         var o = IntVector2.Zero;
         while (o == IntVector2.Zero) o = new IntVector2(random.Next(-100, 100), random.Next(-100, 100));
         return new IntLineSegment2(b, b + o);
      }
   }
}