using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public class DebugCanvasHost {
      private readonly Form form;
      private readonly PictureBox pb;

      public DebugCanvasHost(DebugCanvas canvas) {
         const double scale = 1.0f;
         form = new Form { ClientSize = new Size((int)(canvas.PaddedSize.Width * scale), (int)(canvas.PaddedSize.Height * scale)) };
         form.BackColor = Color.White;
         form.StartPosition = FormStartPosition.CenterScreen;
         pb = new PictureBox { Dock = DockStyle.Fill };
         pb.SizeMode = PictureBoxSizeMode.Zoom;
         form.Controls.Add(pb);

         canvas.Update += (s, e) => {
            Bitmap clonedBitmap = (Bitmap)e.Bitmap.Clone();
            form.BeginInvoke(new Action(() => {
               pb.Image = clonedBitmap;
            }));
         };
      }

      private static DebugCanvasHost CreateShow(DebugCanvas canvas) {
         DebugCanvasHost canvasHost = null;
         var shownLatch = new ManualResetEvent(false);
         var thread = new Thread(() => {
            canvasHost = new DebugCanvasHost(canvas);
            canvasHost.form.Shown += (s, e) => shownLatch.Set();
            Application.Run(canvasHost.form);
         });
         thread.SetApartmentState(ApartmentState.STA);
         thread.Start();
         shownLatch.WaitOne();
         return canvasHost;
      }

      public static DebugCanvas CreateAndShowCanvas(Size displaySize = default(Size)) {
         displaySize = displaySize == default(Size) ? new Size(1000, 1000) : displaySize;
         var drawPadding = new Point(100, 100);

         var canvas = new DebugCanvas(displaySize, drawPadding, OrthographicXYProjector.Instance);
         DebugCanvasHost.CreateShow(canvas);

         return canvas;
      }
   }

   public interface IDebugMultiCanvasHost {
      IDebugCanvas CreateAndAddCanvas(int timestamp);
   }

   public class StrokeStyle {
      public StrokeStyle(Color? color = null, double thickness = 1.0, float[] dashPattern = null) {
         Color = color ?? Color.Black;
         Thickness = thickness;
         DashPattern = dashPattern;
      }

      public Color Color;
      public double Thickness;
      public float[] DashPattern;
      public bool DisableStrokePerspective;
   }

   public class FillStyle {
      public FillStyle(Color? color = null) {
         Color = color ?? Color.Black;
      }

      public Color Color;
   }

   public interface IDebugCanvas {
      void BatchDraw(Action callback);

      void DrawPoint(DoubleVector3 p, StrokeStyle strokeStyle);
      void DrawLine(DoubleVector3 p1, DoubleVector3 p2, StrokeStyle strokeStyle);
      void FillPolygon(IReadOnlyList<DoubleVector3> points, FillStyle fillStyle);
      void DrawPolygon(IReadOnlyList<DoubleVector3> polygonPoints, StrokeStyle strokeStyle);
   }

   public interface IProjector {
      DoubleVector2 Project(DoubleVector3 p);
      double ComputeApparentThickness(DoubleVector3 p, double thickness);
   }

   public class OrthographicXYProjector : IProjector {
      public static readonly OrthographicXYProjector Instance = new OrthographicXYProjector();

      public DoubleVector2 Project(DoubleVector3 p) {
         return p.XY;
      }

      public double ComputeApparentThickness(DoubleVector3 p, double thickness) {
         return thickness;
      }
   }

   public class PerspectiveProjector : IProjector {
      private readonly DoubleVector3 position;
      private readonly DoubleVector3 lookat;
      private readonly DoubleVector3 up;
      private readonly double width;
      private readonly double height;
      private readonly Matrix4x4 worldToCamera;
      private readonly Matrix4x4 cameraToView;
      private readonly Matrix4x4 transform;

      public PerspectiveProjector(DoubleVector3 position, DoubleVector3 lookat, DoubleVector3 up, double width, double height) {
         this.position = position;
         this.lookat = lookat;
         this.up = up;
         this.width = width;
         this.height = height;
         this.worldToCamera = Matrix4x4.CreateTranslation(ToNumerics3(-1.0 * position)) *
            Matrix4x4.CreateLookAt(ToNumerics3(DoubleVector3.Zero), ToNumerics3(position.To(lookat)), ToNumerics3(up));
         this.cameraToView = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI * 2 / 4, (float)(width / height), 1f, 10000f);
//          this.cameraToView = Matrix4x4.CreatePerspectiveOffCenter(0, (float)width, (float)height, 0, 1.0f, 1000.0f);
         transform = cameraToView * worldToCamera;
      }

      public DoubleVector2 Project(DoubleVector3 input) {
         var p = ToNumerics4(input);
         var cameraSpace = Vector4.Transform(p, worldToCamera);
//         return new DoubleVector2(width * (1.0 + cameraSpace.X / cameraSpace.Z) / 2.0, height * (1.0 + cameraSpace.Y / cameraSpace.Z) / 2.0);
         var viewSpace = Vector4.Transform(cameraSpace, cameraToView);
         viewSpace /= viewSpace.W; // I'm doing something wrong.
//         Console.WriteLine(p + " " + cameraSpace + " " + viewSpace + " " + (viewSpace / viewSpace.W));

         return new DoubleVector2(width * (-viewSpace.X + 1.0) / 2.0, height * (-viewSpace.Y + 1.0) / 2.0);
//         return new DoubleVector2(viewSpace.X, viewSpace.Y);
      }

      public double ComputeApparentThickness(DoubleVector3 p, double thickness) {
         var ss1 = Project(p);
         var ss2 = Project(p + position.To(p).Cross(position.To(lookat)).ToUnit() * thickness);

         return (ss1 - ss2).Norm2D();
         //         var cameraSpace = Vector4.Transform(ToNumerics4(p), worldToCamera);
         //         return thickness * (1000f + cameraSpace.Z) / 1000f;
      }

      private static Vector3 ToNumerics3(DoubleVector3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
      private static Vector4 ToNumerics4(DoubleVector3 v) => new Vector4((float)v.X, (float)v.Y, (float)v.Z, 1.0f);
   }

   public class DebugCanvas : IDebugCanvas {
      private readonly Bitmap bitmap;

      private readonly object synchronization = new object();
      private readonly Point drawPadding;
      private readonly IProjector projector;
      private readonly PictureBox pb;

      public event EventHandler<DebugCanvasUpdateEventArgs> Update;

      public class DebugCanvasUpdateEventArgs : EventArgs {
         public Bitmap Bitmap { get; set; }
      }

      public DebugCanvas(Size displaySize, Point drawPadding, IProjector projector) {
         this.drawPadding = drawPadding;
         this.projector = projector;
         PaddedSize = new Size(displaySize.Width + 2 * drawPadding.X, displaySize.Height + 2 * drawPadding.Y);
         bitmap = new Bitmap(PaddedSize.Width, PaddedSize.Height);
      }

      public Size PaddedSize { get; }

      public Font Font => SystemFonts.DefaultFont;

      private bool isRecursion = false;
      private Graphics g;

      public void BatchDraw(Action callback) {
         lock (synchronization) {
            if (!isRecursion) {
               using (g = Graphics.FromImage(bitmap)) {
                  g.CompositingQuality = CompositingQuality.HighQuality;
                  g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                  g.SmoothingMode = SmoothingMode.AntiAlias;
                  g.Transform = new Matrix(1, 0, 0, 1, drawPadding.X, drawPadding.Y);
                  isRecursion = true;
                  callback();
                  isRecursion = false;
               }
               g = null;
               UpdateDisplay();
            } else {
               callback();
            }
         }
      }

      private DoubleVector2 Project(DoubleVector3 p) => projector.Project(p);

      private PointF ProjectPointF(DoubleVector3 p) {
         var proj = Project(p);
         return new PointF((float)proj.X, (float)proj.Y);
      }

      private float ProjectThickness(DoubleVector3 p, double thickness) => (float)projector.ComputeApparentThickness(p, thickness);

      public void DrawPoint(DoubleVector3 point, StrokeStyle strokeStyle) {
         BatchDraw(() => {
            var p = Project(point);
            var x = (float)p.X;
            var y = (float)p.Y;
            var radius = ProjectThickness(point, strokeStyle.Thickness) / 2.0f;
            var rect = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
            using (var brush = new SolidBrush(strokeStyle.Color)) {
               g.FillEllipse(brush, rect);
            }
         });
      }

      public void DrawLine(DoubleVector3 point1, DoubleVector3 point2, StrokeStyle strokeStyle) {
         DepthDrawLineStrip(new [] { point1, point2 }, strokeStyle);
      }

      public void FillPolygon(IReadOnlyList<DoubleVector3> points, FillStyle fillStyle) {
         BatchDraw(() => {
            var ps = points.Select(Project).ToList();
            using (var brush = new SolidBrush(fillStyle.Color)) {
               g.FillPolygon(brush, ps.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray());
            }
         });
      }

      public void DrawPolygon(IReadOnlyList<DoubleVector3> points, StrokeStyle strokeStyle) {
         DepthDrawLineStrip(points.Concat(new [] { points[0] }).ToList(), strokeStyle);
      }

      private void DepthDrawLineStrip(IReadOnlyList<DoubleVector3> points, StrokeStyle strokeStyle) {
         BatchDraw(() => {
            if (strokeStyle.DisableStrokePerspective) {
               if (points.Count <= 1) return;

               using (var pen = new Pen(strokeStyle.Color, ProjectThickness(points[0], strokeStyle.Thickness))) {
                  g.DrawLines(pen, points.Select(ProjectPointF).ToArray());
                  return;
               }
            }

            var thicknesses = points.Select(p => ProjectThickness(p, strokeStyle.Thickness)).ToList();
            const int thicknessMultiplier = 100;
            var segmentsByThicknessKey = new Dictionary<int, List<PointF>>();

            int ComputeThicknessKey(float thickness) {
               return (int)(thickness * thicknessMultiplier);
            }

            for (var i = 0; i < points.Count - 1; i++) {
               var p1 = points[i];
               var p2 = points[i + 1];
               var p1p2 = p1.To(p2);
               var t1 = thicknesses[i];
               var t2 = thicknesses[i + 1];
               const int nsegs = 10;
               for (var part = 0; part < nsegs; part++) {
                  const int maxPart = nsegs - 1;
                  var pa = p1 + p1p2 * (float)(part / (float)nsegs);
                  var pb = p1 + p1p2 * ((float)(part + 1) / (float)nsegs);
                  var t = (t1 * (maxPart - part) + t2 * part) / maxPart;
                  var tkey = ComputeThicknessKey(t);
                  List<PointF> segments;
                  if (!segmentsByThicknessKey.TryGetValue(tkey, out segments)) {
                     segments = new List<PointF>();
                     segmentsByThicknessKey[tkey] = segments;
                  }
                  segments.Add(ProjectPointF(pa));
                  segments.Add(ProjectPointF(pb));
               }
//               Console.WriteLine(ProjectPointF(p1) + " " + ProjectPointF(p2));
            }

            using (var pen = new Pen(strokeStyle.Color)) {
               pen.StartCap = LineCap.Round;
               pen.EndCap = LineCap.Round;
               foreach (var kvp in segmentsByThicknessKey) {
                  pen.Width = kvp.Key / (float)thicknessMultiplier;
                  if (strokeStyle.DashPattern != null) {
                     pen.DashPattern = strokeStyle.DashPattern;
                  }
                  for (var i = 0; i < kvp.Value.Count; i += 2) {
                     g.DrawLine(pen, kvp.Value[i], kvp.Value[i + 1]);
                  }
               }
            }
         });
      }

      public void DrawText(string text, DoubleVector3 point) {
         BatchDraw(() => {
            var p = Project(point);
            g.DrawString(text, Font, Brushes.Black, (float)p.X, (float)p.Y);
         });
      }

      private void UpdateDisplay() {
         lock (synchronization) {
            Update?.Invoke(this, new DebugCanvasUpdateEventArgs { Bitmap = bitmap });
         }
      }

      public void EnterBitmapCriticalSection(Action<Bitmap> callback) {
         lock (synchronization) {
            callback(bitmap);
         }
      }
   }
}
