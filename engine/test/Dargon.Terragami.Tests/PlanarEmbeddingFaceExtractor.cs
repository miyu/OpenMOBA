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
      public void X(List<PgeNode> nodes, int edgeCounter, DebugMultiCanvasHost dmch = null) {
         var pq = new PriorityQueue<PgeNode>((a, b) => a.Vertex.X.CompareTo(b.Vertex.X));
         var edgeToLeftCell = new Cell[edgeCounter];
         var edgeToRightCell = new Cell[edgeCounter];
         var allCells = new List<Cell>();
         var activeCells = new HashSet<Cell>();

         IDebugCanvas Render() {
            var canvas = dmch.CreateAndAddCanvas();
            canvas.BatchDraw(() => {
               foreach (var node in nodes) {
                  canvas.DrawPoint(node.Vertex, StrokeStyle.RedThick5Solid);
                  canvas.DrawText(node.Id.ToString(), node.Vertex.ToDotNetVector());

                  foreach (var outedge in node.OutboundEdges) {
                     canvas.DrawLine(
                        node.Vertex,
                        outedge.Other(node).Vertex,
                        StrokeStyle.BlackHairLineSolid);

                     canvas.DrawText(
                        "e" + outedge.Id.ToString(),
                        ((outedge.Source.Vertex + outedge.Destination.Vertex) / 2).ToDotNetVector()
                     );
                  }
               }

               var events = pq.ToArray();
               foreach (var (i, e) in events.Enumerate()) {
                  canvas.DrawPoint(
                     e.Vertex,
                     i == 0
                        ? StrokeStyle.RedThick25Solid
                        : StrokeStyle.RedThick10Solid);
               }

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
            });

            return canvas;
         }

         foreach (var node in nodes) {
            pq.Enqueue(node);
         }

         Render();

         var it = 0;
         while (!pq.IsEmpty) {
            // if (it == 4) Debugger.Break();
            it++;

            var canvas = Render();
            var n = pq.Dequeue();

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

         Render();

         Debugger.Break();
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

         var dmch = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
            AxisAlignedBoundingBox2.BoundingPoints(nodes.Map(n => n.Vertex)),
            // new Size(2250, 1100),
            new Size(1920 / 2, 1080 / 2),
            new Point(100, 100));

         new PlanarEmbeddingFaceExtractor().X(nodes, edgeCounter, dmch);
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
