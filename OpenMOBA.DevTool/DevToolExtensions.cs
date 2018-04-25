using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Geometry;
using SharpDX;
using Color = System.Drawing.Color;
using Vector3 = System.Numerics.Vector3;

namespace OpenMOBA.DevTool {
   public static class DevToolExtensions {
      private static readonly StrokeStyle PathStroke = new StrokeStyle(Color.Lime, 1.0);

      public static void DrawEntityPaths(this IDebugCanvas debugCanvas, EntityService entityService) {
         foreach (var (i, entity) in entityService.EnumerateEntities().Enumerate()) {
            var mc = entity.MovementComponent;
            if (mc?.PathingRoadmap == null || mc?.Swarm != null || !mc.IsPathfindingEnabled) continue;
            DrawRoadmap(debugCanvas, mc.PathingRoadmap, mc);
         }
      }

      public static void DrawRoadmap(this IDebugCanvas debugCanvas, MotionRoadmap roadmap, MovementComponent movementComponent = null) {
         var skip = movementComponent?.PathingRoadmapProgressIndex ?? 0;
         foreach (var (i, action) in roadmap.Plan.Skip(skip).Enumerate()) {
            switch (action) {
               case MotionRoadmapWalkAction walk:
                  debugCanvas.Transform = Matrix4x4.Identity;
                  var s = i == 0 && movementComponent != null
                     ? movementComponent.WorldPosition
                     : Vector3.Transform(new Vector3(walk.Source.X, walk.Source.Y, 0), walk.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                  var t = Vector3.Transform(new Vector3(walk.Destination.X, walk.Destination.Y, 0), walk.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                  //                     Console.WriteLine("S: " + s + "\t AND T: " + t);
                  //                     for (var i = 0; i < 100; i++) {
                  //                        debugCanvas.DrawPoint((s * (100 - i) + t * i) / 100, new StrokeStyle(Color.Cyan, 50));
                  //                     }
                  debugCanvas.DrawLine(s, t, PathStroke);
                  break;
            }
         }
      }
   }
}

