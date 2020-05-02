using System;
using System.Collections.Generic;
using Dargon.Commons;
using Dargon.Commons.Pooling;
using Dargon.PlayOn.Geometry;

using cInt = System.Int32;

namespace Dargon.Terragami.Sectors {
   public class BarrierCalculator {
      // To compute barriers, dilate polytrees (so hole regions are away from waypoints
      // then expand segments so they cross each other and are watertight
      private const int kBarrierPolyTreeDilationFactor = 5; // dilation to move holes inward
      private const int kBarrierSegmentExpansionFactor = 10; // expansion to make corners hit
      private const int kBarrierOverDilationFactor = 3;

      public static TlsBackedObjectPool<List<IntLineSegment2>> tlsFindContourAndChildHoleBarriersStore = TlsBackedObjectPool.Create<List<IntLineSegment2>>();

      public IntLineSegment2[] CalculateContourAndChildHoleBarriers(PolygonNode root, int exaggerationFactor = 10) {
         var results = tlsFindContourAndChildHoleBarriersStore.UnsafeTakeAndGive();
         results.Clear();
         foreach (var node in root.Dfs((cb, n) => n.Children.ForEach(cb))) {
            if (node.Contour == null) continue;

            var pointCount = node.Contour.Length;
            var isHole = node.IsHole;
            var dilationDirection = isHole ? 1 : 1; // Note: Same value, as hole clockness is opposite of land clockness.

            // TODO: This algo is far faster than dilating the polynode like I did before. However, it probably introduces
            // leaks for sharp corners. Consider working around that by instead dilating corners along their pointed direction
            // & then expanding segments after that.
            for (var i = 0; i < pointCount; i++) {
               var a = node.Contour[i];
               var b = node.Contour[(i + 1) % pointCount];

               var dx = b.X - a.X;
               var dy = b.Y - a.Y;
               var mag = (cInt)Math.Sqrt(dx * dx + dy * dy); // normalizing on xy plane.

               // Move segment toward outside of node.
               var dilateOffsetX = exaggerationFactor * dilationDirection * dy * kBarrierPolyTreeDilationFactor / mag;
               var dilateOffsetY = exaggerationFactor * dilationDirection * -dx * kBarrierPolyTreeDilationFactor / mag;

               // Expand segment to fill leaks at endpoint.
               var expandOffsetX = exaggerationFactor * dx * kBarrierSegmentExpansionFactor / mag;
               var expandOffsetY = exaggerationFactor * dy * kBarrierSegmentExpansionFactor / mag;

               var p1 = new IntVector2(a.X - expandOffsetX + dilateOffsetX, a.Y - expandOffsetY + dilateOffsetY);
               var p2 = new IntVector2(b.X + expandOffsetX + dilateOffsetX, b.Y + expandOffsetY + dilateOffsetY);

               results.Add(new IntLineSegment2(p1, p2));
               // results.Add(isHole ? new IntLineSegment2(p2, p1) : new IntLineSegment2(p1, p2));
               // results.Add(isHole ? new IntLineSegment2(p2, p1) : new IntLineSegment2(p1, p2));
            }
         }
         return results.ToArray();
      }
   }
}
