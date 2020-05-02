using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Dviz;
using Dargon.PlayOn;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;

namespace Dargon.Terragami {
   public class SectorArrangement {
      private static readonly Random random = new Random(3);

      public static SectorArrangement Create(DoubleLineSegment2[] lines, IDebugCanvas debugCanvas) {
         // pq with intersection site events
         var q = new PriorityQueue<(DoubleVector2 p, int i1, int i2)>((a, b) => a.p.Y.CompareTo(b.p.Y));
         for (var i = 0; i < lines.Length; i++) {
            var a = lines[i];
            for (var j = i + 1; j < lines.Length; j++) {
               var b = lines[j];
               if (GeometryOperations.TryFindLineLineIntersection(a.First, a.Second, b.First, b.Second, out var x)) {
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

         var qqa = Arrays.Create(lines.Length, qq.Dequeue);

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


            var assert = !expectedY.HasValue || Math.Abs(p.Y - expectedY.Value) < 0.001;
            if (!assert) {
               Console.WriteLine(expectedY.Value);
               debugCanvas.DrawLine(new DoubleVector2(-100000, expectedY.Value), new DoubleVector2(100000, expectedY.Value), StrokeStyle.MagentaHairLineSolid);
               while (true) ;
            }
            Assert.IsTrue(assert);

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
            if (i % 1000 == 0) Console.WriteLine($"Progress {i} / {cells.Count}");

            if (cell.left != null) {
               // debugCanvas.DrawLineStrip(
               //    cell.left.Map(x => x.p),
               //    StrokeStyle.CyanHairLineSolid);
            }

            if (cell.right != null) {
               // debugCanvas.DrawLineStrip(
               //    cell.right.Map(x => x.p),
               //    StrokeStyle.LimeHairLineSolid);
            }

            // below computes ccw
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

            // should be cw & open
            l.Reverse();
            while (l.Count > 0 && l[0] == l[^1]) {
               l.RemoveAt(l.Count - 1);
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
               // var tris = new Triangulator().TriangulateRoot(PolygonNode.CreateRootHole(PolygonNode.Create(poly.Points.ToArray().Reverse().ToArray(), false)));
               // debugCanvas?.FillTriangulation(tris, new FillStyle(fillColor));

               if (PolygonOperations.TryConvexClip(poly, Polygon2.CreateRect(-50, -50, 1000 + 100, 1000 + 100), out var clip)) {
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

         return null;
      }

      public class Cell { }
   }
}
