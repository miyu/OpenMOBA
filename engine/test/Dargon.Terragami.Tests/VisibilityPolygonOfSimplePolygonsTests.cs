using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;

namespace Dargon.Terragami.Tests {
   public struct VisibilityPolygonWindow {
      public int? EndpointPolygonPointIndexOrNull;
      public DoubleVector2 Endpoint;
      public int ReflexVertexIndex;
      public int SegmentFirstPointIndex;
      public float WindowSegmentT;
   }

   public class VisibilityPolygonOfSimplePolygonsTests {
      public const float TWO_PI = MathF.PI * 2;
      private const bool kEnableDebugPrint = false;

      public struct StackEntry {
         public DoubleVector2 Cartesian;
         public float WindingOffset;

         /// <summary>
         /// Note: PointIndex is vanity data - it's not required for computing
         /// the visibility polygon's contour, but tracking it is useful information
         /// for any consuming client.
         /// 
         /// PointIndex is non-null if the stack entry represents an initial vertex of the
         /// source polygon.
         /// </summary>
         public int? PointIndex;

         /// <summary>
         /// Note: WindowEndpointData is vanity data - it's not required for computing
         /// the visibility polygon's contour, but tracking it is useful information
         /// for any consuming client.
         ///
         /// WindowEndpointData is non-null if the stack entry represents a window vertex.
         /// </summary>
         public WindowEndpointData? WindowEndpointData;
      }

      public struct WindowEndpointData {
         /// <summary>
         /// This might be redundantly tracked? One can derive this from a final vispoly
         /// where the reflex vertex will either be immediately before or after the window
         /// in the output contour.
         /// </summary>
         public int WindowReflexVertexIndex;

         /// <summary>
         /// The first index of the line segment our window endpoint is sitting on.
         /// The line segment is v[WindowSegmentFirstIndex] to v[WindowSegmentFirstIndex + 1]
         /// </summary>
         public int WindowSegmentFirstIndex;

         /// <summary>
         /// T of line segment our window endpoint is sitting on.
         /// T = 0 means we're at v[WindowSegmentFirstIndex], T=1 means we're at v[WindowSegmentFirstIndex + 1].
         /// </summary>
         public double WindowSegmentT;
      }

      public struct WindowEnd {
         public bool IsRayElsePointEndpoint;

         /// <summary>
         /// From ADVANCE there are two cases:
         /// 1. the direction from v0 to s[^1] (window captures points
         /// returning back to visibility, e.g. after an advance, we have a
         /// winding regression & advancement continues once the contour walk
         /// returns to / surpassed the winding angle we just advanced to).
         ///
         /// 2. v0, e.g. winding angle has advanced beyond 2pi, and vispoly building
         /// will only continue once it regresses back past 2pi, through the line
         /// segment from s[^1] to v0.
         ///
         /// From RETARD there are two cases:
         /// 1. Note below, v0 will see A, B, C, then retard with D, scanning past
         /// the window ray from D in direction v0-D.
         /// 
         ///          *v0
         /// 
         ///   C+---+ D
         ///    | E'
         ///    +--------+
         ///    B        A
         ///
         /// 2. See https://imgur.com/a/EHQAIgH -- in this case, we've cut an old
         /// window, and will return to visibility through the segment from s[4]
         /// to intersection(v9-v10, v4-s[5]).
         /// </summary>
         public DoubleVector2 DirectionFromStackTopIfWindowIsRayElsePointEndpoint;

         /// <summary>
         /// Windows either extend from either v0 (for ADVANCE case 2 above) or
         /// reflex vertices of our input polygon (every other case).
         ///
         /// In fact, in the case of ADVANCE Case 2 above, we have a degenerate
         /// case where v0 is a reflex vertex with internal angle 2pi.
         /// 
         /// This captures the index of that vertex. It's useful for later when we
         /// might ask "what windows come from this vertex".
         /// </summary>
         public int WindowReflexVertexIndex;
      }

      // No inlining takes vispoly from 7us to 5.9us.
      [MethodImpl(MethodImplOptions.NoInlining)]
      public float ComputeWindingOffset(float prevWindingOffset, in DoubleVector2 v0, in DoubleVector2 prev, in DoubleVector2 cur) {
         var angle = MathUtils.SignedAngleBetweenVectorsF(
            v0.To(cur),
            v0.To(prev));
         return prevWindingOffset + angle;
      }

      public void Execute() {
         // Compute(Polygon2.CreateRect(0, 0, 200, 200), new IntVector2(0, 0));
         // var poly = Polygon2.CreateCircle(0, 0, 200, n: 8);
         // poly.Points[2] = new IntVector2(50, 80);
         // poly.Points[4] = new IntVector2(-20, -40);
         // poly.Points[5] = new IntVector2(-25, -80);
         // poly.Points[6] = new IntVector2(-50, 50);
         // poly.Visualize(labelIndices: true);

         var poly = new Polygon2(new List<IntVector2> {
            new IntVector2(165, 326),
            new IntVector2(191, 300),
            new IntVector2(240, 300),
            new IntVector2(381, 216),
            new IntVector2(300, 150),
            new IntVector2(280, 75),
            new IntVector2(300, 50),
            new IntVector2(200, 50),
            new IntVector2(185, 25),
            new IntVector2(170, 50),
            new IntVector2(155, 25),
            new IntVector2(120, 75),
            new IntVector2(250, 75),
            new IntVector2(300, 225),
            new IntVector2(225, 275),
            new IntVector2(195, 225),
            new IntVector2(235, 150),
            new IntVector2(165, 100),
            new IntVector2(100, 150),
            new IntVector2(150, 225),
            new IntVector2(100, 275),
            new IntVector2(115, 225),
            new IntVector2(80, 150),
            new IntVector2(150, 100),
            new IntVector2(115, 100),
            new IntVector2(40, 150),
            new IntVector2(40, 200),
            new IntVector2(75, 200),
            new IntVector2(75, 250),
            new IntVector2(50, 250),
            new IntVector2(50, 225),
            new IntVector2(25, 225),
            new IntVector2(25, 275),
            new IntVector2(50, 275),
            new IntVector2(50, 260),
            new IntVector2(75, 260),
            new IntVector2(75, 285),
            new IntVector2(40, 285),
            new IntVector2(40, 300),
            new IntVector2(150, 300)
         }.Map(p => new IntVector2(p.X, 400 - p.Y)).Reverse().ToList());

         var bounds = AxisAlignedBoundingBox2.BoundingPoints(poly.Points.ToArray());
         // bounds.Center += new DoubleVector2(0.8 * Math.Abs(bounds.Extents.X), 0.2 * Math.Abs(bounds.Extents.Y));
         // bounds.Extents *= 0.5;
         // bounds.Center += new DoubleVector2(-0.7 * Math.Abs(bounds.Extents.X), -0.8 * Math.Abs(bounds.Extents.Y));
         // bounds.Extents *= 0.05;
         var dmch = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
            bounds,
            new Size(2250, 1100),
            new Point(100, 100));

