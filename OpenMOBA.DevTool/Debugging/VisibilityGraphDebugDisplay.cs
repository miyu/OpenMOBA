using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class VisibilityGraphDebugDisplay {
      private static readonly StrokeStyle BarrierStrokeStyle = new StrokeStyle(Color.DimGray);
      private static readonly StrokeStyle VisibilityEdgeStrokeStyle = new StrokeStyle(Color.Cyan);
      private static readonly StrokeStyle WaypointStrokeStyle = new StrokeStyle(Color.Red, 5.0f);

      public static void DrawVisibilityGraph(this IDebugCanvas canvas, SectorVisibilityGraph visibilityGraph) {
         canvas.DrawLineList(
            visibilityGraph.Barriers.SelectMany(barrier => barrier.Points.Select(p => new IntVector3(p))).ToList(),
            BarrierStrokeStyle);

         if (visibilityGraph.Waypoints.Any()) {
            canvas.DrawLineList(
               (from sourceIndex in Enumerable.Range(0, visibilityGraph.Waypoints.Length)
                let sourceWaypoint = new IntVector3(visibilityGraph.Waypoints[sourceIndex])
                let offset = visibilityGraph.Offsets[sourceIndex]
                let end = visibilityGraph.Offsets[sourceIndex + 1]
                from edgeIndex in Enumerable.Range(offset, end - offset)
                let edge = visibilityGraph.Edges[edgeIndex]
                let destWaypoint = new IntVector3(visibilityGraph.Waypoints[edge.NextIndex])
                select new IntLineSegment3(sourceWaypoint, destWaypoint)).ToList(),
               VisibilityEdgeStrokeStyle);
            canvas.DrawPoints(
               visibilityGraph.Waypoints.Select(p => new IntVector3(p)).ToList(),
               WaypointStrokeStyle);
         }
      }
   }
}
