using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using Dargon.Commons;
using Dargon.Commons.Collections;

namespace Dargon.Dviz {
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

         var displayUpdateLock = new object();
         Bitmap bitmapToDisplay = null;
         Bitmap lastDisplayedBitmap = null;
         canvas.Update += (s, e) => {
            bool shouldBeginInvoke = false;
            lock (displayUpdateLock) {
               shouldBeginInvoke = bitmapToDisplay == null;
               bitmapToDisplay?.Dispose();
               bitmapToDisplay = (Bitmap)e.Bitmap.Clone();
            }
            if (shouldBeginInvoke) {
               form.BeginInvoke(new Action(() => {
                  Bitmap b;
                  lock (displayUpdateLock) {
                     b = bitmapToDisplay;
                     bitmapToDisplay = null;
                  }
                  pb.Image = b;
                  lastDisplayedBitmap?.Dispose();
                  lastDisplayedBitmap = b;
               }));
            }
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
      IDebugCanvas CreateAndAddCanvas(int frameIndex);
   }

   public interface IProjector {
      Vector2 Project(Vector3 p);
      double ComputeApparentThickness(Vector3 p, double thickness);
   }

   public class OrthographicXYProjector : IProjector {
      public static readonly OrthographicXYProjector Instance = new OrthographicXYProjector();

      private readonly float scale;
      private readonly Vector2 cameraPosition;
      private readonly Vector2 canvasCenterOffset;
      private readonly float flipY;

      public OrthographicXYProjector(double scale = 1.0, Vector2 cameraPosition = default, Vector2 canvasCenterOffset = default, bool flipY = false) {
         this.scale = (float)scale;
         this.cameraPosition = cameraPosition;
         this.canvasCenterOffset = canvasCenterOffset;
         this.flipY = flipY ? -1 : 1;
      }

      public Vector2 Project(Vector3 p) {
         return new Vector2(
            (p.X - cameraPosition.X) * scale + canvasCenterOffset.X,
            (p.Y - cameraPosition.Y) * scale * flipY + canvasCenterOffset.Y);
      }

      public double ComputeApparentThickness(Vector3 p, double thickness) {
         return thickness;
      }
   }

   public class PerspectiveProjector : IProjector {
      private readonly Vector3 position;
      private readonly Vector3 lookat;
      private readonly Vector3 up;
      private readonly float width;
      private readonly float height;
      private readonly Matrix4x4 worldToCamera;
      private readonly Matrix4x4 cameraToView;
      private readonly Matrix4x4 transform;

      public PerspectiveProjector(Vector3 position, Vector3 lookat, Vector3 up, float width, float height) {
         this.position = position;
         this.lookat = lookat;
         this.up = up;
         this.width = width;
         this.height = height;
         this.worldToCamera =
            Matrix4x4.CreateTranslation(-position) *
            Matrix4x4.CreateLookAt(Vector3.Zero, lookat - position, up) *
            Matrix4x4.CreateScale(-1, 1, 1);
         this.cameraToView = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI * 2 / 4, (float)(width / height), 1f, 10000f);
         //          this.cameraToView = Matrix4x4.CreatePerspectiveOffCenter(0, (float)width, (float)height, 0, 1.0f, 1000.0f);
         transform = cameraToView * worldToCamera;
      }

      public Vector2 Project(Vector3 input) {
         var p = new Vector4(input, 1.0f);
         var cameraSpace = Vector4.Transform(p, worldToCamera);
         //         return new DoubleVector2(width * (1.0 + cameraSpace.X / cameraSpace.Z) / 2.0, height * (1.0 + cameraSpace.Y / cameraSpace.Z) / 2.0);
         var viewSpace = Vector4.Transform(cameraSpace, cameraToView);
         viewSpace /= viewSpace.W; // I'm doing something wrong.
                                   //         Console.WriteLine(p + " " + cameraSpace + " " + viewSpace + " " + (viewSpace / viewSpace.W));

         return new Vector2(
            width * (-viewSpace.X + 1.0f) / 2.0f,
            height * (-viewSpace.Y + 1.0f) / 2.0f
         );
         //         return new DoubleVector2(viewSpace.X, viewSpace.Y);
      }

      public double ComputeApparentThickness(Vector3 p, double thickness) => ComputeApparentThickness(ToNumerics3(p), (float)thickness);

      public double ComputeApparentThickness(Vector3 p, float thickness) {
         var ss1 = Project(p);
         var ss2 = Project(p + Vector3.Normalize(Vector3.Cross(p - position, lookat - position)) * thickness);

         return (float)(ss1 - ss2).Length();
         //         var cameraSpace = Vector4.Transform(ToNumerics4(p), worldToCamera);
         //         return thickness * (1000f + cameraSpace.Z) / 1000f;
      }

      private static Vector3 ToNumerics3(Vector3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
      private static Vector4 ToNumerics4(Vector3 v) => new Vector4((float)v.X, (float)v.Y, (float)v.Z, 1.0f);
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
         EffectiveRect = new Rectangle(-drawPadding.X, -drawPadding.Y, PaddedSize.Width, PaddedSize.Height);
         bitmap = new Bitmap(PaddedSize.Width, PaddedSize.Height);
      }

