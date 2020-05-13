using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;

namespace Dargon.Terragami.Tests {
   public class VisibilityPolygonOfSimplePolygonsTests {
      public const float TWO_PI = MathF.PI * 2;
      private const bool kEnableDebugPrint = true;

      public struct StackEntry {
         public DoubleVector2 Cartesian;
         public float WindingOffset;
      }

      public struct WindowEnd {
         public bool IsRayElsePointEndpoint;
         public DoubleVector2 DirectionFromStackTopIfWindowIsRayElsePointEndpoint;
      }

      public float ComputeWindingOffset(float prevWindingOffset, DoubleVector2 v0, DoubleVector2 prev, DoubleVector2 cur) {
         var clk = GeometryOperations.Clockness(v0, prev, cur); // clockwise is positive.
         var angle = MathUtils.SignedAngleBetweenVectorsF(
            v0.To(prev),
            v0.To(cur));
         angle = Math.Abs(angle);
         angle = Math.Min(angle, TWO_PI - angle); // unnecessary due to atan2 range?
         return prevWindingOffset + angle * (int)clk;
      }

      public void Execute() {
         // Compute(Polygon2.CreateRect(0, 0, 200, 200), new IntVector2(0, 0));
         // var poly = Polygon2.CreateCircle(0, 0, 200, n: 8);
         // poly.Points[6] = new IntVector2(10, 30);

         var poly = new Polygon2(new List<IntVector2> {
            new IntVector2(165, 326),
            new IntVector2(191, 300),
            new IntVector2(238, 300),
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
            new IntVector2(200, 225),
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
         // poly.Visualize(labelIndices: true);

         // var initialIndex = 0;
         ComputeVisibilityPolygon(poly, 1, null, DebugDrawMode.Steps);
         return;

         var dmch = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
            AxisAlignedBoundingBox2.BoundingPoints(poly.Points.ToArray()),
            new Size(2350, 1200),
            new Point(50, 50));
         
         for (var initialIndex = 0; initialIndex < poly.Points.Count; initialIndex++) {
            ComputeVisibilityPolygon(poly, initialIndex, dmch, DebugDrawMode.Result);
         }
      }

      enum DebugDrawMode {
         None,
         Steps,
         Result,
      }

      public const float EPSILON = 1E-5f;
      public const float NEGATIVE_EPSILON = -EPSILON;

      public bool WithinEpsilon(float a, float b) {
         var c = a - b;
         return c > NEGATIVE_EPSILON & c < EPSILON;
      }

      private void ComputeVisibilityPolygon(Polygon2 poly, int initialIndex, DebugMultiCanvasHost dmch = null, DebugDrawMode debugDrawMode = DebugDrawMode.None) {
         // With ForwardPolygonIterator, we now observe poly as if v0 is what was at poly[initialIndex].
         // This means windingOffset[0] corresponds to v0.
         var it = new ForwardPolygonIterator(poly, initialIndex);
         var v0 = it.Cur.ToDoubleVector2();
         var z = v0;
         var windingOffsets = new float[it.NumPoints + 1];
         for (var i = 0; i < windingOffsets.Length; i++, it.Step()) {
            if (i == 0) {
               windingOffsets[i] = 0; // if z on boundary(P), 
            } else {
               windingOffsets[i] = ComputeWindingOffset(
                  windingOffsets[i - 1],
                  v0,
                  it.Prev.ToDoubleVector2(),
                  it.Cur.ToDoubleVector2());
            }
         }

         var bounds = AxisAlignedBoundingBox2.BoundingPoints(poly.Points.ToArray());
         if (debugDrawMode != DebugDrawMode.None) {
            dmch ??= SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
               bounds,
               new Size(2350, 1200),
               new Point(50, 50));
         }

         it.ResetToInitialIndex(); // i := 0 is curIndex == initialIndex
         var s = new List<StackEntry>();
         var isZVertex = true;
         var n = isZVertex ? windingOffsets.Length - 1 : windingOffsets.Length;

         var state = windingOffsets[it.NextWindingIndex] >= windingOffsets[it.CurWindingIndex] ? Vpstate.Advance : Vpstate.Scan;
         var stateScanWindowEnd = default(WindowEnd);
         var stateScanCcw = false;

         void Render() {
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
               
               canvas.DrawPoint(it.Cur, StrokeStyle.LimeThick25Solid);

               for (var i = 0; i < s.Count; i++) {
                  var x = s[i];
                  canvas.DrawPoint(x.Cartesian, StrokeStyle.RedThick10Solid);
                  canvas.DrawText($"s[{i}], wo {s[i].WindingOffset:F2}", x.Cartesian.ToDotNetVector() + new Vector2(-20, 0));
               }

               var it2 = new ForwardPolygonIterator(poly, initialIndex);
               for (var i = 0; i <= poly.Points.Count; i++, it2.Step()) {
                  var p = it2.Cur.ToDotNetVector();
                  if (i == poly.Points.Count) {
                     p -= new Vector2(0, 40);
                  } else {
                     // this is only true for initialOffset == 0
                     // Assert.Equals(i, it2.CurIndex);
                  }

                  var windingIndex = it2.CurWindingIndex;
                  canvas.DrawText($"{i} ({windingIndex})\n{windingOffsets[windingIndex]:F2}\n<{it2.Cur.X:F2}, {it2.Cur.Y:F2}>", p);
                  // canvas.DrawLine(l[i].Cartesian, l[i].Cartesian + RayDirectionFromPoint(l[i]) * 30, StrokeStyle.LimeHairLineSolid);
               }

               canvas.DrawText($"{initialIndex} @({it.PrevWindingIndex} => {it.CurWindingIndex} => {it.NextWindingIndex}) {state}", new Vector2(-10, -30) + bounds.Center.ToDotNetVector());
            });
         }