         // ComputeVisibilityPolygon(poly, 6, dmch, DebugDrawMode.Steps); // this commit also fixed 37
         // return;

         var canvas = dmch.CreateAndAddCanvas();
         canvas.DrawPolygon(poly, StrokeStyle.BlackHairLineSolid);
         poly.Visualize(canvas, labelIndices: true, labelCoordinates: true);

         var allWindows = new List<VisibilityPolygonWindow>();
         var reflexVertexAndSegmentFirstPointIndexToWindowEndpoints = new Dictionary<(int, int), List<DoubleVector2>>();
         var addedVertexToVertexWindows = new HashSet<(int, int)>();
         for (var i = 0; i < poly.Points.Count; i++) {
            var windows = ComputeVisibilityPolygon(poly, i, dmch, DebugDrawMode.None);
            var lines = windows.Map(w => new DoubleLineSegment2(w.Endpoint, poly.Points[w.ReflexVertexIndex].ToDoubleVector2()));
            canvas.DrawLineList(lines, new StrokeStyle(Color.Gray, 1.0f, new[] { 100.0f, 100.0f }));
            // Console.WriteLine($"idx {i} {allWindows.Count}-{(allWindows.Count + lines.Length - 1)}");
            // allWindows.AddRange(windows);

            foreach (var window in windows) {
               // dedupe windows where endpoint and reflex vertex are both polygon points
               if (window.EndpointPolygonPointIndexOrNull != null) {
                  var endpointIndex = window.EndpointPolygonPointIndexOrNull.Value;
                  var reflexIndex = window.ReflexVertexIndex;
                  var (a, b) = (endpointIndex, reflexIndex);
                  if (a > b) (a, b) = (b, a);

                  if (!addedVertexToVertexWindows.Add((a, b))) {
                     // window already visited
                     continue;
                  }
               }

               // Handle cases where two vispolies would add an identical window (e.g. because they're
               // both collinear with the reflex vertex)
               var key = (window.ReflexVertexIndex, window.SegmentFirstPointIndex);
               if (reflexVertexAndSegmentFirstPointIndexToWindowEndpoints.TryGetValue(key, out var existing)) {
                  bool endpointAlreadyAdded = false;
                  foreach (var priorEndpoint in existing) {
                     if (DoubleVector2.SquaredDistanceNorm2(window.Endpoint, priorEndpoint) < EPSILON) {
                        endpointAlreadyAdded = true;
                        break;
                     }
                  }

                  if (endpointAlreadyAdded) continue;
               } else {
                  existing = reflexVertexAndSegmentFirstPointIndexToWindowEndpoints[key] = new List<DoubleVector2>();
               }
               existing.Add(window.Endpoint);

               allWindows.Add(window);
            }
         }
         
         var (nodes, edges) = SimplePolygonVisibilityDecomposition.Imstreafmingontwitch(poly, allWindows, dmch, DebugDrawMode.None);
         var pfe = new PlanarEmbeddingFaceExtractor().X(nodes, edges, dmch, DebugDrawMode.None);

         var bvh = BvhTreeAABB2<PlanarEmbeddingFaceExtractor.Cell>.Build(
            pfe.Cells.Map(c => {
               var bbox = AxisAlignedBoundingBox2.BoundingPoints(c.Left.Concat(c.Right).Select(x => x.node.Vertex).ToArray());
               return new KeyValuePair<AxisAlignedBoundingBox2, PlanarEmbeddingFaceExtractor.Cell>(bbox, c);
            }));

         var bvhDepthStrokeStyles = new[] {
            StrokeStyle.BlackHairLineSolid,
            StrokeStyle.RedHairLineSolid,
            StrokeStyle.LimeHairLineSolid,
            StrokeStyle.CyanHairLineSolid,
            StrokeStyle.MagentaHairLineSolid,
            StrokeStyle.OrangeHairLineSolid,
            new StrokeStyle(Color.SpringGreen), 
            new StrokeStyle(Color.Teal),
            new StrokeStyle(Color.MediumOrchid),
         };

         var greatestDepth = bvh.AllNodes.Max(x => x.Depth);

         for (var i = 0; i <= greatestDepth; i++) {
            var c = dmch.CreateAndAddCanvas();
            c.DrawPolygon(poly, StrokeStyle.BlackHairLineSolid);
            c.DrawLineList(
               allWindows.Map(w => new DoubleLineSegment2(w.Endpoint, poly.Points[w.ReflexVertexIndex].ToDoubleVector2())),
               StrokeStyle.GrayHairLineSolid);

            foreach (var bvhNode in bvh.AllNodes) {
               if (bvhNode.Depth > i) continue;
               var strokeStyle = bvhDepthStrokeStyles[bvhNode.Depth];
               c.DrawRectangle(bvhNode.Bounds, 0, strokeStyle);
            }
         }

