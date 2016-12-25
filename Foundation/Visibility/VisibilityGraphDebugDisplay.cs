using System.Drawing;
using System.Linq;
using OpenMOBA.Debugging;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Visibility {
   public static class VisibilityGraphDebugDisplay {
      public static void DrawVisibilityGraph(this DebugCanvas canvas, VisibilityGraph visibilityGraph) {
         canvas.DrawLineList(
            visibilityGraph.Barriers.SelectMany(barrier => barrier.Points).ToList(),
            Pens.DimGray);
         if (visibilityGraph.Waypoints.Any()) {
            canvas.DrawLineList(
               (from i in Enumerable.Range(0, visibilityGraph.Waypoints.Length - 1)
                  from j in Enumerable.Range(i + 1, visibilityGraph.Waypoints.Length - i - 1)
                  where !double.IsNaN(visibilityGraph.Distances[i, j])
                  select new IntLineSegment2(visibilityGraph.Waypoints[i], visibilityGraph.Waypoints[j])).ToList(),
               Pens.Cyan);
            canvas.DrawPoints(
               visibilityGraph.Waypoints,
               Brushes.Red);
         }
      }
   }
}
