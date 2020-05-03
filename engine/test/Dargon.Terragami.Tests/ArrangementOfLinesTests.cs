using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Xunit;
using static NMockito.NMockitoStatics;

namespace Dargon.Terragami.Tests {
   public class ArrangementOfLinesTests {
      [Fact]
      public void Test() {
         var canvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(new Size(100, 100), new Point(125, 125));
         var canvas = canvasHost.CreateAndAddCanvas(0);
         var res = SectorArrangement2.Create(
            new[] {
               new DoubleLineSegment2(new DoubleVector2(0, 0), new DoubleVector2(100, 0)),
               new DoubleLineSegment2(new DoubleVector2(0, 100), new DoubleVector2(100, 100)),
               new DoubleLineSegment2(new DoubleVector2(0, 0), new DoubleVector2(0, 100)),
               new DoubleLineSegment2(new DoubleVector2(100, 0), new DoubleVector2(100, 100)),
            }, 
            new AxisAlignedBoundingBox2 {
               Center = new DoubleVector2(50, 50),
               Extents = new DoubleVector2(100, 100), // + some padding
            }, 
            canvas);
         res.NoOp();
      }
   }
}
