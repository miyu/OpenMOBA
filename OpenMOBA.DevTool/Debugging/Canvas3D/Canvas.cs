//using ItzWarty.Geometry;
//using SharpDX;
//using SharpDX.DXGI;
//using SharpDX.Toolkit.Graphics;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using ClipperLib;
//using Navi;
//using SharpDX.Direct3D11;
//using RasterizerState = SharpDX.Toolkit.Graphics.RasterizerState;
//using SamplerState = SharpDX.Toolkit.Graphics.SamplerState;

namespace Shade {
//   public class Canvas {
//      public static CanvasEngine Engine { get; set; }
//      public static GraphicsDevice GraphicsDevice => Engine.GraphicsDevice;
//
//      private readonly List<PathSegment> pathSegments = new List<PathSegment>();
//      private LinePoint pathHead = new LinePoint(Point2D.Zero, Color.Black, 1, true);
//      private double lineStyleThickness = 1;
//      private Color lineStyleColor = Color.Black;
//
//      private readonly RasterizerState rasterizerState;
//      private readonly BasicEffect effect;
//
//      public Canvas(int width, int height) {
//         Width = width;
//         Height = height;
//         RenderTarget = RenderTarget2D.New(
//            GraphicsDevice,
//            width,
//            height,
//            Format.B8G8R8A8_UNorm);
//         rasterizerState = RasterizerState.New(GraphicsDevice, new RasterizerStateDescription {
//            CullMode = CullMode.None,
//            FillMode = FillMode.Solid,
//            IsAntialiasedLineEnabled = true,
//            IsMultisampleEnabled = true
//         });
//         effect = new BasicEffect(GraphicsDevice) {
//            World = Matrix.Identity,
//            View = Matrix.Identity,
//            Projection = Matrix.OrthoOffCenterLH(0, Width, 0, Height, 1.0f, 1000.0f),
//            VertexColorEnabled = true
//         };
//      }
//
//      public int Width { get; }
//      public int Height { get; }
//      public RenderTarget2D RenderTarget { get; }
//
//      public void Clear() => Clear(Color.Transparent);
//
//      public void Clear(Color color) {
//         using (RenderTargetSwap()) {
//            GraphicsDevice.Clear(color);
//         }
//      }
//
//      public void SetLineStyle(double thickness, Color color) {
//         lineStyleThickness = thickness;
//         lineStyleColor = color;
//      }
//
//      public void BeginPath() {
//         pathSegments.Clear();
//         pathHead.IsNewPathStart = true;
//      }
//
//      public void MoveTo(double x, double y) {
//         pathHead = new LinePoint(new Point2D(x, y), lineStyleColor, lineStyleThickness, true);
//      }
//
//      public void LineTo(double x, double y) {
//         var pathTail = new LinePoint(new Point2D(x, y), lineStyleColor, lineStyleThickness, false);
//         pathSegments.Add(new PathSegment(pathHead, pathTail));
//         pathHead = pathTail;
//      }
//
//      public void Stroke() {
//         using (RenderTargetSwap()) {
//            GraphicsDevice.SetRasterizerState(rasterizerState);
//
//            foreach (var pass in effect.CurrentTechnique.Passes) {
//               pass.Apply();
//
//               var x = new PrimitiveBatch<VertexPositionColor>(GraphicsDevice);
//               x.Begin();
//
//               Color lastTailColor = Color.Transparent;
//               Point2D lastP3 = null;
//               Point2D lastP4 = null;
//               foreach (var pathSegment in pathSegments) {
//                  var a = pathSegment.Head;
//                  var b = pathSegment.Tail;
//
//                  var v = b.Location - a.Location;
//                  var vPerpUnit = v.Perp().ToUnitVector();
//                  var vR1 = vPerpUnit * a.Thickness / 2;
//                  var vR2 = vPerpUnit * b.Thickness / 2;
//
//                  var p1 = a.Location - vR1;
//                  var p2 = a.Location + vR1;
//                  var p3 = b.Location + vR2;
//                  var p4 = b.Location - vR2;
//
//                  if (!a.IsNewPathStart && lastP3 != null) {
//                     var hull = GeometryUtilities.ConvexHull(
//                        new[] { lastP3, lastP4, p1, p2 });
//                     if (hull.Length == 5) {
//                        x.DrawQuad(
//                           new VertexPositionColor(new Vector3((float)hull[0].X, (float)hull[0].Y, 100), (hull[0] == p1 || hull[0] == p2) ? a.Color : lastTailColor),
//                           new VertexPositionColor(new Vector3((float)hull[1].X, (float)hull[1].Y, 100), (hull[1] == p1 || hull[1] == p2) ? a.Color : lastTailColor),
//                           new VertexPositionColor(new Vector3((float)hull[2].X, (float)hull[2].Y, 100), (hull[2] == p1 || hull[2] == p2) ? a.Color : lastTailColor),
//                           new VertexPositionColor(new Vector3((float)hull[3].X, (float)hull[3].Y, 100), (hull[3] == p1 || hull[3] == p2) ? a.Color : lastTailColor)
//                           );
//                     } else {
//                        x.DrawTriangle(
//                           new VertexPositionColor(new Vector3((float)hull[0].X, (float)hull[0].Y, 100), (hull[0] == p1 || hull[0] == p2) ? a.Color : lastTailColor),
//                           new VertexPositionColor(new Vector3((float)hull[1].X, (float)hull[1].Y, 100), (hull[1] == p1 || hull[1] == p2) ? a.Color : lastTailColor),
//                           new VertexPositionColor(new Vector3((float)hull[2].X, (float)hull[2].Y, 100), (hull[2] == p1 || hull[2] == p2) ? a.Color : lastTailColor));
//                     }
//                  }
//
//                  lastTailColor = b.Color;
//                  lastP3 = p3;
//                  lastP4 = p4;
//
//                  x.DrawQuad(
//                        new VertexPositionColor(new Vector3((float)p1.X, (float)p1.Y, 100), a.Color),
//                        new VertexPositionColor(new Vector3((float)p2.X, (float)p2.Y, 100), a.Color),
//                        new VertexPositionColor(new Vector3((float)p3.X, (float)p3.Y, 100), b.Color),
//                        new VertexPositionColor(new Vector3((float)p4.X, (float)p4.Y, 100), b.Color)
//                     );
//               }
//               x.End();
//            }
//         }
//      }
//
//      public void FillPath(Color fillColor) {
//         var fillClipper = new Clipper(Clipper.ioStrictlySimple);
//         var points = pathSegments.SelectMany(s => new[] { new IntPoint(s.Head.Location.X * 1000, s.Head.Location.Y * 1000), new IntPoint(s.Tail.Location.X * 1000, s.Tail.Location.Y * 1000) });
//         fillClipper.AddPath(points.ToList(), PolyType.ptSubject, true);
//         var polytree = new PolyTree();
//         fillClipper.Execute(ClipType.ctUnion, polytree, PolyFillType.pftEvenOdd, PolyFillType.pftNonZero);
//         var triangles = new Triangulator().TriangulateComplex(polytree);
//
//         using (RenderTargetSwap()) {
//            GraphicsDevice.SetRasterizerState(rasterizerState);
//
//            foreach (var pass in effect.CurrentTechnique.Passes) {
//               pass.Apply();
//               var x = new PrimitiveBatch<VertexPositionColor>(GraphicsDevice);
//               x.Begin();
//               foreach (var triangle in triangles) {
//                  var p1 = triangle.Points[0];
//                  var p2 = triangle.Points[1];
//                  var p3 = triangle.Points[2];
//                  x.DrawTriangle(
//                     new VertexPositionColor(new Vector3((float)p1.X, (float)p1.Y, 100), fillColor),
//                     new VertexPositionColor(new Vector3((float)p2.X, (float)p2.Y, 100), fillColor),
//                     new VertexPositionColor(new Vector3((float)p3.X, (float)p3.Y, 100), fillColor)
//                     );
//               }
//               x.End();
//            }
//         }
//      }
//
//      private IDisposable RenderTargetSwap() {
//         return new RenderTargetSwitchContext(GraphicsDevice, RenderTarget);
//      }
//
//      private class RenderTargetSwitchContext : IDisposable {
//         private readonly GraphicsDevice graphicsDevice;
//         private readonly RenderTarget2D target;
//         private readonly DepthStencilView oldDepthStencil;
//         private readonly RenderTargetView[] oldRenderTargets;
//
//         public RenderTargetSwitchContext(GraphicsDevice graphicsDevice, RenderTarget2D target) {
//            this.graphicsDevice = graphicsDevice;
//            this.target = target;
//            oldRenderTargets = graphicsDevice.GetRenderTargets(out oldDepthStencil);
//            graphicsDevice.SetRenderTargets(target);
//         }
//
//         public void Dispose() {
//            graphicsDevice.SetRenderTargets(oldDepthStencil, oldRenderTargets);
//         }
//      }
//
//      internal class LinePoint {
//         public LinePoint(Point2D location, Color color, double thickness, bool isNewPathStart) {
//            Location = location;
//            Color = color;
//            Thickness = thickness;
//            IsNewPathStart = isNewPathStart;
//         }
//
//         public Point2D Location { get; private set; }
//         public Color Color { get; private set; }
//         public double Thickness { get; private set; }
//         public bool IsNewPathStart { get; set; }
//      }
//
//      internal class PathSegment {
//         public PathSegment(LinePoint head, LinePoint tail) {
//            Head = head;
//            Tail = tail;
//         }
//
//         public LinePoint Head { get; private set; }
//         public LinePoint Tail { get; private set; }
//      }
//   }
}