      public Size PaddedSize { get; }
      public Rectangle EffectiveRect { get; }

      public Font Font => SystemFonts.DefaultFont;

      private bool isRecursion = false;
      private Graphics g;

      internal Bitmap BitmapReadOnlyUnsafe => bitmap;
      public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

      public void BatchDraw(Action callback) {
         lock (synchronization) {
            if (!isRecursion) {
               using (g = Graphics.FromImage(bitmap)) {
                  g.CompositingQuality = CompositingQuality.HighQuality;
                  g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                  g.SmoothingMode = SmoothingMode.AntiAlias;
                  g.Transform = new Matrix(1, 0, 0, 1, drawPadding.X, drawPadding.Y);
                  g.TextRenderingHint = TextRenderingHint.AntiAlias;
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

      public void BatchDraw(Action<Graphics> callback) {
         BatchDraw(() => callback(g));
      }

      private Vector2 Project(Vector3 p) {
         var transformed = Vector3.Transform(new Vector3((float)p.X, (float)p.Y, (float)p.Z), Transform);
         return projector.Project(transformed);
      }

      private PointF ProjectPointF(Vector3 p) {
         var proj = Project(p);
         return new PointF((float)proj.X, (float)proj.Y);
      }

      private float ProjectThickness(Vector3 p, double thickness) => (float)projector.ComputeApparentThickness(p, thickness);

      public Vector3 ToNumerics(Vector3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

      private static readonly CopyOnAddDictionary<Color, SolidBrush> fillBrushCache = new CopyOnAddDictionary<Color, SolidBrush>();

      public void DrawPoint(Vector3 point, StrokeStyle strokeStyle) {
         BatchDraw(() => {
            var p = Project(ToNumerics(point));
            var x = (float)p.X;
            var y = (float)p.Y;
            var pointTransformed = Vector3.Transform(new Vector3((float)point.X, (float)point.Y, (float)point.Z), Transform);
            var radius = ProjectThickness(pointTransformed, strokeStyle.Thickness) / 2.0f;
            var rect = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
            var overlap = !(rect.Right <= EffectiveRect.X || rect.Bottom <= EffectiveRect.Y || rect.Left >= EffectiveRect.Right || rect.Top >= EffectiveRect.Bottom);
            //Console.WriteLine(overlap + " " + rect);
            if (!overlap) return;
            SolidBrush added = null;
            // Console.WriteLine("Get brush");
            var brush = fillBrushCache.GetOrAdd(
               strokeStyle.Color,
               add => added = new SolidBrush(strokeStyle.Color));
            // Console.WriteLine("Got brush");
            if (brush != added && added != null) {
               added.Dispose();
            }
            // Console.WriteLine("Fill");
            g.FillEllipse(brush, rect);
            // Console.WriteLine("OK");
         });
      }

      public void DrawLine(Vector3 point1, Vector3 point2, StrokeStyle strokeStyle) {
         DepthDrawLineStrip(new[] { point1, point2 }, strokeStyle);
      }

      public void DrawTriangle(Vector3 p1, Vector3 p2, Vector3 p3, StrokeStyle strokeStyle) {
         BatchDraw(() => {
            DrawLine(p1, p2, strokeStyle);
            DrawLine(p2, p3, strokeStyle);
            DrawLine(p3, p1, strokeStyle);
         });
      }

      public void FillTriangle(Vector3 p1, Vector3 p2, Vector3 p3, FillStyle fillStyle) {
         FillPolygon(new[] { p1, p2, p3 }, fillStyle);
      }

      public void FillPolygon(IReadOnlyList<Vector3> points, FillStyle fillStyle) {
         BatchDraw(() => {
            var ps = points.Select(p => Project(ToNumerics(p))).ToList();
            using (var brush = new SolidBrush(fillStyle.Color)) {
               g.FillPolygon(brush, ps.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray());
            }
         });
      }

      public void DrawPolygon(IReadOnlyList<Vector3> points, StrokeStyle strokeStyle) {
         DepthDrawLineStrip(points.Concat(new[] { points[0] }).ToList(), strokeStyle);
      }

      private void DepthDrawLineStrip(IReadOnlyList<Vector3> points, StrokeStyle strokeStyle) {
         BatchDraw(() => {
            if (strokeStyle.DisableStrokePerspective || true) {
               if (points.Count <= 1) return;

               using (var pen = new Pen(strokeStyle.Color, (float)strokeStyle.Thickness)) {
                  var ps = points.Select(p => ProjectPointF(ToNumerics(p))).ToArray();
                  g.DrawLines(pen, ps);
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
               var p1p2 = p2 - p1;
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
                  segments.Add(ProjectPointF(ToNumerics(pa)));
                  segments.Add(ProjectPointF(ToNumerics(pb)));
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

      public void DrawText(string text, Vector3 point) {
         BatchDraw(() => {
            var p = Project(ToNumerics(point));
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
