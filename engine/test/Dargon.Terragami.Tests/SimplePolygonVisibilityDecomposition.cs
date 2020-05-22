using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Dviz;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using NuGet.Frameworks;
using cDouble = System.Double;
using Debugger = System.Diagnostics.Debugger;

namespace Dargon.Terragami.Tests {
   public enum EventType {
      PointEvent,
      InboundWindow,
      OutboundWindow,

      Intersection,
   }

   public abstract class PriorityQueueEvent {
      public int XKey;
      public cDouble Y;
      public EventType EventType;
   }

   public class WindowEvent : PriorityQueueEvent {
      public int windowIndex;
      public int existingVertexIndex;
      public DoubleVector2 vertex;
   }

   public class IntersectionEvent : PriorityQueueEvent {
      public DoubleVector2 intersectionPoint;
      public int w1;
      public double t1;
      public int w2;
      public double t2;
   }

   public class PointEvent : PriorityQueueEvent {
      public int pointIndex;
   }

   public class SimplePolygonVisibilityDecomposition {
      private class Calculator {
         private HashSet<int> usedXKeys = new HashSet<int>();
         private PriorityQueue<PriorityQueueEvent> pq = new PriorityQueue<PriorityQueueEvent>(ComparePriorityQueueEvents);

         private static int ComparePriorityQueueEvents(PriorityQueueEvent x, PriorityQueueEvent y) {
            var res = x.XKey.CompareTo(y.XKey);
            if (res != 0) return res;
            res = x.Y.CompareTo(y.Y);
            if (res != 0) return res;
            return x.EventType.CompareTo(y.EventType);
         }

         public const float EPSILON = 1E-5f;
         public const float NEGATIVE_EPSILON = -EPSILON;

         public bool WithinEpsilon(float a, float b) {
            var c = a - b;
            return c > NEGATIVE_EPSILON & c < EPSILON;
         }

