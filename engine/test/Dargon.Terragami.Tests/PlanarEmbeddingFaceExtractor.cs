using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Commons.Exceptions;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;

namespace Dargon.Terragami.Tests {
   public class PlanarEmbeddingFaceExtractor {
      public void X(List<PgeNode> nodes, int edgeCounter, DebugMultiCanvasHost dmch = null, DebugDrawMode debugDrawMode = DebugDrawMode.None) {
         // traverse nodes in topological order
         var nodeToUnactivatedInEdgeCount = new int[nodes.Count];
         var activatableNodeQueue = new Queue<PgeNode>();
         for (var i = 0; i < nodes.Count; i++) {
            var node = nodes[i];

            var inboundEdgesCount = node.InboundEdges.Count;
            nodeToUnactivatedInEdgeCount[i] = inboundEdgesCount;
            Assert.Equals(i, node.Id);

            if (inboundEdgesCount == 0) {
               activatableNodeQueue.Enqueue(node);
            }
         }

         var edgeToLeftCell = new Cell[edgeCounter];
         var edgeToRightCell = new Cell[edgeCounter];
         var allCells = new List<Cell>();
         var activeCells = new HashSet<Cell>();

         if (dmch == null && debugDrawMode != DebugDrawMode.None) {
            dmch = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
               AxisAlignedBoundingBox2.BoundingPoints(nodes.Map(n => n.Vertex)),
               new Size(2250, 1100), // new Size(1920 / 2, 1080 / 2),
               new Point(100, 100));
         }

         IDebugCanvas Render() {
            var canvas = dmch.CreateAndAddCanvas();
            canvas.BatchDraw(() => {
               var cellColors = new[] {
                  Color.Red,
                  Color.Orange,
                  Color.Yellow,
                  Color.YellowGreen,
                  Color.Lime,
                  Color.Cyan,
                  Color.Blue,
                  Color.Purple,
                  Color.Magenta,
                  Color.Gray,
               };

               ((DebugCanvas)canvas).SetFontScale(5);

               foreach (var (i, cell) in allCells.Enumerate()) {
                  var contour = cell.Left.Concat(cell.Right.Skip(1).Reverse())
                                    .Select(x => x.Vertex)
                                    .ToList();

                  var color = cellColors[i % cellColors.Length];
                  canvas.DrawPolygon(contour, new StrokeStyle(color));

                  if (contour.Count <= 2) continue;
                  
                  var transparentColor = Color.FromArgb(125, color);
                  canvas.FillPolygon(contour, new FillStyle(transparentColor));
               }

               var graphScale = Math.Sqrt(nodes.Max(n => n.OutboundEdges.Count == 0 ? -1 : n.OutboundEdges.Max(e => DoubleVector2.SquaredDistanceNorm2(e.Source.Vertex, e.Destination.Vertex))));
               var pointSize = 10.0f * (float)graphScale / 315.42986542177647f; // found by hand
               var arrowPadding = 10.0f * (float)graphScale / 315.42986542177647f; // found by hand

               var activatableNodes = activatableNodeQueue.ToArray().ToHashSet();
               foreach (var node in nodes) {
                  var strokeStyle = activatableNodes.Contains(node)
                     ? (activatableNodeQueue.Peek() == node
                        ? new StrokeStyle(Color.Lime, pointSize * 5)
                        : new StrokeStyle(Color.Orange, pointSize * 4))
                     : new StrokeStyle(Color.Red, pointSize * 3);
                  canvas.DrawPoint(node.Vertex, strokeStyle);
                  canvas.DrawText(node.Id.ToString(), node.Vertex.ToDotNetVector());

                  foreach (var outedge in node.OutboundEdges) {
                     var seg = new DoubleLineSegment2(node.Vertex, outedge.Other(node).Vertex);
                     var segLength = seg.Length;
                     var effectiveArrowPadding = segLength < arrowPadding ? 0 : arrowPadding; // todo: tween this

                     canvas.DrawVector(
                        seg.PointAt(effectiveArrowPadding / segLength),
                        seg.PointAt((segLength - effectiveArrowPadding) / segLength),
                        StrokeStyle.BlackThick3Solid,
                        arrowPadding * 2);

                     canvas.DrawText(
                        "e" + outedge.Id.ToString(),
                        ((outedge.Source.Vertex + outedge.Destination.Vertex) / 2).ToDotNetVector()
                     );
                  }
               }
            });

            return canvas;
         }

         if (debugDrawMode == DebugDrawMode.Steps) {
            Render();
         }

