using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami.Dviz {
   public static class SceneVisualizerUtils {
      public static Polygon2 Visualize(this Polygon2 poly, bool labelIndices = false) {
         var tree = PolygonNode.CreateRootHole(PolygonNode.Create(poly.Points.ToArray(), false));
         VisualizeInternal(tree, c => {
            c.DrawPolygonContour(poly, StrokeStyle.BlackHairLineSolid);
            
            if (labelIndices) {
               foreach (var (i, p) in poly.Points.Enumerate()) {
                  c.DrawText(i.ToString(), p.ToDotNetVector());
               }
            }
         });
         return poly;
      }

      public static PolygonNode Visualize(this PolygonNode node, bool labelIndices = false) {
         return VisualizeInternal(node, c => {
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
      }

      private static PolygonNode VisualizeInternal(PolygonNode node, Action<IDebugCanvas> drawFunc) {
         var lower = new Point(int.MaxValue, int.MaxValue);
         var upper = new Point(int.MinValue, int.MinValue);

         foreach (var c in node.Bfs((push, n) => n.Children.ForEach(push))) {
            if (c.Contour == null) continue;
            foreach (var p in c.Contour) {
               lower.X = Math.Min(lower.X, p.X);
               lower.Y = Math.Min(lower.Y, p.Y);

               upper.X = Math.Max(upper.X, p.X);
               upper.Y = Math.Max(upper.Y, p.Y);
            }
         }

         var displaySize = new Size(800, 500);
         var paddingSize = new Point(125, 125);
         var dx = upper.X - lower.X;
         var dy = upper.Y - lower.Y;
         var displayScale =
            MathF.Min((float)displaySize.Width / dx, (float)displaySize.Height / dy);
         displayScale = float.IsInfinity(displayScale) ? 1 : displayScale; // handle 0 w & h

         var cameraPosition = (new Vector2((lower.X + upper.X) / 2.0f, (lower.Y + upper.Y) / 2.0f));
         var projector = new OrthographicXYProjector(displayScale, cameraPosition, new Vector2(displaySize.Width / 2, displaySize.Height / 2), true);
         var canvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(displaySize, paddingSize, projector);
         drawFunc(canvasHost.CreateAndAddCanvas(0));
         canvasHost.WaitForClose();
         return node;
      }
   }
}
