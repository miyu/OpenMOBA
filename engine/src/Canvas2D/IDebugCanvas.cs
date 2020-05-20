using System;
using System.Collections.Generic;
using System.Numerics;

namespace Dargon.Dviz {
   public interface IDebugCanvas {
      Matrix4x4 Transform { get; set; }
      void BatchDraw(Action callback);

      void DrawPoint(Vector3 p, StrokeStyle strokeStyle);
      void DrawLine(Vector3 p1, Vector3 p2, StrokeStyle strokeStyle);
      void DrawVector(Vector3 p1, Vector3 p2, StrokeStyle strokeStyle, float arrowheadScale = 1);
      void DrawTriangle(Vector3 p1, Vector3 p2, Vector3 p3, StrokeStyle strokeStyle);
      void FillTriangle(Vector3 p1, Vector3 p2, Vector3 p3, FillStyle fillStyle);
      void FillPolygon(IReadOnlyList<Vector3> polygonPoints, FillStyle fillStyle);
      void DrawPolygon(IReadOnlyList<Vector3> polygonPoints, StrokeStyle strokeStyle);
      void DrawText(string text, Vector3 point);
   }
}