         for (var frame = 0; frame < 1155; frame++) { // 3 * 5 * 7 * 11
         // for (var frame = 90; frame < 110; frame++) {
            var c = dmch.CreateAndAddCanvas();
            c.DrawPolygon(poly, StrokeStyle.BlackHairLineSolid);
            c.DrawLineList(
               allWindows.Map(w => new DoubleLineSegment2(w.Endpoint, poly.Points[w.ReflexVertexIndex].ToDoubleVector2())),
               StrokeStyle.GrayHairLineSolid);

            var r = new Random(frame);
            var q = new DoubleVector2(
               200 + (Math.Sin((frame / (1155 / 3.0)) * Math.PI * 2) + Math.Sin((frame / (1155 / 11.0)) * Math.PI * 2)) * 100,
               200 + (Math.Sin((frame / (1155 / 5.0)) * Math.PI * 2) + Math.Sin((frame / (1155 / 7.0)) * Math.PI * 2)) * 100);
            //r.NextDouble() * 400, r.NextDouble() * 400);

            var leaves = bvh.FindIntersectingLeaves(q);

            var nodesInPathToLeaves = leaves.SelectMany(l => l.Dfs((push, cur) => {
               if (cur.Parent != null) push(cur.Parent);
            })).ToHashSet();

            foreach (var node in bvh.AllNodes) {
               if (!nodesInPathToLeaves.Contains(node)) continue;

               var strokeStyle = bvhDepthStrokeStyles[node.Depth];
               c.DrawRectangle(node.Bounds, 0, strokeStyle);
            }

            foreach (var leaf in leaves) {
               foreach (var cell in leaf.Values) {
                  var contour = cell.Contour;
                  var contained = PointInConvexPolygonTests.Pip(contour, q);
                  c.FillPolygon(cell.Contour, new FillStyle(contained ? Color.Lime : Color.DarkRed));
               }
            }

            c.DrawPoint(q, StrokeStyle.MagentaThick25Solid);
         }

         Console.WriteLine("done");
         return;
         
         ComputeVisibilityPolygon(poly, 0, dmch, DebugDrawMode.Steps);
         for (var i = 0; i < 10; i++) ComputeVisibilityPolygon(poly, 0, dmch, DebugDrawMode.Result);
         
         for (var initialIndex = 0; initialIndex < poly.Points.Count; initialIndex++) {
            ComputeVisibilityPolygon(poly, initialIndex, dmch, DebugDrawMode.Result);
            ComputeVisibilityPolygon(poly, initialIndex, dmch, DebugDrawMode.Result);
            ComputeVisibilityPolygon(poly, initialIndex, dmch, DebugDrawMode.Result);
         }
         
         for (var i = 0; i < 5; i++) ComputeVisibilityPolygon(poly, 0, dmch, DebugDrawMode.Result);
         ComputeVisibilityPolygon(poly, 0, dmch, DebugDrawMode.Steps);
         return;

         while (true) {
            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < 1000; i++) {
               for (var j = 0; j < poly.Points.Count; j++) {
                  ComputeVisibilityPolygon(poly, j);
               }
            }