         var it = 0;
         while (activatableNodeQueue.Count > 0) {
            it++;

            if (debugDrawMode == DebugDrawMode.Steps) {
               var canvas = Render();
            }

            var n = activatableNodeQueue.Dequeue();

            foreach (var outedge in n.OutboundEdges) {
               var dest = outedge.Destination;
               if (--nodeToUnactivatedInEdgeCount[dest.Id] == 0) {
                  activatableNodeQueue.Enqueue(dest);
               }
            }

            n.InboundEdges.Sort((a, b) => -a.Slope.CompareTo(b.Slope));
            n.OutboundEdges.Sort((a, b) => a.Slope.CompareTo(b.Slope));

            for (var i = 0; i < n.OutboundEdges.Count - 1; i++) {
               var leftEdge = n.OutboundEdges[i + 1];
               var rightEdge = n.OutboundEdges[i];

               var cell = new Cell();
               cell.Left.Add(n);
               cell.Right.Add(n);

               edgeToRightCell[leftEdge.Id] = cell;
               edgeToLeftCell[rightEdge.Id] = cell;

               allCells.Add(cell);
               activeCells.Add(cell);
            }

            if (n.OutboundEdges.Count > 0 && n.InboundEdges.Count > 0) {
               var firstInboundEdge = n.InboundEdges[0];
               var lastInboundEdge = n.InboundEdges[^1];

               var firstOutboundEdge = n.OutboundEdges[0];
               var lastOutboundEdge = n.OutboundEdges[^1];

               if (edgeToLeftCell[lastInboundEdge.Id] != null) {
                  var lastOutboundEdgeLeftCell = edgeToLeftCell[lastOutboundEdge.Id] = edgeToLeftCell[lastInboundEdge.Id];
                  lastOutboundEdgeLeftCell.Right.Add(n);
               }

               if (edgeToRightCell[firstInboundEdge.Id] != null) {
                  var lastOutboundEdgeRightCell = edgeToRightCell[firstOutboundEdge.Id] = edgeToRightCell[firstInboundEdge.Id];
                  lastOutboundEdgeRightCell.Left.Add(n);
               }
            }

            // foreach inbound cell we're terminating, add self.
            // first/last inbound cells added to above already
            for (var i = 0; i < n.InboundEdges.Count - 1; i++) {
               var inboundEdge = n.InboundEdges[i];
               var leftCell = edgeToLeftCell[inboundEdge.Id];

               // left cell can be null in case
               // *--->
               //  \
               //   * here
               //  / 
               // *--->
               if (leftCell == null) continue;

               leftCell.Right.Add(n);
               activeCells.Remove(leftCell);
            }
         }

         if (debugDrawMode != DebugDrawMode.None) {
            Render();
         }
      }

      public class Cell {
         public List<PgeNode> Left = new List<PgeNode>();
         public List<PgeNode> Right = new List<PgeNode>();
      }

      public static void Exec() {
         var a = new PgeNode { Vertex = new DoubleVector2(146, 222) };
         var b = new PgeNode { Vertex = new DoubleVector2(174, 382) };
         var c = new PgeNode { Vertex = new DoubleVector2(251, 131) };
         var d = new PgeNode { Vertex = new DoubleVector2(285, 298) };
         var e = new PgeNode { Vertex = new DoubleVector2(338, 225) };
         var f = new PgeNode { Vertex = new DoubleVector2(413, 138) };
         var g = new PgeNode { Vertex = new DoubleVector2(395, 293) };
         var h = new PgeNode { Vertex = new DoubleVector2(488, 241) };
         var i = new PgeNode { Vertex = new DoubleVector2(488, 412) };

         var nodes = new[] {
            a, b, c, d, e, f, g, h, i
         }.ToList();

         foreach (var v in nodes) {
            v.Vertex = new DoubleVector2(v.Vertex.X, 482 - v.Vertex.Y);
         }

         var edgeCounter = 0;
         void AddEdge(PgeNode src, PgeNode dst) {
            var srcToDest = src.Vertex.To(dst.Vertex);
            Assert.IsLessThanOrEqualTo(src.Vertex.X, dst.Vertex.X);

            var edge = new PgeEdge {
               Source = src,
               Destination = dst,
               Id = edgeCounter++,
               Slope = (float)(srcToDest.Y / srcToDest.X)
            };

            src.OutboundEdges.Add(edge);
            dst.InboundEdges.Add(edge);
         }

         AddEdge(a, c);
         AddEdge(a, d);
         AddEdge(b, d);
         AddEdge(b, i);
         AddEdge(c, e);
         AddEdge(c, f);
         AddEdge(d, e);
         AddEdge(d, i);
         AddEdge(e, f);
         AddEdge(e, g);
         AddEdge(f, h);
         AddEdge(g, h);
         AddEdge(g, i);

         for (var it = 0; it < nodes.Count; it++) {
            nodes[it].Id = it;
         }

         // var dmch = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
         //    AxisAlignedBoundingBox2.BoundingPoints(nodes.Map(n => n.Vertex)),
         //    // new Size(2250, 1100),
         //    new Size(1920 / 2, 1080 / 2),
         //    new Point(100, 100));

         new PlanarEmbeddingFaceExtractor().X(nodes, edgeCounter, null, DebugDrawMode.Steps);
      }
   }

   /// <summary>
   /// todo: figure out what graph representation is best
   /// </summary>
   public class PgeNode {
      public DoubleVector2 Vertex;
      public int Id;

      public List<PgeEdge> InboundEdges = new List<PgeEdge>();
      public List<PgeEdge> OutboundEdges = new List<PgeEdge>();
   }

   public class PgeEdge {
      public PgeNode Source;
      public PgeNode Destination;

      public int Id;
      public float Slope; // todo handle verticals

      public PgeNode Other(PgeNode node) =>
         node == Source ? Destination :
         node == Destination ? Source :
         throw new InvalidStateException();
   }
}
