using System;
using System.Numerics;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.DevTool.Debugging {
   public interface IDebugCanvas {
      Matrix4x4 Transform { get; set; }
      void BatchDraw(Action callback);

      void DrawPoint(DoubleVector3 p, StrokeStyle strokeStyle);
      void DrawLine(DoubleVector3 p1, DoubleVector3 p2, StrokeStyle strokeStyle);
      void DrawTriangle(DoubleVector3 p1, DoubleVector3 p2, DoubleVector3 p3, StrokeStyle strokeStyle);
      void FillTriangle(DoubleVector3 p1, DoubleVector3 p2, DoubleVector3 p3, FillStyle fillStyle);
      void DrawText(string text, DoubleVector3 point);
   }
}