            var ms = sw.Elapsed.TotalMilliseconds;
            var numVispolies = 1000 * poly.Points.Count;
            Console.WriteLine($"{numVispolies} {ms} {ms / 1000} {ms * 1000 / numVispolies}");
         }
      }

      public const float EPSILON = 1E-5f;
      public const float NEGATIVE_EPSILON = -EPSILON;

      public bool WithinEpsilon(float a, float b) {
         var c = a - b;
         return c > NEGATIVE_EPSILON & c < EPSILON;
      }

      private List<VisibilityPolygonWindow> ComputeVisibilityPolygon(Polygon2 poly, int initialIndex, DebugMultiCanvasHost dmch = null, DebugDrawMode debugDrawMode = DebugDrawMode.None) {
         // With ForwardPolygonIterator, we now observe poly as if v0 is what was at poly[initialIndex].
         // This means windingOffset[0] corresponds to v0.
         var it = new ForwardPolygonIterator(poly, initialIndex);
         var v0 = it.Cur.ToDoubleVector2();
         var z = v0;
         var windingOffsets = new float[it.NumPoints + 1];
         for (var i = 0; i < windingOffsets.Length; i++, it.Step()) {
            if (i == 0 || i == 1) {
               windingOffsets[i] = 0; // if z on boundary(P), 
            } else if (i == windingOffsets.Length - 1) {
               windingOffsets[i] = windingOffsets[i - 1];
            } else {
               windingOffsets[i] = ComputeWindingOffset(
                  windingOffsets[i - 1],
                  v0,
                  it.Prev.ToDoubleVector2(),
                  it.Cur.ToDoubleVector2());
            }
         }

         AxisAlignedBoundingBox2 ComputePolyPointsBounds() => AxisAlignedBoundingBox2.BoundingPoints(poly.Points.ToArray());
         if (debugDrawMode != DebugDrawMode.None) {
            dmch ??= SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
               ComputePolyPointsBounds(),
               new Size(2250, 1100),
               new Point(100, 100));
         }

         it.ResetToInitialIndex(); // i := 0 is curIndex == initialIndex
         var s = new List<StackEntry>(poly.Points.Count);
         var isZVertex = true;
         var n = isZVertex ? windingOffsets.Length - 1 : windingOffsets.Length;

         var state = windingOffsets[it.NextWindingIndex] >= windingOffsets[it.CurWindingIndex] ? Vpstate.Advance : Vpstate.Scan;
         var stateScanWindowEnd = default(WindowEnd);
         var stateScanCcw = false;

         void Render(int iteration) {
            var canvas = dmch.CreateAndAddCanvas();
            canvas.BatchDraw(() => {
               canvas.DrawPolygon(poly.Points, StrokeStyle.BlackHairLineSolid);
               canvas.DrawPolygon(s.Map(x => x.Cartesian), StrokeStyle.OrangeThick10Solid);

               if (s.Count > 3) {
                  canvas.FillPolygon(s.Map(x => x.Cartesian), new FillStyle(Color.FromArgb(100, 255, 255, 0)));
               }

               canvas.DrawLine(
                  v0,
                  (v0 + v0.To(s[^1].Cartesian).ToUnit() * 1000),
                  StrokeStyle.CyanHairLineSolid);
               
               canvas.DrawPoint(it.Cur, StrokeStyle.LimeThick35Solid);
               
               for (var i = 0; i < s.Count; i++) {
                  var x = s[i];
                  canvas.DrawPoint(x.Cartesian, StrokeStyle.RedThick25Solid);
                  var debugText = $"s[{i}], wo {s[i].WindingOffset:F2}";

                  if (x.WindowEndpointData != null) {
                     var windowEndpointData = x.WindowEndpointData.Value;
                     var reflexVertex = poly.Points[windowEndpointData.WindowReflexVertexIndex].ToDoubleVector2();

                     // Note, seg[T] should be the same as x.Cartesian (w/ maybe some fp error).
                     var seg = new DoubleLineSegment2(
                        poly.Points[windowEndpointData.WindowSegmentFirstIndex].ToDoubleVector2(),
                        poly.Points[(windowEndpointData.WindowSegmentFirstIndex + 1) % poly.Points.Count].ToDoubleVector2());
                     canvas.DrawLine(seg.PointAt(windowEndpointData.WindowSegmentT), reflexVertex, StrokeStyle.MagentaThick10Solid);

                     debugText += $"\nwsfi {windowEndpointData.WindowSegmentFirstIndex} wst {windowEndpointData.WindowSegmentT:F2}";
                  }

                  canvas.DrawText(debugText, x.Cartesian.ToDotNetVector() + new Vector2(-20, 0));
               }

               var it2 = new ForwardPolygonIterator(poly, initialIndex);
               for (var i = 0; i <= poly.Points.Count; i++, it2.Step()) {
                  var p = it2.Cur.ToDotNetVector();
                  if (i == poly.Points.Count) {
                     p -= new Vector2(0, 11); //
                  } else {
                     // this is only true for initialOffset == 0
                     // Assert.Equals(i, it2.CurIndex);
                  }

                  var vertexIndex = it2.CurVertexIndex;
                  canvas.DrawText($"{i} ({vertexIndex})\n{windingOffsets[i]:F2}\n<{it2.Cur.X:F2}, {it2.Cur.Y:F2}>", p);
                  // canvas.DrawLine(l[i].Cartesian, l[i].Cartesian + RayDirectionFromPoint(l[i]) * 30, StrokeStyle.LimeHairLineSolid);
               }

               if (state == Vpstate.Scan) {
                  if (stateScanWindowEnd.IsRayElsePointEndpoint) {
                     var ro = s[^1].Cartesian;
                     var rd = stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint;
                     canvas.DrawLine(ro, ro + rd.ToUnit() * 1000, StrokeStyle.LimeThick5Solid);
                  } else {
                     var endpoint = stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint;
                     canvas.DrawLine(s[^1].Cartesian, endpoint, StrokeStyle.LimeThick5Solid);
                  }
               }

               var bounds = ComputePolyPointsBounds();
               canvas.DrawText($"it={iteration}, v0={initialIndex} @({it.PrevWindingIndex} => {it.CurWindingIndex} => {it.NextWindingIndex}) {state}", new Vector2(-10, -30) + bounds.Center.ToDotNetVector());
            });
         }

         s.Add(new StackEntry {
            Cartesian = it.Cur.ToDoubleVector2(),
            WindingOffset = windingOffsets[it.CurWindingIndex],
            PointIndex = it.CurVertexIndex,
         });

         // paper defines this degenerately in the case where z is v0.
         // i presume a traversal ordering of zv0v1v2v3..vnzv0...,
         // with z on vn-v0, thus z-v0 is vn-v0
         //
         // Edit: on second thought I think v0v1 makes more sense in the context of the algorithm.
         // We haven't observed vn-v0 midway through the algorithm. Where we use zv0, we are slicing
         // a visibility segment that surpasses 2pi winding (meaning, it'd cross our original winding=0 line),
         // which is delineated by the line v0v1.
         var zv0 = new DoubleLineSegment2(
            it.Cur.ToDoubleVector2(),
            it.Next.ToDoubleVector2());

         for (var iteration = 0;; iteration++) {
            if (iteration == 50) break;
            if (debugDrawMode == DebugDrawMode.Steps) {
               Render(iteration);
            }

            if (kEnableDebugPrint) Console.WriteLine($"{initialIndex}, it {iteration}: {it.PrevVertexIndex}/{it.PrevWindingIndex} => {it.CurVertexIndex}/{it.CurWindingIndex} => {it.NextVertexIndex}/{it.NextWindingIndex}: {state} {stateScanCcw} {stateScanWindowEnd.IsRayElsePointEndpoint} {stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint}");

            if (it.IndexOffset > it.NumPoints) {
               Assert.Equals(Vpstate.Finish, state);
            }

            switch (state) {
               case Vpstate.Advance: {
                  if (windingOffsets[it.NextWindingIndex] <= TWO_PI) {
                     // step so point to add is the current point.
                     it.Step();
                     if (kEnableDebugPrint) Console.WriteLine($"STEPPED {it.PrevVertexIndex}/{it.PrevWindingIndex} => {it.CurVertexIndex}/{it.CurWindingIndex} => {it.NextVertexIndex}/{it.NextWindingIndex}, n = {n}: {state} {stateScanCcw} {stateScanWindowEnd.IsRayElsePointEndpoint} {stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint}");

                     // add current point to stack. handle the degenerate collinear case where 
                     // the prior stack entry is a window vertex at our exact point, by merging the
                     // point-to-add with that window vertex.
                     {
                        var lastStackEntry = s[^1]; // scoped to not be accessible later, as wrong after else case.
                        if (lastStackEntry.WindowEndpointData.HasValue &&
                            (lastStackEntry.Cartesian - it.Cur.ToDoubleVector2()).SquaredNorm2D() < EPSILON) {
                           lastStackEntry.Cartesian = it.Cur.ToDoubleVector2();
                           lastStackEntry.WindingOffset = windingOffsets[it.CurWindingIndex];
                           lastStackEntry.PointIndex = it.CurVertexIndex;
                           s[^1] = lastStackEntry;
                        } else {
                           s.Add(new StackEntry {
                              Cartesian = it.Cur.ToDoubleVector2(),
                              WindingOffset = windingOffsets[it.CurWindingIndex],
                              PointIndex = it.CurVertexIndex,
                           });
                        }
                     }

                     // transition to finish if done iterating contour
                     if (it.IndexOffset == n) {
                        state = Vpstate.Finish;
                        break;
                     }

                     var nextWindingOffset = windingOffsets[it.NextWindingIndex];
                     var currentWindingOffset = windingOffsets[it.CurWindingIndex];
                     
                     // Presume that if winding offsets are within epsilon, we are observing collinear points passing through v0.
                     // In this case, we want to ADVANCE to process future points, so the collinear points appear in the polygon output.
                     var isNextPointWindingRegressed = nextWindingOffset < currentWindingOffset && !WithinEpsilon(nextWindingOffset, currentWindingOffset);

                     // Clk only matters in the case where we have a winding regression. Because collinear points (where fuzziness in winding offset
                     // makes clockness useless) are not considered regressed, clockness can be considered numerically robust.
                     var clk = it.ClocknessPrevCurNext;

                     if (kEnableDebugPrint) Console.WriteLine($"CLK {clk} WR {isNextPointWindingRegressed}; woc = {windingOffsets[it.CurWindingIndex]}, won = {windingOffsets[it.NextWindingIndex]}");

                     if (isNextPointWindingRegressed && clk == Clockness.CounterClockWise) {
                        if (kEnableDebugPrint) Console.WriteLine(".. adv 1 => scan");
                        state = Vpstate.Scan;
                        stateScanWindowEnd = new WindowEnd {
                           IsRayElsePointEndpoint = true,
                           DirectionFromStackTopIfWindowIsRayElsePointEndpoint = v0.To(it.Cur.ToDoubleVector2()),
                           WindowReflexVertexIndex = it.CurVertexIndex,
                        };
                        stateScanCcw = true; // ccw := true
                     } else if (isNextPointWindingRegressed && clk == Clockness.ClockWise) {
                        if (kEnableDebugPrint) Console.WriteLine(".. adv 1 => retard");
                        state = Vpstate.Retard;
                     } else {
                        if (kEnableDebugPrint) Console.WriteLine(".. adv 1 => adv");
                        state = Vpstate.Advance;
                     }
                  } else {
                     if (kEnableDebugPrint) Console.WriteLine($".. adv 2 {s[^1].WindingOffset}");

                     // the next point has winding > 2pi, the prior is either <2pi or on 2pi.
                     if (s[^1].WindingOffset < TWO_PI) {
                        // In the case where the prior point is <2pi in the vispoly, we'll want to update
                        // the vispoly to extend to 2pi. Find intersection with zv0 (initial segment of
                        // vispoly, aka a line going in the direction of winding offset 0 / 2pi winding line).
                        var seg = new DoubleLineSegment2(it.Cur.ToDoubleVector2(), it.Next.ToDoubleVector2());
                        var intersect = GeometryOperations.TryFindNonoverlappingSegmentLineIntersectionT(
                           seg,
                           zv0,
                           out double tForSeg);

                        // If the two points are within epsilon in terms of winding, they're collinear w/
                        // v0 / zv0. In this case, intersection is nonreliable. We want to treat this
                        // as a "no intersection" case & add the next point to the stack.
                        intersect |= WithinEpsilon(windingOffsets[it.CurWindingIndex], windingOffsets[it.NextWindingIndex]);

                        // There's a chance of no intersect. This happens in the case where we're intersecting
                        // zv0 with a line parallel to it. We would still like to retain the contour point we're
                        // advancing to, so we will consider it the intersection point & add it to stack.
                        DoubleVector2 intersectionPoint;
                        if (intersect) {
                           intersectionPoint = seg.PointAt(tForSeg);
                        } else {
                           intersectionPoint = it.Next.ToDoubleVector2();
                           tForSeg = 1;
                        }

                        if (kEnableDebugPrint) Console.WriteLine($".. adv 2 INTERSEC {intersect}");
                        s.Add(new StackEntry {
                           Cartesian = intersectionPoint,
                           WindingOffset = ComputeWindingOffset(
                              windingOffsets[it.CurWindingIndex],
                              v0,
                              it.Cur.ToDoubleVector2(),
                              intersectionPoint),
                           PointIndex = null,
                           WindowEndpointData = new WindowEndpointData {
                              // Technically this is correct? In practice this value should never actually
                              // be output by the system, as we should expect a RETARD event to eventually
                              // remove this stack entry. The only case where we should expect this output
                              // is in a degenerate input, where vnv0v1 and form angle 0.
                              WindowReflexVertexIndex = 0,

                              WindowSegmentFirstIndex = it.CurVertexIndex,
                              WindowSegmentT = tForSeg,
                           },
                        });
                     }

                     if (kEnableDebugPrint) Console.WriteLine($".. adv 2 => scan v0");

                     // Since out next point is beyond 2pi (note we haven't advanced the contour iterator), transition
                     // to scan mode. Note the window at wihch we'll exit scanning is at winding = 2pi.
                     state = Vpstate.Scan; // ccw := false, w := v0
                     stateScanWindowEnd = new WindowEnd {
                        IsRayElsePointEndpoint = false,
                        DirectionFromStackTopIfWindowIsRayElsePointEndpoint = v0,
                        WindowReflexVertexIndex = initialIndex,
                     };
                     stateScanCcw = false;
                  }

                  break;
               }
               case Vpstate.Scan: {
                  it.Step();

                  var alphaNext = windingOffsets[it.NextWindingIndex];
                  var alphaStackTop = s[^1].WindingOffset;
                  var alphaCur = windingOffsets[it.CurWindingIndex];

                  //we transition into advance if we pass or are collinear with the window.
                  var cond1 = stateScanCcw && (alphaNext > alphaStackTop || WithinEpsilon(alphaNext, alphaStackTop)) && (alphaStackTop > alphaCur);

                  // transition into retard
                  var cond2 = !stateScanCcw && alphaNext <= alphaStackTop && alphaStackTop < alphaCur;
                  if (kEnableDebugPrint) Console.WriteLine($"SCAN STEPPED {it.PrevVertexIndex}/{it.PrevWindingIndex} => {it.CurVertexIndex}/{it.CurWindingIndex} => {it.NextVertexIndex}/{it.NextWindingIndex}: {alphaNext} {alphaStackTop} {alphaCur} {cond1} {cond2}");


                  if (!cond1 && !cond2) break;

                  var sega = new DoubleLineSegment2(it.Cur.ToDoubleVector2(), it.Next.ToDoubleVector2());
                  
                  bool intersec;
                  DoubleVector2 intersectionPoint;
                  double tForSega;
                  if (stateScanWindowEnd.IsRayElsePointEndpoint) {
                     var directionFromStackTop = stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint;
                     var ray = new DoubleLineSegment2(s[^1].Cartesian, s[^1].Cartesian + directionFromStackTop);
                     intersec = GeometryOperations.TryFindNonoverlappingLineLineIntersectionEx(sega, ray, out tForSega, out var tForRay) &&
                                tForSega >= 0 && tForSega <= 1 &&
                                tForRay >= 0;
                     intersectionPoint = sega.PointAt(tForSega);
                  } else {
                     var windowEndpoint = stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint;
                     var segb = new DoubleLineSegment2(s[^1].Cartesian, windowEndpoint);
                     intersec = GeometryOperations.TryFindNonoverlappingLineLineIntersectionEx(sega, segb, out tForSega, out var tForSegb) &&
                                tForSega >= 0 && tForSega <= 1 &&
                                tForSegb >= 0 && tForSegb <= 1;
                     intersectionPoint = sega.PointAt(tForSega);
                  }

                  if (cond1 && intersec) {
                     s.Add(new StackEntry {
                        Cartesian = intersectionPoint,
                        WindingOffset = ComputeWindingOffset(windingOffsets[it.CurWindingIndex], v0, it.Cur.ToDoubleVector2(), intersectionPoint),
                        PointIndex = null,
                        WindowEndpointData = new WindowEndpointData {
                           WindowReflexVertexIndex = stateScanWindowEnd.WindowReflexVertexIndex,
                           WindowSegmentFirstIndex = it.CurVertexIndex,
                           WindowSegmentT = tForSega,
                        }
                     });
                     state = Vpstate.Advance;
                     stateScanWindowEnd = default;
                     stateScanCcw = default;
                  } else if (cond2 && intersec) {
                     state = Vpstate.Retard;
                     stateScanWindowEnd = default;
                     stateScanCcw = default;
                  }

                  break;
               }
               case Vpstate.Retard: {
                  // scan stack backwards for first vertex sj such that either:
                  // (a) α(s_j) < α(vnext) <= α(s_j+1) or
                  // (b) α(vnext) <= α(s_j) == α(s_j+1) and vcur-vnext intersects sj-sj+1
                  //
                  // Case (a) is our trivial case: we are regressing our contour as our new point/edge
                  // occludes (note: is in front of) our existing vispoly. Note: the paper is bugged here.
                  // case (a) only makes sense if it's for slicing occluded segments in vispoly.it's
                  // nonsensical to slice segments we are behind.
                  //
                  // Case (b) handles partially vispoly segments that are collinear with v0. In this case,
                  // one of the endpoints in the vispoly will be occluded & replaced, while the other will
                  // be kept.
                  int j = -1337;
                  var jCaseAJPlus1Occluded = false;
                  for (var candidateJ = s.Count - 2; candidateJ >= 0; candidateJ--) {
                     var alphaSj = s[candidateJ].WindingOffset;
                     var alphaSjnext = s[candidateJ + 1].WindingOffset;
                     var alphaVnext = windingOffsets[it.NextWindingIndex];

                     // Note: this is the clockness between a candidate segment & the new point -- which side
                     // of the segment the point is on. This is useful for case (A) where the new point is
                     // overlapping in winding with an existing segment, at which point clockness can determine
                     // whether the new point is in front of or behind the segment.
                     //
                     // This is NOT immediately useful for case (B) where the new point has lower winding than an existing
                     // segment which is collinear with v0. Here, there is ambiguity in which endpoint of that
                     // segment is nearer or further, or what a front-facing clockness is. (If the segment enters a window
                     // moving toward v0, vs moving away from it, then the clockness to occluding vnext is opposite)
                     //
                     // Likewise, this isn't very useful for case (c), as that can have the same issues as case (B) with
                     // windows.
                     var clk = GeometryOperations.Clockness(
                        s[candidateJ].Cartesian,
                        s[candidateJ + 1].Cartesian,
                        it.Next.ToDoubleVector2());

                     // Instead, for cases (b) and (c), we want to know whether occluded points are on the side of v0
                     // or the opposite WRT line vcur-vnext. v0 is on the CCW side as we retard on back-facing segments.
                     var clk2 = GeometryOperations.Clockness(
                        it.Cur.ToDoubleVector2(),
                        it.Next.ToDoubleVector2(),
                        s[candidateJ].Cartesian);

                     if (kEnableDebugPrint) Console.WriteLine($"RETARDING j={candidateJ}, α(s_j)={alphaSj}, α(s_j+1)={alphaSjnext} α(vnext)={alphaVnext} clk={clk}");

                     var shouldContinueScanningStack = false;

                     // Case A: the new point (segment) occludes part of a segment in the existing vispoly
                     //
                     // Note: The new point should occlude s[^2]-s[^1], but not s[^2]-s[^3].
                     // so that we can retain source poly vertices.
                     //
                     // Similarly, if alphaVnext & alphaSjNext within epsilon, we'd keep both vertices.
                     //
                     // * v1                |
                     // ^                   v
                     // | v0    vn+1  s[^2] | s[^3]    in this diagram the vispoly expands from v0..s[^3], s[^2], s[^1].
                     // *---------*   *-----*
                     //            \ / sj
                     //             * vn, s[^1], sj+1
                     //
                     // It might be cleaner to simplify this to a "overlaps vj" case vs "overlaps vj-vjnext"
                     if ((alphaSj < alphaVnext || WithinEpsilon(alphaSj, alphaVnext)) && (alphaVnext < alphaSjnext && !WithinEpsilon(alphaVnext, alphaSjnext))) {
                        // NOTE: The paper doesn't perform this check. Without this check, observed points
                        // will "cut" / occlude the segments in front of them, which is broken! To observe 
                        // this, run on vertex 0 of my squiddy test polygon.
                        var nextIsInFrontOfJSegment = clk == Clockness.ClockWise;
                        if (kEnableDebugPrint) Console.WriteLine("cond (a) " + nextIsInFrontOfJSegment);
                        if (nextIsInFrontOfJSegment) {
                           j = candidateJ;
                           jCaseAJPlus1Occluded = true;
                           shouldContinueScanningStack = true;
                        }
                     }

                     // only keep scanning if the point is in front of stack top w/ lower winding or cond a/b
                     // https://imgur.com/a/beAUx6o, note this is tolerant to collinearity too https://imgur.com/a/4EJhibK
                     // this isn't documented in the article, but this is case (c): the entire segment gets wiped away
                     // by a lower winding number.
                     if (!shouldContinueScanningStack && (alphaSj > alphaVnext && !WithinEpsilon(alphaSj, alphaVnext) && clk2 == Clockness.ClockWise)) {
                        if (kEnableDebugPrint) Console.WriteLine("cond (c) aka (a) II");
                        j = candidateJ;
                        jCaseAJPlus1Occluded = true;
                        shouldContinueScanningStack = true;
                     }

                     // Case B: Cutting a near-to-far window. See https://imgur.com/a/EHQAIgH
                     //  s2
                     //   *-----* q
                     //   |  .-'
                     // s | *rt----------- 
                     //   *-----* p
                     //  s1     |
                     //         *------*
                     //                v0
                     //
                     // Note, during the clockwise scan from v0..p..s...q, v0 will see a window with
                     // endpoint p and some additional endpoint w' on s, with the vispoly continuing to s2, q.
                     //
                     // When we observe r, that "cuts" the window from p to w', occluding w'.
                     // We then enter SCAN mode, until we add a point t, a new endpoint forming a window with p.
                     // Let's refer to this as case b II.
                     //
                     // Note: we can also enter ADVANCE mode if r is ON the window (at which point we replace w' with r).
                     // Let's refer to that as case b I.
                     if (!shouldContinueScanningStack && (alphaVnext < alphaSj && !WithinEpsilon(alphaVnext, alphaSj)) && WithinEpsilon(alphaSj, alphaSjnext)) {
                        // TODO: vivinext intersects sj sj+1
                        var sega = new DoubleLineSegment2(s[candidateJ].Cartesian, s[candidateJ + 1].Cartesian);
                        var segb = new DoubleLineSegment2(it.Cur.ToDoubleVector2(), it.Next.ToDoubleVector2());
                        var intersect = GeometryOperations.TryFindSegmentSegmentIntersection(
                           ref sega,
                           ref segb,
                           out var intersectionPoint);

                        if (kEnableDebugPrint) Console.WriteLine("cond (b) " + intersect);
                        if (intersect) {
                           j = candidateJ;
                           jCaseAJPlus1Occluded = false;
                           shouldContinueScanningStack = true;
                        }
                     }

                     if (!shouldContinueScanningStack) break;
                  }
                  Assert.NotEquals(-1337, j);

                  // Note: Explicitly store whether j is for case a vs b, rather than rederiving here
                  // like done in paper.
                  if (jCaseAJPlus1Occluded) {
                     it.Step();

                     // t := j+1. Note t is last valid index in s.
                     while (s.Count - 1 != j + 1) {
                        s.RemoveAt(s.Count - 1);
                     }

                     Assert.Equals(j + 1, s.Count - 1);

                     // Note: The paper will do an intersection between s[j]-s[j+1] and z-vcur
                     // This is correct behavior, but makes it hard to derive tForSeg (as s[j]-s[j+1] might
                     // just be a subsegment). As we are occluding s[j]-s[j+1], it MUST be the case that
                     // we are also occluding its weak super-segment (weak as seg might be that super-segment).
                     // 
                     // Note, it is possible that BOTH endpoints are window endpoints. In this and all cases,
                     // they are guaranteed to be on the same segment. It is likewise possible that only one of
                     // s[j] or s[j+1] is a window endpoint. Regardless, s[j] is always the index of the segment
                     // that we care about intersecting. 
                     //
                     // One <could> assert that our intersection point is between the parameterized t of the
                     // endpoints (subject to floating point error), though I've not implemented that for now.
                     //
                     // Alternatively, one can intersect with s[j]-s[j+1], then scale tForSeg of that subsegment
                     // to compute the true tForSeg of the supersegment. This is the approach I've taken.
                     var seg = new DoubleLineSegment2(s[j].Cartesian, s[j + 1].Cartesian);
                     var line = new DoubleLineSegment2(z, it.Cur.ToDoubleVector2());
                     var intersec = GeometryOperations.TryFindNonoverlappingSegmentLineIntersectionT(seg, line, out var tForSeg);
                     Assert.IsTrue(intersec);

                     var intersectionPoint = seg.PointAt(tForSeg);
                     
                     // Adjust tForSeg to be on the supersegment of seg, constructed from 2 vertices in poly.
                     // If seg is already that segment, then do nothing.
                     if (s[j].WindowEndpointData != null || s[j + 1].WindowEndpointData != null) {
                        var trueSegTInitial = s[j].WindowEndpointData?.WindowSegmentT ?? 0;
                        var trueSegTFinal = s[j + 1].WindowEndpointData?.WindowSegmentT ?? 1;
                        tForSeg = trueSegTInitial + tForSeg * (trueSegTFinal - trueSegTInitial);
                     }

                     // Normally we'd push a window endpoint & push the current endpoint onto stack.
                     // In the case where we have collinear points, the window endpoint would overlap
                     // with an existing vertex, so handle that case specially & update the existing
                     // stack element instead.
                     //
                     // Here, for example, we'll visit v0...s[^3], s[^2], s[^1], q, vn.
                     // Case (a) will normally cover s[^2]-s[^1], introducing a window from q to some
                     // endpoint on line v0-q intersecting s[^2]-s[^1]. Normally, this is in the middle
                     // of the second segment, though in the collinear degenerate case this middle point
                     // is s[^2]. We update s[^2] with data about the window endpoint rather than
                     // introducing a new point after s[^2] at the same position.
                     // 
                     // |
                     // |     q  s[^2]
                     // *-*---*   *--*
                     //   v0   \ /   s[^3]
                     //         *s[^1]
                     var windowEndpointData = new WindowEndpointData {
                        WindowReflexVertexIndex = it.CurVertexIndex,
                        WindowSegmentFirstIndex = s[j].PointIndex ?? s[j].WindowEndpointData.Value.WindowSegmentFirstIndex,
                        WindowSegmentT = tForSeg,
                     };
                     if ((s[^2].Cartesian - intersectionPoint).SquaredNorm2D() < EPSILON) {
                        s.RemoveAt(s.Count - 1);

                        var lastEntry = s[^1];
                        lastEntry.WindowEndpointData = windowEndpointData;
                        s[^1] = lastEntry;
                     } else {
                        s[^1] = new StackEntry {
                           Cartesian = intersectionPoint,
                           WindingOffset = ComputeWindingOffset(
                              s[^1].WindingOffset,
                              v0,
                              s[^1].Cartesian,
                              intersectionPoint),
                           PointIndex = null,
                           WindowEndpointData = windowEndpointData,
                        };
                     }

                     s.Add(new StackEntry {
                        Cartesian = it.Cur.ToDoubleVector2(),
                        WindingOffset = windingOffsets[it.CurWindingIndex],
                        PointIndex = it.CurVertexIndex,
                     });

                     if (it.IndexOffset == n) {
                        state = Vpstate.Finish;
                        break;
                     }

                     var alphaVnext = windingOffsets[it.NextWindingIndex];
                     var alphaVcur = windingOffsets[it.CurWindingIndex];
                     var clk = it.ClocknessPrevCurNext;
                     if (alphaVnext >= alphaVcur && clk == Clockness.CounterClockWise) {
                        state = Vpstate.Advance;
                     } else if (alphaVnext > alphaVcur && clk == Clockness.ClockWise) {
                        state = Vpstate.Scan;
                        stateScanCcw = false;
                        stateScanWindowEnd = new WindowEnd {
                           IsRayElsePointEndpoint = false,
                           DirectionFromStackTopIfWindowIsRayElsePointEndpoint = it.Cur.ToDoubleVector2(),
                           WindowReflexVertexIndex = it.CurVertexIndex,
                        };
                        s.RemoveAt(s.Count - 1);
                     } else {
                        s.RemoveAt(s.Count - 1);
                     }

                     // note from paper:
                     // remain at retard if alphaVnext < alphaVcur or
                     //   alphaVnext = alphaVcur & r(vnext) > r(vcur)
                  } else {
                     // case (b) I
                     if (WithinEpsilon(windingOffsets[it.NextWindingIndex], s[j].WindingOffset) &&
                         windingOffsets[it.NextNextWindingIndex] > windingOffsets[it.NextWindingIndex] &&
                         it.ClocknessCurNext_NextNext == Clockness.CounterClockWise) {
                        state = Vpstate.Advance;
                        it.Step();

                        // t := j+1. Note t is last valid index in s.
                        while (s.Count - 1 != j + 1) {
                           s.RemoveAt(s.Count - 1);
                        }

                        Assert.Equals(j + 1, s.Count - 1);

                        s[^1] = new StackEntry {
                           Cartesian = it.Cur.ToDoubleVector2(),
                           WindingOffset = windingOffsets[it.CurWindingIndex],
                           PointIndex = it.CurVertexIndex,
                        };
                     } else {
                        // b II
                        state = Vpstate.Scan;

                        stateScanCcw = true;
                        var intersec = GeometryOperations.TryFindLineLineIntersection(
                           it.Cur.ToDoubleVector2(),
                           it.Next.ToDoubleVector2(),
                           s[j].Cartesian,
                           s[j + 1].Cartesian,
                           out var intersectionPoint);
                        Assert.IsTrue(intersec);

                        // t := j. Note t is last valid index in s.
                        // Note this line is AFTER the above, since it accesses j+1...
                        while (s.Count - 1 != j) {
                           s.RemoveAt(s.Count - 1);
                        }
                        Assert.Equals(j, s.Count - 1);

                        stateScanWindowEnd = new WindowEnd {
                           IsRayElsePointEndpoint = false,
                           DirectionFromStackTopIfWindowIsRayElsePointEndpoint = intersectionPoint,
                           WindowReflexVertexIndex = s[^1].PointIndex.Value,
                        };
                     }
                  }

                  break;
               }
               case Vpstate.Finish: {
                  goto done;
                  break;
               }
            }
         }

         done:
         if (debugDrawMode == DebugDrawMode.Result) {
            Render(-1337);
         }

         var windows = new List<VisibilityPolygonWindow>();
         for (var i = 0; i < s.Count; i++) {
            var cur = s[i];
            if (cur.WindowEndpointData.HasValue) {
               windows.Add(new VisibilityPolygonWindow {
                  EndpointPolygonPointIndexOrNull = cur.PointIndex,
                  Endpoint = cur.Cartesian,
                  ReflexVertexIndex = cur.WindowEndpointData.Value.WindowReflexVertexIndex,
                  SegmentFirstPointIndex = cur.WindowEndpointData.Value.WindowSegmentFirstIndex,
                  WindowSegmentT = (float)cur.WindowEndpointData.Value.WindowSegmentT,
               });
            }
         }
         return windows;
      }

      public class ForwardPolygonIterator {
         private readonly List<IntVector2> points;
         private readonly int initialIndex;
         private int prevVertexIndex, curVertexIndex, nextVertexIndex;
         private int prevWindingIndex, curWindingIndex, nextWindingIndex;
         private int indexOffset;

         public ForwardPolygonIterator(Polygon2 p, int initialIndex) {
            this.points = p.Points;
            this.initialIndex = initialIndex;
            Assert.IsFalse(p.IsClosed);

            ResetToInitialIndex();
         }

         public void ResetToInitialIndex() {
            prevVertexIndex = initialIndex == 0 ? points.Count - 1 : initialIndex - 1;
            curVertexIndex = initialIndex;
            nextVertexIndex = initialIndex == points.Count - 1 ? 0 : initialIndex + 1;

            prevWindingIndex = -1; // should never be observed by algo
            curWindingIndex = 0;
            nextWindingIndex = 1;
            
            indexOffset = 0;
         }

         /// <summary>
         /// Returns 0 if we've stepped back to the first offset in our polygon view.
         /// </summary>
         public bool Step() {
            (prevVertexIndex, curVertexIndex) = (curVertexIndex, nextVertexIndex);
            nextVertexIndex = curVertexIndex == points.Count - 1 ? 0 : curVertexIndex + 1;

            prevWindingIndex++;
            curWindingIndex++;
            nextWindingIndex++;

            indexOffset++;
            return indexOffset % points.Count == 0;
         }

         public int NumPoints => points.Count;
         public int IndexOffset => indexOffset;
         public IntVector2 Prev => points[prevVertexIndex];
         public IntVector2 Cur => points[curVertexIndex];
         public IntVector2 Next => points[nextVertexIndex];
         public int PrevVertexIndex => prevVertexIndex;
         public int CurVertexIndex => curVertexIndex;
         public int NextVertexIndex => nextVertexIndex;
         public int PrevWindingIndex => prevWindingIndex;
         public int CurWindingIndex => curWindingIndex;
         public int NextWindingIndex => nextWindingIndex; // WindingOffset array doesn't wrap & defines last point as first point (closed contour)
         public int NextNextWindingIndex => nextWindingIndex + 1; // Is this even a valid index? Do we need to compute 1 past endpoint? Not sure, but required by paper.
         public Clockness ClocknessPrevCurNext => GeometryOperations.Clockness(Prev, Cur, Next);
         public Clockness ClocknessCurNext_NextNext => GeometryOperations.Clockness(Cur, Next, points[nextVertexIndex + 1]); // see NextNextWindingIndex... can this out of bounds??
      }

      public enum Vpstate {
         Advance,
         Scan,
         Retard,
         Finish,
      }
   }
}
