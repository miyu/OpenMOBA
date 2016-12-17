using System.Drawing;
using System.Linq;
using OpenMOBA.Debugging;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Visibility {
   public static class VisibilityGraphDebugDisplay {
      public static void DrawVisibilityGraph(this DebugDisplay display, VisibilityGraph visibilityGraph) {
         display.DrawLineList(
            visibilityGraph.Barriers.SelectMany(barrier => barrier.Points).ToList(),
            Pens.DimGray);
         display.DrawLineList(
            (from i in Enumerable.Range(0, visibilityGraph.Waypoints.Length - 1)
               from j in Enumerable.Range(i + 1, visibilityGraph.Waypoints.Length - i - 1)
               where !double.IsNaN(visibilityGraph.Distances[i, j])
               select new IntLineSegment2(visibilityGraph.Waypoints[i], visibilityGraph.Waypoints[j])).ToList(),
            Pens.Cyan);
         display.DrawPoints(
            visibilityGraph.Waypoints,
            Brushes.Red);
      }
   }
}
