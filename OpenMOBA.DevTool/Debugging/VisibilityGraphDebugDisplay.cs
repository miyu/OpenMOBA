using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class VisibilityGraphDebugDisplay {
      public static void DrawVisibilityGraph(this DebugCanvas canvas, VisibilityGraph visibilityGraph) {
         canvas.DrawLineList(
            visibilityGraph.Barriers.SelectMany(barrier => barrier.Points.Select(p => p.XY)).ToList(),
            Pens.DimGray);
         if (visibilityGraph.Waypoints.Any()) {
            canvas.DrawLineList(
               (from i in Enumerable.Range(0, visibilityGraph.Waypoints.Length - 1)
                  from j in Enumerable.Range(i + 1, visibilityGraph.Waypoints.Length - i - 1)
                  where !double.IsNaN(visibilityGraph.Distances[i, j])
                  select new IntLineSegment2(visibilityGraph.Waypoints[i].XY, visibilityGraph.Waypoints[j].XY)).ToList(),
               Pens.Cyan);
            canvas.DrawPoints(
               visibilityGraph.Waypoints.Select(p => p.XY).ToList(),
               Brushes.Red);
         }
      }
   }
}
