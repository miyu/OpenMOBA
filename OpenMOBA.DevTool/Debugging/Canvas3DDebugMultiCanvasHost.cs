using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Canvas3D;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public class Canvas3DDebugMultiCanvasHost : IDebugMultiCanvasHost {
      public IDebugCanvas CreateAndAddCanvas(int timestamp) {
      }

      public static void Create(Size size) {
         var thread = new Thread(() => {
            var graphicsLoop = GraphicsLoop.CreateWithNewWindow(size);
            while(graphicsLoop.IsRunning(out var renderer)) {
            }
         });
         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
      }

      public class Canvas3DDebugCanvas : IDebugCanvas {
         public Matrix4x4 Transform { get; set; }

         public void BatchDraw(Action callback) {
            throw new NotImplementedException();
         }

         public void DrawPoint(DoubleVector3 p, StrokeStyle strokeStyle) {
            throw new NotImplementedException();
         }

         public void DrawLine(DoubleVector3 p1, DoubleVector3 p2, StrokeStyle strokeStyle) {
            throw new NotImplementedException();
         }

         public void FillPolygon(IReadOnlyList<DoubleVector3> points, FillStyle fillStyle) {
            throw new NotImplementedException();
         }

         public void DrawPolygon(IReadOnlyList<DoubleVector3> polygonPoints, StrokeStyle strokeStyle) {
            throw new NotImplementedException();
         }

         public void DrawText(string text, DoubleVector3 point) {
            throw new NotImplementedException();
         }
      }
   }
}
