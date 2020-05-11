using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using Xunit;
using static NMockito.NMockitoStatics;

namespace Dargon.Terragami.Tests {
   public class ArrangementOfLinesTests {
      [Fact]
      public void Test() {
         var canvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(new Size(100, 100), new Point(125, 125));
         var canvas = canvasHost.CreateAndAddCanvas(0);
         var segments = new[] {
            /* 0 */ new DoubleLineSegment2(new DoubleVector2(0, 0), new DoubleVector2(100, 0)),
            /* 1 */ new DoubleLineSegment2(new DoubleVector2(0, 100), new DoubleVector2(100, 100)),
            /* 2 */ new DoubleLineSegment2(new DoubleVector2(0, 0), new DoubleVector2(0, 100)),
            /* 3 */ new DoubleLineSegment2(new DoubleVector2(100, 0), new DoubleVector2(100, 100)),
            /* 4 */ new DoubleLineSegment2(new DoubleVector2(0, 0), new DoubleVector2(100, 100)),
            /* 5 */ new DoubleLineSegment2(new DoubleVector2(100, 0), new DoubleVector2(0, 100)),
            /* 6 */ new DoubleLineSegment2(new DoubleVector2(-100, 0), new DoubleVector2(0, 100)),
            /* 7 */ new DoubleLineSegment2(new DoubleVector2(200, 0), new DoubleVector2(100, 100)),
            /* 8 */ new DoubleLineSegment2(new DoubleVector2(100, 0), new DoubleVector2(200, 100)),
            /* 9 */ new DoubleLineSegment2(new DoubleVector2(0, 0), new DoubleVector2(-100, 100)),
         };
         var res = SectorArrangement2.Create(
            segments, 
            new AxisAlignedBoundingBox2 {
               Center = new DoubleVector2(50, 50),
               Extents = new DoubleVector2(200, 200), // + some padding
            }, 
            canvas);
         res.Visualize(canvas);

         canvas.DrawLineList(segments, StrokeStyle.LimeThick5Solid);
      }
   }
}