         s.Add(new StackEntry {
            Cartesian = it.Cur.ToDoubleVector2(),
            WindingOffset = windingOffsets[it.CurWindingIndex],
         });

         // paper defines this degenerately in the case where z is v0.
         // i presume a traversal ordering of zv0v1v2v3..vnzv0...,
         // with z on vn-v0, thus z-v0 is vn-v0
         var zv0 = new DoubleLineSegment2(
            it.Prev.ToDoubleVector2(),
            it.Cur.ToDoubleVector2());

         for (var iteration = 0;; iteration++) {
            if (iteration == 50) break;
            if (debugDrawMode == DebugDrawMode.Steps) {
               Render();
            }

            if (kEnableDebugPrint) Console.WriteLine($"{initialIndex}, it {iteration}: {it.PrevVertexIndex}/{it.PrevWindingIndex} => {it.CurVertexIndex}/{it.CurWindingIndex} => {it.NextVertexIndex}/{it.NextWindingIndex}: {state} {stateScanCcw} {stateScanWindowEnd.IsRayElsePointEndpoint} {stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint}");

            if (it.IndexOffset > it.NumPoints) {
               Assert.Equals(Vpstate.Finish, state);
            }

            switch (state) {
               case Vpstate.Advance: {
                  if (windingOffsets[it.NextWindingIndex] <= TWO_PI) {
                     var stepLoop = it.Step();
                     if (kEnableDebugPrint) Console.WriteLine($"STEPPED {it.PrevVertexIndex}/{it.PrevWindingIndex} => {it.CurVertexIndex}/{it.CurWindingIndex} => {it.NextVertexIndex}/{it.NextWindingIndex}, n = {n}: {state} {stateScanCcw} {stateScanWindowEnd.IsRayElsePointEndpoint} {stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint}");

                     s.Add(new StackEntry {
                        Cartesian = it.Cur.ToDoubleVector2(),
                        WindingOffset = windingOffsets[it.CurWindingIndex],
                     });
                     if (it.IndexOffset == n) {
                        state = Vpstate.Finish;
                        break;
                     }

                     var windingRegression = windingOffsets[it.NextWindingIndex] < windingOffsets[it.CurWindingIndex];
                     var clk = it.ClocknessPrevCurNext;

                     if (kEnableDebugPrint) Console.WriteLine($"CLK {clk} WR {windingRegression}; woc = {windingOffsets[it.CurWindingIndex]}, won = {windingOffsets[it.NextWindingIndex]}");

                     if (windingRegression && clk == Clockness.CounterClockWise) {
                        if (kEnableDebugPrint) Console.WriteLine(".. adv 1 => scan");
                        state = Vpstate.Scan;
                        stateScanWindowEnd = new WindowEnd {
                           IsRayElsePointEndpoint = true,
                           DirectionFromStackTopIfWindowIsRayElsePointEndpoint = v0.To(it.Cur.ToDoubleVector2()),
                        };
                        stateScanCcw = true; // ccw := true
                     } else if (windingRegression && clk == Clockness.ClockWise) {
                        if (kEnableDebugPrint) Console.WriteLine(".. adv 1 => retard");
                        state = Vpstate.Retard;
                     } else {
                        if (kEnableDebugPrint) Console.WriteLine(".. adv 1 => adv");
                        state = Vpstate.Advance;
                     }
                  } else {
                     if (kEnableDebugPrint) Console.WriteLine($".. adv 2 {s[^1].WindingOffset}");
                     // winds away from visibility
                     if (s[^1].WindingOffset < TWO_PI) {
                        var intersect = GeometryOperations.TryFindNonoverlappingLineSegmentIntersectionT(
                           new DoubleLineSegment2(it.Cur.ToDoubleVector2(), it.Next.ToDoubleVector2()),
                           zv0,
                           out double tForRay);
                        // Assert.IsTrue(intersect);

                        // There's a chance of no intersect, e.g. 
                        if (kEnableDebugPrint) Console.WriteLine($".. adv 2 INTERSEC {intersect}");
                        if (intersect) {
                           var intersectionPoint = zv0.First + zv0.First.To(zv0.Second) * tForRay;
                           s.Add(new StackEntry {
                              Cartesian = intersectionPoint,
                              WindingOffset = ComputeWindingOffset(
                                 windingOffsets[it.CurWindingIndex],
                                 v0,
                                 it.Cur.ToDoubleVector2(),
                                 intersectionPoint)
                           });
                        }
                     }

                     if (kEnableDebugPrint) Console.WriteLine($".. adv 2 => scan v0");
                     state = Vpstate.Scan; // ccw := false, w := v0
                     stateScanWindowEnd = new WindowEnd {
                        IsRayElsePointEndpoint = false,
                        DirectionFromStackTopIfWindowIsRayElsePointEndpoint = v0,
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

                  var cond1 = stateScanCcw && alphaNext > alphaStackTop && alphaStackTop >= alphaCur;
                  var cond2 = !stateScanCcw && alphaNext <= alphaStackTop && alphaStackTop < alphaCur;
                  if (kEnableDebugPrint) Console.WriteLine($"SCAN STEPPED {it.PrevVertexIndex}/{it.PrevWindingIndex} => {it.CurVertexIndex}/{it.CurWindingIndex} => {it.NextVertexIndex}/{it.NextWindingIndex}: {alphaNext} {alphaStackTop} {alphaCur} {cond1} {cond2}");


                  if (!cond1 && !cond2) break;

                  var sega = new DoubleLineSegment2(it.Cur.ToDoubleVector2(), it.Next.ToDoubleVector2());

                  bool intersec;
                  DoubleVector2 intersectionPoint;
                  if (stateScanWindowEnd.IsRayElsePointEndpoint) {
                     var ro = s[^1].Cartesian;
                     var rd = stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint;
                     intersec = GeometryOperations.TryFindNonoverlappingRaySegmentIntersectionT(
                        ro,
                        rd,
                        sega,
                        out var tForRay
                     );
                     intersectionPoint = ro + rd * tForRay;
                  } else {
                     var segb = new DoubleLineSegment2(s[^1].Cartesian, stateScanWindowEnd.DirectionFromStackTopIfWindowIsRayElsePointEndpoint);
                     intersec = GeometryOperations.TryFindNonoverlappingSegmentSegmentIntersectionT(
                        ref sega,
                        ref segb,
                        out var tForSegA);
                     intersectionPoint = sega.First + sega.First.To(sega.Second) * tForSegA;
                  }
                  // GeometryOperations.TryFindNonoverlappingRaySegmentIntersectionT()

                  if (cond1 && intersec) {
                     s.Add(new StackEntry {
                        Cartesian = intersectionPoint,
                        WindingOffset = ComputeWindingOffset(windingOffsets[it.CurWindingIndex], v0, it.Cur.ToDoubleVector2(), intersectionPoint)
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
                  // (a) α(vnext) <= α(s_j) == α(s_j+1) and vcur-vnext intersects sj-sj+1
                  int j = -1337;
                  var jCaseA = false;
                  for (var candidateJ = s.Count - 2; candidateJ >= 0; candidateJ--) {
                     var alphaSj = s[candidateJ].WindingOffset;
                     var alphaSjnext = s[candidateJ + 1].WindingOffset;
                     var alphaVnext = windingOffsets[it.NextWindingIndex];

                     var clk = GeometryOperations.Clockness(
                        s[candidateJ].Cartesian,
                        s[candidateJ + 1].Cartesian,
                        it.Next.ToDoubleVector2());

                     if (kEnableDebugPrint) Console.WriteLine($"RETARDING j={candidateJ}, α(s_j)={alphaSj}, α(s_j+1)={alphaSjnext} α(vnext)={alphaVnext} clk={clk}");

                     var shouldContinueScanningStack = false;
                     if (alphaSj < alphaVnext && alphaVnext <= alphaSjnext) {
                        // NOTE: The paper doesn't perform this check. Without this check, observed points
                        // will "cut" / occlude the segments in front of them, which is broken! To observe 
                        // this, run on vertex 0 of my squiddy test polygon.
                        var nextIsInFrontOfJSegment = clk == Clockness.ClockWise;
                        if (kEnableDebugPrint) Console.WriteLine("cond (a) " + nextIsInFrontOfJSegment);
                        if (nextIsInFrontOfJSegment) {
                           j = candidateJ;
                           jCaseA = true;
                           shouldContinueScanningStack = true;
                        }
                        // break; // (a)
                     }

                     if (alphaVnext <= alphaSj && WithinEpsilon(alphaSj, alphaSjnext)) {
                        // TODO: vivinext intersects sj sj+1
                        if (kEnableDebugPrint) Console.WriteLine("cond (b)");
                        j = candidateJ;
                        jCaseA = false;
                        shouldContinueScanningStack = true;
                        // break; // (b), fp precision?
                     }

                     // only keep scanning if the point is in front of stack top w/ lower winding or cond a/b
                     // https://imgur.com/a/beAUx6o, note this is tolerant to collinearity too https://imgur.com/a/4EJhibK
                     if (!shouldContinueScanningStack && (alphaSj > alphaVnext || (clk != Clockness.CounterClockWise && WithinEpsilon(alphaSj, alphaVnext)))) {
                        if (kEnableDebugPrint) Console.WriteLine("cond (a) II");
                        j = candidateJ;
                        jCaseA = true;
                        shouldContinueScanningStack = true;
                     }

                     if (!shouldContinueScanningStack) break;
                  }
                  Assert.NotEquals(-1337, j);

                  // Note: Explicitly store whether j is for case a vs b, rather than rederiving here
                  // like done in paper.
                  if (jCaseA) {
                     it.Step();

                     // t := j+1. Note t is last valid index in s.
                     while (s.Count - 1 != j + 1) {
                        s.RemoveAt(s.Count - 1);
                     }

                     Assert.Equals(j + 1, s.Count - 1);

                     var line = new DoubleLineSegment2(z, it.Cur.ToDoubleVector2());
                     var intersec = GeometryOperations.TryFindNonoverlappingLineSegmentIntersectionT(
                        line,
                        new DoubleLineSegment2(s[j].Cartesian, s[j + 1].Cartesian),
                        out var tForLine);
                     Assert.IsTrue(intersec);

                     var intersectionPoint = line.PointAt(tForLine);
                     s[^1] = new StackEntry {
                        Cartesian = intersectionPoint,
                        WindingOffset = ComputeWindingOffset(
                           s[^1].WindingOffset,
                           v0,
                           s[^1].Cartesian,
                           intersectionPoint)
                     };
                     s.Add(new StackEntry {
                        Cartesian = it.Cur.ToDoubleVector2(),
                        WindingOffset = windingOffsets[it.CurWindingIndex],
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
                        };
                        s.RemoveAt(s.Count - 1);
                     } else {
                        s.RemoveAt(s.Count - 1);
                     }

                     // note from paper:
                     // remain at retard if alphaVnext < alphaVcur or
                     //   alphaVnext = alphaVcur & r(vnext) > r(vcur)
                  } else {
                     // case (b)
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
                        };
                     } else {
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
            Render();
         }
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
