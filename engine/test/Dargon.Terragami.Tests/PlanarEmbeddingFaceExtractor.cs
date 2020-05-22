using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Commons.Exceptions;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Dargon.Terragami.Tests {
   public class PlanarEmbeddingFaceExtractor {
      public PlanarEmbeddingFaceExtraction X(List<PgeNode> nodes, List<PgeEdge> edges, DebugMultiCanvasHost dmch = null, DebugDrawMode debugDrawMode = DebugDrawMode.None) {
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

         var edgeToLeftCell = new Cell[edges.Count];
         var edgeToRightCell = new Cell[edges.Count];

         var allCells = new HashSet<Cell>();
         var activeCells = new HashSet<Cell>();

         if (dmch == null && debugDrawMode != DebugDrawMode.None) {
            dmch = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
               AxisAlignedBoundingBox2.BoundingPoints(nodes.Map(n => n.Vertex)),
               new Size(2250, 1100), // new Size(1920 / 2, 1080 / 2),
               new Point(100, 100));
         }

         IDebugCanvas Render(int mode = 0) {
            var canvas = dmch.CreateAndAddCanvas();
            canvas.BatchDraw(() => {
               // ripped from https://bhaskarvk.github.io/colormap/reference/colormap.html lul
               var colorMap = @"
440154ff 440558ff 450a5cff 450e60ff 451465ff 461969ff 
461d6dff 462372ff 472775ff 472c7aff 46307cff 45337dff 
433880ff 423c81ff 404184ff 3f4686ff 3d4a88ff 3c4f8aff 
3b518bff 39558bff 37598cff 365c8cff 34608cff 33638dff 
31678dff 2f6b8dff 2d6e8eff 2c718eff 2b748eff 29788eff 
287c8eff 277f8eff 25848dff 24878dff 238b8dff 218f8dff 
21918dff 22958bff 23988aff 239b89ff 249f87ff 25a186ff 
25a584ff 26a883ff 27ab82ff 29ae80ff 2eb17dff 35b479ff 
3cb875ff 42bb72ff 49be6eff 4ec16bff 55c467ff 5cc863ff 
61c960ff 6bcc5aff 72ce55ff 7cd04fff 85d349ff 8dd544ff 
97d73eff 9ed93aff a8db34ff b0dd31ff b8de30ff c3df2eff 
cbe02dff d6e22bff e1e329ff eae428ff f5e626ff fde725ff ".Split(' ', StringSplitOptions.RemoveEmptyEntries).Map(x => x.Trim())
                                                       .Map(hex => {
                                                          int r = int.Parse(hex[0..2], System.Globalization.NumberStyles.HexNumber); // jfc
                                                          int g = int.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber); // jfc
                                                          int b = int.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber); // jfc
                                                          return Color.FromArgb(r, g, b);
                                                       })
                                                       .Shuffle(new Random(0));

               canvas.FillPolygon(Polygon2.CreateRect(
                  -1000, -1000,
                  3000, 3000), new FillStyle(Color.White));

               var cellColors = colorMap.Shuffle(new Random(0)).ToArray();

               ((DebugCanvas)canvas).SetFontScale(3);

               var graphScale = Math.Sqrt(nodes.Max(n => n.OutboundEdges.Count == 0 ? -1 : n.OutboundEdges.Max(e => DoubleVector2.SquaredDistanceNorm2(e.Source.Vertex, e.Destination.Vertex))));
               var pointSize = 10.0f * (float)graphScale / 315.42986542177647f; // found by hand
               var arrowPadding = 10.0f * (float)graphScale / 315.42986542177647f; // found by hand
               var neighborEdgePadding = 5.0f * (float)graphScale / 315.42986542177647f; // found by hand

               var cellToContourAndCentroid = new Dictionary<Cell, (List<DoubleVector2> contour, DoubleVector2 centroid)>();
               foreach (var (i, cell) in allCells.Enumerate()) {
                  var contour = cell.Left.MapList(c => c.node.Vertex);

                  // either closed cell or freshly opened
                  var j0 =
                     !activeCells.Contains(cell) ? cell.Right.Count - 2 : // last element in right chain matches last element in left chain
                     cell.Left[^1] == cell.Right[^1] ? -1 : // right chain has 1 item, which is first item in left chain
                     cell.Right.Count - 1; // right chain hasn't met left chain yet

                  for (var j = j0; j >= 1; j--) {
                     contour.Add(cell.Right[j].node.Vertex);
                  }

                  var centroid = contour.Aggregate(DoubleVector2.Zero, (a, b) => a + b) / contour.Count;
                  cellToContourAndCentroid.Add(cell, (contour, centroid));
               }

               foreach (var (i, cell) in allCells.Enumerate()) {
                  var (contour, centroid) = cellToContourAndCentroid[cell];

                  var color = cellColors[i % cellColors.Length];
                  canvas.DrawPolygon(contour, new StrokeStyle(color));

                  if (contour.Count > 2) {
                     var transparentColor = Color.FromArgb(125, color);
                     canvas.FillPolygon(contour, new FillStyle(transparentColor));
                  }
               }

               var asdf = 0;
               foreach (var (i, cell) in allCells.Enumerate()) {
                  var (contour, centroid) = cellToContourAndCentroid[cell];

                  foreach (var ((_, edgeIndex), isLeftChain) in cell.Left.Select(x => (x, true)).Concat(cell.Right.Select(x => (x, false)))) {
                     if (edgeIndex < 0) continue;

                     var neighborCell = isLeftChain ? edgeToLeftCell[edgeIndex] : edgeToRightCell[edgeIndex];
                     if (neighborCell == null) continue;

                     var edge = edges[edgeIndex];
                     var edgeSegment = edge.Source.Vertex.SegmentTo(edge.Destination.Vertex);
                     var edgeCenter = edgeSegment.PointAt(0.5);
                     var edgeDirection = edgeSegment.First.To(edgeSegment.Second).ToUnit();
                     var edgeDirectionPerp = isLeftChain ? edgeDirection.PerpLeft() : edgeDirection.PerpRight();
                     var vectorOffset = (isLeftChain ? edgeDirection : -edgeDirection) * neighborEdgePadding;

                     canvas.DrawVector(
                        edgeCenter + vectorOffset + edgeDirectionPerp * (neighborEdgePadding * 2),
                        edgeCenter + vectorOffset - edgeDirectionPerp * (neighborEdgePadding * 2),
                        StrokeStyle.BlackThick3Solid,
                        arrowPadding);

                     // if (asdf == 11) Debugger.Break();
                     // canvas.DrawText("d" + asdf.ToString(), ((centroid + offset + neighborCentroid + offset) / 2).ToDotNetVector());

                     asdf++;
                  }
               }

               var activatableNodes = activatableNodeQueue.ToArray().ToHashSet();
               foreach (var (i, node) in nodes.Enumerate()) {
                  var strokeStyle =
                     i == -12
                        ? new StrokeStyle(Color.LightSalmon, pointSize * 10)
                        : activatableNodes.Contains(node)
                           ? (
                              activatableNodeQueue.Peek() == node
                                 ? new StrokeStyle(Color.Lime, pointSize * 5)
                                 : new StrokeStyle(Color.Orange, pointSize * 4))
                           : new StrokeStyle(Color.Red, pointSize * 3);
                  canvas.DrawPoint(node.Vertex, strokeStyle);
                  var r = new Random(i * 1337);
                  var jitter = new Vector2(0.5f - (float)r.NextDouble(), 0.5f - (float)r.NextDouble()) * 20;
                  if ((mode & 1) == 1) {
                     var textPositoin = node.Vertex.ToDotNetVector(); 
                     textPositoin += new Vector2(2, 2) + jitter;
                     // canvas.DrawText($"{node.Id} ({nodeToUnactivatedInEdgeCount[node.Id]})", textPositoin);
                     canvas.DrawText($"{node.Id} ({nodeToUnactivatedInEdgeCount[node.Id]})\n{node.Vertex.X:F2},{node.Vertex.Y:F2}", textPositoin);
                     canvas.DrawLine(node.Vertex, textPositoin.ToOpenMobaVector(), new StrokeStyle(Color.MediumSlateBlue, 1));
                  }

                  // canvas.DrawText($"{node.Id}\n{node.Vertex.X:F2},{node.Vertex.Y:F2}", node.Vertex.ToDotNetVector());

                  foreach (var outedge in node.OutboundEdges) {
                     var seg = new DoubleLineSegment2(node.Vertex, outedge.Other(node).Vertex);
                     var segLength = seg.Length;
                     var effectiveArrowPadding = segLength < arrowPadding ? 0 : arrowPadding; // todo: tween this

                     canvas.DrawVector(
                        seg.PointAt((effectiveArrowPadding / 2) / segLength),
                        seg.PointAt((segLength - (effectiveArrowPadding / 2)) / segLength),
                        StrokeStyle.BlackThick3Solid,
                        arrowPadding * 2);

                     if ((mode & 2) == 2)
                        canvas.DrawText(
                           "e" + outedge.Id.ToString(),
                           outedge.Source.Vertex.SegmentTo(outedge.Destination.Vertex).PointAt(0.3).ToDotNetVector()
                        );
                  }
               }
            });

            return canvas;
         }

         if (debugDrawMode == DebugDrawMode.Steps) {
            // Render();
         }

         var it = 0;
         // try {
         while (activatableNodeQueue.Count > 0) {
            Console.WriteLine($"it{it}: activatable nodes: {activatableNodeQueue.Count + 1}");

            it++;
            
            if (debugDrawMode == DebugDrawMode.Steps) {
               Render(0);
               // Render(1);
               // Render(2);
               // Render(false);
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
               cell.Left.Add((n, -1));
               cell.Right.Add((n, -1));

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
                  lastOutboundEdgeLeftCell.Right.Add((n, lastInboundEdge.Id));
               }

               if (edgeToRightCell[firstInboundEdge.Id] != null) {
                  var lastOutboundEdgeRightCell = edgeToRightCell[firstOutboundEdge.Id] = edgeToRightCell[firstInboundEdge.Id];
                  lastOutboundEdgeRightCell.Left.Add((n, firstInboundEdge.Id));
               }
            }

            // foreach inbound cell we're terminating, add self to both left/right
            // chains, as we need to track both segments' edges.
            // first/last inbound cells added to above already
            for (var i = 0; i < n.InboundEdges.Count - 1; i++) {
               var rightEdge = n.InboundEdges[i];
               var leftEdge = n.InboundEdges[i + 1];

               var leftEdgeRightCell = edgeToRightCell[leftEdge.Id];
               var rightEdgeLeftCell = edgeToLeftCell[rightEdge.Id];

               // The cells don't necessarily match, one or the other can be null
               // See https://imgur.com/a/cRCsmLH
               //
               // If they mismatch or one is null, then nothing to do here.
               if (leftEdgeRightCell != rightEdgeLeftCell || leftEdgeRightCell == null) {
                  if (leftEdgeRightCell != null) {
                     activeCells.Remove(leftEdgeRightCell);
                  }
                  if (rightEdgeLeftCell != null) {
                     activeCells.Remove(rightEdgeLeftCell);
                  }
                  continue;
               }

               var cell = leftEdgeRightCell;
               if (cell == null) continue;
               cell.Left.Add((n, leftEdge.Id));
               cell.Right.Add((n, rightEdge.Id));
               activeCells.Remove(cell);
            }
         }

         // cleanup: kill remaining unclosed cells - they' walking nonconverging
         // paths on the contour
         for (var i = 0; i < edges.Count; i++) {
            if (activeCells.Contains(edgeToLeftCell[i])) edgeToLeftCell[i] = null;
            if (activeCells.Contains(edgeToRightCell[i])) edgeToRightCell[i] = null;
         }


         foreach (var activeCell in activeCells) {
            allCells.Remove(activeCell);
         }

         activeCells.Clear();

         // } catch (Exception) when (new [] { 1 }.Map(x => {
         //    if (debugDrawMode != DebugDrawMode.None) {
         //       Render(false);
         //       Render(true);
         //    }
         //    return false;
         // })[0]) { }

         if (debugDrawMode != DebugDrawMode.None) {
            Render();
         }

         var allCellsList = allCells.ToList();
         var res = new PlanarEmbeddingFaceExtraction {
            Cells = allCellsList,
            EdgeToLeftCell = edgeToLeftCell,
            EdgeToRightCell = edgeToRightCell,
         }; // todo: API for fetching cell neighbors not via lookup of etlc/etrc

         foreach (var cell in allCells) {
            // edgeIndices[i] corresponds to edge of contour[i] + contour[i + 1]
            var contour = new DoubleVector2[cell.Left.Count + cell.Right.Count - 2];
            var neighbors = new Cell[contour.Length];
            var edgeIndices = new int[contour.Length];

            var nextIndex = 0;
            for (var i = 0; i < cell.Left.Count - 1; i++) {
               contour[nextIndex] = cell.Left[i].node.Vertex;
               var edgeIndex = cell.Left[i + 1].edgeIndex;
               neighbors[nextIndex] = edgeToLeftCell[edgeIndex];
               edgeIndices[nextIndex] = edgeIndex;
               nextIndex++;
            }

            for (var i = cell.Right.Count - 1; i >= 1; i--) {
               contour[nextIndex] = cell.Right[i].node.Vertex;
               var edgeIndex = cell.Right[i].edgeIndex;
               neighbors[nextIndex] = edgeToRightCell[edgeIndex];
               edgeIndices[nextIndex] = edgeIndex;
               nextIndex++;
            }

            cell.Contour = contour;
            cell.Neighbors = neighbors;
            cell.EdgeIndices = edgeIndices;
         }

         return res;
      }

      public class PlanarEmbeddingFaceExtraction {
         public List<Cell> Cells;
         public Cell[] EdgeToLeftCell;
         public Cell[] EdgeToRightCell;
      }

      public class Cell {
         // (node, edge last responsible for adding node as destination)
         // such that node[i] to node[i+1] is through edgeIndex[i+1]
         public List<(PgeNode node, int edgeIndex)> Left = new List<(PgeNode node, int edgeIndex)>();
         public List<(PgeNode node, int edgeIndex)> Right = new List<(PgeNode node, int edgeIndex)>();

         // Computed at cleanup step of face extraction
         public DoubleVector2[] Contour;
         public Cell[] Neighbors;
         public int[] EdgeIndices;
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
         var edges = new List<PgeEdge>();
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

            edges.Add(edge);
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

         var cells = new PlanarEmbeddingFaceExtractor().X(nodes, edges, null, DebugDrawMode.Steps);
      }
   }

   /// <summary>
   /// todo: figure out what graph representation is best
   /// </summary>
   public class PgeNode {
      public int Id;
      public DoubleVector2 Vertex;

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