         public unsafe (List<PgeNode> allNodes, List<PgeEdge> allEdges) Compute(Polygon2 poly, List<VisibilityPolygonWindow> windows, DebugMultiCanvasHost dmch = null, DebugDrawMode debugDrawMode = DebugDrawMode.None) {
            var windowSegments = windows.Map(w =>
               w.Endpoint.X < poly.Points[w.ReflexVertexIndex].X
                ? new DoubleLineSegment2(w.Endpoint, poly.Points[w.ReflexVertexIndex].ToDoubleVector2())
                : new DoubleLineSegment2(poly.Points[w.ReflexVertexIndex].ToDoubleVector2(), w.Endpoint));

            var allNodes = new List<PgeNode>(); // indices [0, pointCount) map to nodes corresponding to point
            var allEdges = new List<PgeEdge>();
            var contourPointNodes = new PgeNode[poly.Points.Count];

            for (var pointIndex = 0; pointIndex < poly.Points.Count; pointIndex++) {
               var p = poly.Points[pointIndex];
               var xKey = RoundToKey(p.X);
               pq.Enqueue(new PointEvent {
                  XKey = xKey,
                  Y = p.Y,
                  EventType = EventType.PointEvent,

                  pointIndex = pointIndex,
               });

               var node = new PgeNode {
                  Id = pointIndex,
                  Vertex = poly.Points[pointIndex].ToDoubleVector2(),
               };
               allNodes.Add(node);
               Console.WriteLine($"INIT: Add node id {node.Id}");
            }

            for (var windowIndex = 0; windowIndex < windows.Count; windowIndex++) {
               var window = windows[windowIndex];
               var endpoint = window.Endpoint;
               var windowReflexVertexIndex = window.ReflexVertexIndex;
               var reflexVertex = poly.Points[windowReflexVertexIndex];

               var endpointKey = RoundToKey(endpoint.X);
               var reflexVertexKey = RoundToKey(reflexVertex.X);
               var (endpointType, reflexVertexType) =
                  Math.Abs(endpointKey - reflexVertexKey) <= 1 ? (
                     endpoint.Y < reflexVertex.Y ? (EventType.OutboundWindow, EventType.InboundWindow) :
                        (EventType.InboundWindow, EventType.OutboundWindow)
                     ) :
                  endpointKey < reflexVertexKey ? (EventType.OutboundWindow, EventType.InboundWindow) :
                  (EventType.InboundWindow, EventType.OutboundWindow);

               pq.Enqueue(new WindowEvent {
                  XKey = reflexVertexKey,
                  Y = reflexVertex.Y,
                  EventType = reflexVertexType,

                  windowIndex = windowIndex,
                  existingVertexIndex = windowReflexVertexIndex,
                  vertex = reflexVertex.ToDoubleVector2(),
               });
               pq.Enqueue(new WindowEvent {
                  XKey = endpointKey,
                  Y = endpoint.Y,
                  EventType = endpointType,
                  
                  windowIndex = windowIndex,
                  existingVertexIndex = -1,
                  vertex = endpoint,
               });
            }

            var activeWindowIndices = new HashSet<int>();
            var windowToLastSweepedNode = new PgeNode[windows.Count];
            var segmentToLastSweepedNode = new PgeNode[poly.Points.Count];

            var outputGraphEdgeIdCounter = 0;
            var iteration = 0;

            void Derp(PgeNode[] segmentIndexToLastNode, int segmentIndex, PgeNode newNode) {
               Assert.IsNotNull(newNode);
               var lastNode = segmentIndexToLastNode[segmentIndex];
               if (lastNode == newNode) {
                  Console.WriteLine($"it{iteration}: skip degenerate edge from node to itself");
               } else if (lastNode != null) {
                  // if (segmentToLastSweepedNode == segmentIndexToLastNode && segmentIndex == 2 && newNode.Id == 21) Debugger.Break();
                  // if (outputGraphEdgeIdCounter == 33) Debugger.Break();
                  var direction = lastNode.Vertex.To(newNode.Vertex);

                  var edgeId = outputGraphEdgeIdCounter++;
                  Console.WriteLine($"it{iteration}: edge {edgeId} src {lastNode.Id} / {lastNode.Vertex} dst {newNode.Id} / {newNode.Vertex}");

                  var edge = new PgeEdge {
                     Source = lastNode,
                     Destination = newNode,
                     Id = edgeId,
                     Slope = (float)(direction.Y / direction.X),
                  };

                  if (lastNode == newNode) Debugger.Break();

                  allEdges.Add(edge);

                  lastNode.OutboundEdges.Add(edge);
                  newNode.InboundEdges.Add(edge);
               }

               segmentIndexToLastNode[segmentIndex] = newNode;
            }
            var outputGraphNodeIdCounter = poly.Points.Count;

            void Render(IDebugCanvas canvas = null) {
               canvas ??= dmch.CreateAndAddCanvas();
               canvas.BatchDraw(() => {
                  canvas.DrawPolygon(poly, StrokeStyle.BlackHairLineSolid);

                  var labelOffset = new Vector2(1, -2);
                  foreach (var (i, p) in poly.Points.Enumerate()) {
                     canvas.DrawText($"{i}\n{p.X:F2}, {p.Y:F2}", p.ToDotNetVector() + labelOffset);
                  }

                  foreach (var awi in activeWindowIndices) {
                     var endpoint = windows[awi].Endpoint;
                     var reflex = poly.Points[windows[awi].ReflexVertexIndex].ToDoubleVector2();
                     canvas.DrawLine(endpoint, reflex, StrokeStyle.CyanHairLineSolid);
                  }

                  var arr = pq.ToArray();
                  var drawnXLineKeys = new HashSet<int>();
                  for (var pqi = 0; pqi < arr.Length; pqi++) {
                     var pqe = arr[pqi];
                     if (drawnXLineKeys.Add(pqe.XKey)) {
                        var x = LossyKeyToX(pqe.XKey);
                        canvas.DrawLine(new DoubleVector2(x, -1337), new DoubleVector2(x, 1337), pqi == 0 ? StrokeStyle.MagentaThick5Solid : StrokeStyle.RedHairLineSolid);
                     }

                     if (pqe is IntersectionEvent ie) {
                        canvas.DrawPoint(ie.intersectionPoint, StrokeStyle.OrangeThick10Solid);
                     }
                  }

                  for (var windowIndex = 0; windowIndex < windows.Count; windowIndex++) {
                     var w = windows[windowIndex];
                     var seg = windowSegments[windowIndex];
                     canvas.DrawLine(seg, StrokeStyle.GrayHairLineSolid);
                     var p = seg.PointAt(0.3);
                     var r = new Random(windowIndex * 1337);
                     // var jitter = new Vector2((float)r.NextDouble(), (float)r.NextDouble());
                     canvas.DrawText("w" + windowIndex, p.ToDotNetVector());
                  }

                  foreach (var node in allNodes) {
                     canvas.DrawPoint(node.Vertex, StrokeStyle.CyanThick5Solid);
                     canvas.DrawText($"{node.Id}", node.Vertex.ToDotNetVector());

                     foreach (var outedge in node.OutboundEdges) {
                        var other = outedge.Other(node);
                        var nodeToOtherSeg = node.Vertex.SegmentTo(other.Vertex);
                        var segLength = nodeToOtherSeg.Length;
                        var desiredPadding = 20;
                        var padding = segLength < desiredPadding ? 0 : desiredPadding;

                        // Assert.IsLessThanOrEqualTo(node.Vertex.X, other.Vertex.X);

                        canvas.DrawVector(
                           nodeToOtherSeg.PointAt((padding / 2) / segLength),
                           nodeToOtherSeg.PointAt(1 - (padding / 2) / segLength), 
                           StrokeStyle.RedThick3Solid, 
                           10);

                        canvas.DrawText($"e{outedge.Id}", nodeToOtherSeg.PointAt(0.5).ToDotNetVector());
                     }
                  }
               });
            }

            // if (debugDrawMode == DebugDrawMode.Steps) {
            //    Render();
            // }

            PgeNode lastPointOrWindowEventNode = null;

            while (!pq.IsEmpty) {
               if (debugDrawMode == DebugDrawMode.Steps) {
                  var canvas = dmch.CreateAndAddCanvas();
                  Render(canvas);
               }

               // if (iteration == 100) break;

               iteration++;

               var pqe = pq.Dequeue();
               if (Math.Abs(pqe.Y - 100) < EPSILON && Math.Abs(pqe.XKey - LossyKeyToX(75)) <= 1) {
                  Debugger.Break();
               }

               var pqorderedhack = pq.ToArray();

               Console.WriteLine($"Scan at {pqe.XKey} x={LossyKeyToX(pqe.XKey)}");

               switch (pqe) {
                  case WindowEvent we: {
                     var eventWindowIndex = we.windowIndex;
                     var existingVertexIndexOpt = we.existingVertexIndex;
                     var vertex = we.vertex;

                     var window = windows[eventWindowIndex];
                     Console.WriteLine("PROCESS WINDOW INDEX " + eventWindowIndex + " " + we.EventType + " wi " + we.windowIndex + " rvi " + window.ReflexVertexIndex + " sfp " + window.SegmentFirstPointIndex);

                     if (existingVertexIndexOpt < 0) {
                        // endpoint

                        // NOTE: there is a degenerate case where a window endpoint ends on a vertex
                        // of our input polygon. In this case, we don't want to create a new node,
                        // rather we want to use the existing node.
                        // TODO: this assumes 0 floating point error, which I think is fine as the
                        // endpoints should be copied in, no math involved
                        PgeNode node;
                        if (lastPointOrWindowEventNode?.Vertex == window.Endpoint) {
                           node = lastPointOrWindowEventNode;
                           Console.WriteLine($"it{iteration}: Degenerate case, node exists and just added, use {node.Id}");
                        } else {
                           node = new PgeNode {
                              Id = outputGraphNodeIdCounter++,
                              Vertex = vertex,
                           };
                           allNodes.Add(node);
                           Console.WriteLine($"it{iteration}: Add node id {node.Id}");
                           lastPointOrWindowEventNode = node;
                        }

                        Derp(segmentToLastSweepedNode, window.SegmentFirstPointIndex, node);
                        Derp(windowToLastSweepedNode, eventWindowIndex, node);
                     } else {
                        // reflex
                        Assert.Equals(existingVertexIndexOpt, window.ReflexVertexIndex);
                        var node = contourPointNodes[window.ReflexVertexIndex];
                        Derp(windowToLastSweepedNode, eventWindowIndex, node);
                     }

                     if (we.EventType == EventType.OutboundWindow) {
                        foreach (var wi in activeWindowIndices) {
                           var intersects = GeometryOperations.TryFindNonoverlappingLineLineIntersectionEx(
                                               windowSegments[eventWindowIndex],
                                               windowSegments[wi],
                                               out var t1,
                                               out var t2) &&
                                            EPSILON < t1 && t1 < 1 - EPSILON &&
                                            EPSILON < t2 && t2 < 1 - EPSILON;

                           if (intersects) {
                              var intersectionPoint = windowSegments[eventWindowIndex].PointAt(t1);
                              var xkey = GetOrAddXKey(intersectionPoint.X);
                              pq.Enqueue(new IntersectionEvent {
                                 XKey = xkey,
                                 Y = intersectionPoint.Y,
                                 EventType = EventType.Intersection,

                                 intersectionPoint = intersectionPoint,
                                 w1 = eventWindowIndex,
                                 t1 = t1,
                                 w2 = wi,
                                 t2 = t2,
                              });
                              // GetOrAddXKey(intersectionPointKey).WindowIntersectionNodes.Add(());
                           }
                        }

                        activeWindowIndices.Add(eventWindowIndex);
                     } else {
                        activeWindowIndices.Remove(eventWindowIndex);
                     }

                     break;
                  }
                  case IntersectionEvent ie: {
                     Console.WriteLine("PROCESS INTERSECTION EVENT " + ie.XKey + " " + ie.Y + " " + ie.w1 + " " + ie.w2 + " " + ie.intersectionPoint);

                     var node = new PgeNode {
                        Id = outputGraphNodeIdCounter++,
                        Vertex = ie.intersectionPoint,
                     };
                     allNodes.Add(node);
                     Console.WriteLine($"it{iteration}: Add node id {node.Id}");

                     Derp(windowToLastSweepedNode, ie.w1, node);
                     Derp(windowToLastSweepedNode, ie.w2, node);
                     break;
                  }
                  case PointEvent pe: {
                     Console.WriteLine("PROCESS POINT EVENT " + pe.XKey + " " + pe.Y + " " + pe.pointIndex + " " + poly.Points[pe.pointIndex]);

                     var pointIndex = pe.pointIndex;

                     var node = allNodes[pointIndex];
                     contourPointNodes[pointIndex] = node;
                     lastPointOrWindowEventNode = node;

                     var inboundEdgeIds = stackalloc int[2];
                     inboundEdgeIds[0] = pointIndex == 0 ? poly.Points.Count - 1 : pointIndex - 1;
                     inboundEdgeIds[1] = pointIndex;

                     for (var i = 0; i < 2; i++) {
                        var inboundEdgeId = inboundEdgeIds[i];
                        Derp(segmentToLastSweepedNode, inboundEdgeId, node);
                     }
                     break;
                  }
               }
            }

            if (debugDrawMode != DebugDrawMode.None) {
               Render();
            }

            return (allNodes, allEdges);
         }

         private static int RoundToKey(cDouble x) {
            return (int)Math.Round(x * 10000);
         }

         private static cDouble LossyKeyToX(int key) {
            return key / 10000.0;
         }

         private int GetOrAddXKey(cDouble x) {
            var center = RoundToKey(x);

            if (usedXKeys.Contains(center)) return center;
            if (usedXKeys.Contains(center + 1)) return center + 1;
            if (usedXKeys.Contains(center - 1)) return center - 1;
            usedXKeys.Add(center);
            return center;
         }
      }

      public static (List<PgeNode> allNodes, List<PgeEdge> allEdges) Imstreafmingontwitch(Polygon2 poly, List<VisibilityPolygonWindow> windows, DebugMultiCanvasHost dmch, DebugDrawMode debugDrawMode) {
         return new Calculator().Compute(poly, windows, dmch, debugDrawMode);
      }
   }
}
