using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Dviz;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using NuGet.Frameworks;
using cDouble = System.Double;

namespace Dargon.Terragami.Tests {
   public class PQE {
      public int Key;
      public List<int> InboundWindowIndices;
      public List<int> OutboundWindowIndices;
      public List<int> VerticalWindowIndices;
      public List<Node> WindowIntersectionNodes;
      public List<int> PointIndices;
   }

   public class Node {
      public DoubleVector2 P;
      public int WindowIndex1;
      public double WindowT1;
      public int WindowIndex2;
      public double WindowT2;
   }

   public class SimplePolygonVisibilityDecomposition {
      private class Calculator {
         private Dictionary<int, PQE> pqeByKey = new Dictionary<int, PQE>();
         private PriorityQueue<PQE> pq = new PriorityQueue<PQE>((a, b) => a.Key.CompareTo(b.Key));

         public const float EPSILON = 1E-5f;
         public const float NEGATIVE_EPSILON = -EPSILON;

         public bool WithinEpsilon(float a, float b) {
            var c = a - b;
            return c > NEGATIVE_EPSILON & c < EPSILON;
         }


         public void Compute(Polygon2 poly, List<VisibilityPolygonWindow> windows, DebugMultiCanvasHost dmch = null) {
            var windowSegments = windows.Map(w => 
               w.Endpoint.X < poly.Points[w.ReflexVertexIndex].X
                ? new DoubleLineSegment2(w.Endpoint, poly.Points[w.ReflexVertexIndex].ToDoubleVector2())
                : new DoubleLineSegment2(poly.Points[w.ReflexVertexIndex].ToDoubleVector2(), w.Endpoint));

            var allNodes = new List<Node>();

            for (var windowIndex = 0; windowIndex < windows.Count; windowIndex++) {
               var window = windows[windowIndex];
               var endpoint = window.Endpoint;
               var reflexVertex = poly.Points[window.ReflexVertexIndex];

               var endpointKey = RoundToKey(endpoint.X);
               var reflexVertexKey = RoundToKey(reflexVertex.X);

               if (Math.Abs(endpointKey - reflexVertexKey) <= 1) {
                  GetOrAddPqe(endpointKey).VerticalWindowIndices.Add(windowIndex);
               } else if (endpointKey < reflexVertexKey) {
                  GetOrAddPqe(endpointKey).OutboundWindowIndices.Add(windowIndex);
                  GetOrAddPqe(reflexVertexKey).InboundWindowIndices.Add(windowIndex);
               } else { // endpointKey < endpointKey
                  GetOrAddPqe(reflexVertexKey).OutboundWindowIndices.Add(windowIndex);
                  GetOrAddPqe(endpointKey).InboundWindowIndices.Add(windowIndex);
               }
            }

            for (var pointIndex = 0; pointIndex < poly.Points.Count; pointIndex++) {
               var p = poly.Points[pointIndex];
               var xKey = RoundToKey(p.X);
               GetOrAddPqe(xKey).PointIndices.Add(pointIndex);
            }

            var activeWindowIndices = new HashSet<int>();
            var windowToLastSweepedNode = new Node[windows.Count];

            void Render(IDebugCanvas canvas = null) {
               canvas ??= dmch.CreateAndAddCanvas();
               canvas.BatchDraw(() => {
                  canvas.DrawPolygon(poly, StrokeStyle.BlackHairLineSolid);
                  
                  foreach (var awi in activeWindowIndices) {
                     canvas.DrawLine(windows[awi].Endpoint, poly.Points[windows[awi].ReflexVertexIndex].ToDoubleVector2(), StrokeStyle.CyanHairLineSolid);
                  }

                  var arr = pq.ToArray();
                  for (var pqi = 0; pqi < arr.Length; pqi++) {
                     var pqe = arr[pqi];
                     var x = LossyKeyToX(pqe.Key);
                     canvas.DrawLine(new DoubleVector2(x, -1337), new DoubleVector2(x, 1337), pqi == 0 ? StrokeStyle.MagentaThick5Solid: StrokeStyle.RedHairLineSolid);
                     canvas.DrawPoints(pqe.WindowIntersectionNodes.Map(x => x.P), StrokeStyle.OrangeThick10Solid);
                  }

                  for (var windowIndex = 0; windowIndex < windows.Count; windowIndex++) {
                     var w = windows[windowIndex];
                     var seg = windowSegments[windowIndex];
                     canvas.DrawLine(seg, StrokeStyle.GrayHairLineSolid);
                     var center = seg.PointAt(0.5);
                     canvas.DrawText(windowIndex.ToString(), center.ToDotNetVector());
                  }

                  foreach (var node in allNodes) {
                     canvas.DrawPoint(node.P, StrokeStyle.CyanThick5Solid);
                  }
               });
            }
            
            Render();

            while (!pq.IsEmpty) {
               var canvas = dmch.CreateAndAddCanvas();
               Render(canvas);

               var pqe = pq.Dequeue();

               Console.WriteLine($"Scan at {pqe.Key} x={LossyKeyToX(pqe.Key)}");

               foreach (var newWindowIndex in pqe.OutboundWindowIndices) {
                  Console.WriteLine("ADD " + newWindowIndex);

                  // tood handle mulltiple inteserets at same point
                  var node = new Node {
                     P = windowSegments[newWindowIndex].First,
                  };
                  windowToLastSweepedNode[newWindowIndex] = node;
                  allNodes.Add(node);

                  foreach (var wi in activeWindowIndices) {
                     var intersects = GeometryOperations.TryFindNonoverlappingLineLineIntersectionEx(
                                         windowSegments[newWindowIndex],
                                         windowSegments[wi],
                                         out var t1,
                                         out var t2) &&
                                      -EPSILON < t1 && t1 < EPSILON + 1 &&
                                      -EPSILON < t2 && t2 < EPSILON + 1;

                     if (intersects) {
                        var intersectionPoint = windowSegments[newWindowIndex].PointAt(t1);
                        var intersectionPointKey = RoundToKey(intersectionPoint.X);
                        var intersectionNode = new Node {
                           P = intersectionPoint, 
                           WindowIndex1 = newWindowIndex,
                           WindowT1 = t1,
                           WindowIndex2 = wi,
                           WindowT2 = t2,
                        };
                        GetOrAddPqe(intersectionPointKey).WindowIntersectionNodes.Add(intersectionNode);
                        allNodes.Add(intersectionNode);
                     }
                  }

                  activeWindowIndices.Add(newWindowIndex);
               }

               foreach (var endingWindowIndex in pqe.InboundWindowIndices) {
                  Console.WriteLine("REM " + endingWindowIndex);
                  var removed = activeWindowIndices.Remove(endingWindowIndex);
                  Assert.IsTrue(removed);

                  var node = new Node {
                     P = windowSegments[endingWindowIndex].Second,
                  };
                  windowToLastSweepedNode[endingWindowIndex] = node;
                  allNodes.Add(node);
               }



               foreach (var node in pqe.WindowIntersectionNodes) {
                  var wi1 = node.WindowIndex1;
                  var wi2 = node.WindowIndex2;
                  var a = windowToLastSweepedNode[wi1]?.P; // thes ca'nt actualyl be null right
                  var b = windowToLastSweepedNode[wi2]?.P;

                  if (a.HasValue) {
                     canvas.DrawLine(a.Value, node.P, StrokeStyle.LimeThick5Solid);
                  }

                  if (b.HasValue) {
                     canvas.DrawLine(b.Value, node.P, StrokeStyle.LimeThick5Solid);
                  }

                  windowToLastSweepedNode[wi1] = node;
                  windowToLastSweepedNode[wi2] = node;
               }

               foreach (var pointIndex in pqe.PointIndices) {
                  var node = new Node {
                     P = poly.Points[pointIndex].ToDoubleVector2(),
                  };
                  allNodes.Add(node);

                  var prev = poly.Points[pointIndex == 0 ? ^1 : pointIndex - 1];
                  var cur = poly.Points[pointIndex];
                  var next = poly.Points[pointIndex == poly.Points.Count - 1 ? 0 : pointIndex + 1];

                  // two edges extending right, what are vertical lines
                  var prevToCurIsRight = prev.X < cur.X;
                  var curToNextIsRight = cur.X < next.X;
                  if (prevToCurIsRight && !curToNextIsRight) {
                     var cell = new Cell();
                     cell.Left.Add(cur.ToDoubleVector2());
                     cell.Right.Add(cur.ToDoubleVector2());
                  } else if (prevToCurIsRight && curToNextIsRight) {
                     // extend cell prev to cur
                     // am i on the bot or top chain?
                  }
               }
            }

            Render();
         }

         public class Cell {
            public List<DoubleVector2> Left = new List<DoubleVector2>();
            public List<DoubleVector2> Right = new List<DoubleVector2>();
         }

         private static int RoundToKey(cDouble x) {
            return (int)Math.Round(x * 10000);
         }

         private static cDouble LossyKeyToX(int key) {
            return key / 10000.0;
         }

         private PQE GetOrAddPqe(int key) {
            PQE existing;
            if (!pqeByKey.TryGetValue(key, out existing) &&
                !pqeByKey.TryGetValue(key + 1, out existing) &&
                !pqeByKey.TryGetValue(key - 1, out existing)) {
               var pqe = pqeByKey[key] = new PQE {
                  Key = key,
                  InboundWindowIndices = new List<int>(),
                  OutboundWindowIndices = new List<int>(),
                  VerticalWindowIndices = new List<int>(),
                  WindowIntersectionNodes = new List<Node>(),
                  PointIndices = new List<int>(),
               };
               pq.Enqueue(pqe);
               return pqe;
            }
            return existing;
         }
      }

      public static void ExtractCells(Polygon2 poly, List<VisibilityPolygonWindow> windows, DebugMultiCanvasHost dmch) {
         new Calculator().Compute(poly, windows, dmch);
      }
   }
}
