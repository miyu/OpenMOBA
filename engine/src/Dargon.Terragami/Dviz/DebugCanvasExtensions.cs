using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dargon.Commons;
using Dargon.Dviz;

namespace Dargon.Terragami.Dviz {
   public static class DebugCanvasExtensions {
      public static void DrawPolygonNode(this IDebugCanvas canvas, PolygonNode polytree, StrokeStyle landStroke = null, StrokeStyle holeStroke = null) {
         landStroke = landStroke ?? new StrokeStyle(Color.Black); // Orange
         holeStroke = holeStroke ?? new StrokeStyle(Color.Red); // Brown

         canvas.BatchDraw(() => {
            var s = new Stack<PolygonNode>();
            s.Push(polytree);
            while (s.Any()) {
               var node = s.Pop();
               node.Children.ForEach(s.Push);
               if (node.Contour != null)
                  canvas.DrawPolygonContour(
                     node.Contour.Map(p => new Vector2(p.X, p.Y)).ToList(),
                     node.IsHole ? holeStroke : landStroke);
            }
         });
      }
   }
}
