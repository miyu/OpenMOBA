using System.Drawing;
using OpenMOBA.DataStructures;

namespace OpenMOBA.DevTool.Debugging {
   public static class BvhDebugDisplay {
      private static readonly StrokeStyle StrokeStyle1 = new StrokeStyle(Color.Red, 5, new[] { 3.0f, 1.0f });
      private static readonly StrokeStyle StrokeStyle2 = new StrokeStyle(Color.Lime, 5, new[] { 1.0f, 3.0f });

      public static void DrawBvh(this IDebugCanvas canvas, BvhILS2 bvh) {
         void Helper(BvhILS2 node, int d) {
            if (d != 0) {
               var s = new StrokeStyle(d % 2 == 0 ? Color.Red : Color.Lime, 10.0f / d, new[] { d % 2 == 0 ? 1.0f : 3.0f, d % 2 == 0 ? 3.0f : 1.0f });
               canvas.DrawRectangle(node.Bounds, 0.0f, s);
            }
            if (node.First != null) {
               Helper(node.First, d + 1);
               Helper(node.Second, d + 1);
            }
         }
         Helper(bvh, 0);
      }
   }
}