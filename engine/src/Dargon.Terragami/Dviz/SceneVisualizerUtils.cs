using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami.Dviz {
   public static class SceneVisualizerUtils {
      public static Polygon2 Visualize(this Polygon2 poly, IDebugCanvas canvas = null, bool labelIndices = false) {
         VisualizeInternal(AxisAlignedBoundingBox2.BoundingPolygon(poly), canvas, c => {
            c.DrawPolygon(poly, StrokeStyle.BlackHairLineSolid);
            
            if (labelIndices) {
               foreach (var (i, p) in poly.Points.Enumerate()) {
                  c.DrawText(i.ToString(), p.ToDotNetVector());
               }
            }
         });
         return poly;
      }

      public static PolygonNode Visualize(this PolygonNode node, IDebugCanvas canvas = null, bool labelIndices = false) {
         VisualizeInternal(AxisAlignedBoundingBox2.BoundingPolygonNode(node), canvas, c => {
            c.DrawPolygonNode(node);

            if (labelIndices) {
               foreach (var n in node.Bfs((push, n) => n.Children.ForEach(push))) {
                  if (n.Contour == null) continue;

                  foreach (var (i, p) in n.Contour.Enumerate()) {
                     c.DrawText(i.ToString(), p.ToDotNetVector());
                  }
               }
            }
         });
         return node;
      }

      private static void VisualizeInternal(AxisAlignedBoundingBox2 bounds, IDebugCanvas canvas, Action<IDebugCanvas> drawFunc) {
         if (canvas != null) {
            drawFunc(canvas);
            return;
         }

         ShowAndWaitHelperInternal(bounds, drawFunc);
      }

      public static DebugMultiCanvasHost CreateAndShowFittingCanvasHost(AxisAlignedBoundingBox2 bounds, Size? displaySize = null, Point? paddingSize = null) {
         var lower = (bounds.Center - bounds.Extents).ToDotNetVector();
         var upper = (bounds.Center + bounds.Extents).ToDotNetVector();

         displaySize ??= new Size(800, 500);
         paddingSize ??= new Point(125, 125);

         var dx = upper.X - lower.X;
         var dy = upper.Y - lower.Y;
         var displayScale = MathF.Min((float)displaySize.Value.Width / dx, (float)displaySize.Value.Height / dy);
         displayScale = float.IsInfinity(displayScale) ? 1 : displayScale; // handle 0 w & h

         var cameraPosition = (new Vector2((float)(lower.X + upper.X) / 2.0f, (float)(lower.Y + upper.Y) / 2.0f));
         var projector = new OrthographicXYProjector(displayScale, cameraPosition, new Vector2(displaySize.Value.Width / 2, displaySize.Value.Height / 2), true);
         return DebugMultiCanvasHost.CreateAndShowCanvas(displaySize.Value, paddingSize.Value, projector);

      }

      private static void ShowAndWaitHelperInternal(AxisAlignedBoundingBox2 bounds, Action<IDebugCanvas> drawFunc) {
         var canvasHost = CreateAndShowFittingCanvasHost(bounds);
         var canvas = canvasHost.CreateAndAddCanvas(0);
         canvas.BatchDraw(() => {
            drawFunc(canvas);
         });
         canvasHost.WaitForClose();
      }

      public static SectorArrangement2 Visualize(this SectorArrangement2 arrangement, IDebugCanvas canvasOpt) {
         var leftBound = (float)(arrangement.Bounds.Center.X - arrangement.Bounds.Extents.X);
         var rightBound = (float)(arrangement.Bounds.Center.X + arrangement.Bounds.Extents.X);
         var downBound = (float)(arrangement.Bounds.Center.Y - arrangement.Bounds.Extents.Y);
         var upBound = (float)(arrangement.Bounds.Center.Y + arrangement.Bounds.Extents.Y);
         var r = new Random(0);
         void DrawFunc(IDebugCanvas c) {
            c.BatchDraw(() => {
               foreach (var cell in arrangement.Cells) {
                  var left = cell.Left ?? new List<(DoubleVector2, int)> {
                     (new DoubleVector2(cell.StartX, upBound), -12345),
                     (new DoubleVector2(cell.EndX, upBound), -12345),
                  };
                  var right = cell.Right ?? new List<(DoubleVector2, int)> {
                     (new DoubleVector2(cell.StartX, downBound), -12345),
                     (new DoubleVector2(cell.EndX, downBound), -12345),
                  };

                  var poly = left.Concat(Enumerable.Reverse(right)).Select(x => x.Item1.ToDotNetVector()).ToArray();
                  c.FillPolygon(poly, new FillStyle(Color.FromArgb(r.Next(256), r.Next(256), r.Next(256))));
               }
            });
         }

         if (canvasOpt == null) {
            var bounds = new AxisAlignedBoundingBox2 {
               Center = new DoubleVector2((leftBound + rightBound) / 2, (upBound + downBound) / 2),
               Extents = new DoubleVector2((rightBound - leftBound) / 2, (upBound - downBound) / 2),
            };
            ShowAndWaitHelperInternal(bounds, DrawFunc);
         } else {
            DrawFunc(canvasOpt);
         }

         return arrangement;
      }

      public static Triangulation Visualize(this Triangulation triangulation, IDebugCanvas canvas) {
         AxisAlignedBoundingBox2 bounds = null;
         foreach (var island in triangulation.Islands) {
            var rect = island.TriangleIndexQuadTree.Root.Rect;
            var lowY = Math.Min(rect.Top, rect.Bottom); // TODO: Fix Y axis direction for quadtrees.
            var highY = Math.Min(rect.Top, rect.Bottom);
            var islandBounds = AxisAlignedBoundingBox2.FromExtents(rect.Left, lowY, rect.Right, highY);
            bounds = bounds == null ? islandBounds : bounds.Merge(islandBounds);
         }

         VisualizeInternal(bounds, canvas, c => {
            c.DrawTriangulation(triangulation, StrokeStyle.BlackHairLineSolid);
         });
         return triangulation;
      }
   }
}
