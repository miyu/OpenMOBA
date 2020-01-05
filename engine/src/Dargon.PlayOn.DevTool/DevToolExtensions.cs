using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dargon.Dviz;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;
using SharpDX;
using Color = System.Drawing.Color;
using Vector3 = System.Numerics.Vector3;

namespace Dargon.PlayOn.DevTool {
   public static class DevToolExtensions {
      private static readonly StrokeStyle PathStroke = new StrokeStyle(Color.Lime, 1.0);

      public static void DrawEntityPaths(this IDebugCanvas debugCanvas, EntityWorld entityWorld) {
         foreach (var (i, entity) in entityWorld.EnumerateEntities().Enumerate()) {
            var mc = entity.MotionComponent;
            if (mc?.Internals.Steering.Roadmap == null || mc?.Internals.Swarm != null || !mc.Internals.Steering.IsPathfindingEnabled()) continue;
            DrawRoadmap(debugCanvas, mc.Internals.Steering.Roadmap, mc);
         }
      }

      public static void DrawRoadmap(this IDebugCanvas debugCanvas, MotionRoadmap roadmap, MotionComponent motionComponent = null) {
         var skip = motionComponent?.Internals.Steering.RoadmapProgressIndex ?? 0;
         foreach (var (i, action) in roadmap.Plan.Skip(skip).Enumerate()) {
            switch (action) {
               case MotionRoadmapWalkAction walk:
                  debugCanvas.Transform = Matrix4x4.Identity;
                  var s = i == 0 && motionComponent != null
                     ? motionComponent.Internals.Pose.WorldPosition
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

