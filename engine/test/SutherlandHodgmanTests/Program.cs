using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using OpenMOBA;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;

namespace SutherlandHodgmanTests {
   public class Program {
      private static readonly float renderScale = 1.0f;
      private static readonly Size bounds = new Size(1280, 720);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(50, 50), new OrthographicXYProjector());
      private static int frameCounter = 0;

      public static void Main(string[] args) {
//         var canvas = host.CreateAndAddCanvas(0);
//         var smp = SectorMetadataPresets.Blank2D;
//         var punchResult = PolygonOperations.Punch()
//                                            .Include(smp.LocalIncludedContours)
////                          .Exclude(smp.LocalExcludedContours)
//                                            .Exclude(Polygon2.CreateRect(-10000, -10000, 20000, 20000))
//                                            .Exclude(new []{(Polygon2.CreateRect(-8000, -8000, 16000, 16000), true)})
//                                            .Execute();
//
//         canvas.Transform = Matrix4x4.CreateScale(500 / 60000.0f) * Matrix4x4.CreateTranslation(500, 300, 0);
//         canvas.DrawPolyNode(punchResult);
//         return;

         //var subjectPolygon = Polygon2.CreateCircle(0, 0, 100, 16);
         var n = 128;
         var random = new Random(0);
         var subjectPolygon = new Polygon2(
            Util.Generate(
               n,
               i => DoubleVector2.FromRadiusAngle(random.Next(10, 150), -i * Math.PI * 2 / n).LossyToIntVector2())
               .ToList());

         var clipPolygon = Polygon2.CreateRect(-80, -80, 160, 160);
         
         RenderSomething(0, subjectPolygon, clipPolygon);

         var offsetSubjectPolygon = new Polygon2(subjectPolygon.Points.Map(p => new IntVector2(0, 240) + p).ToList());
         RenderSomething(1, offsetSubjectPolygon, clipPolygon);
      }

      private static void RenderSomething(int canvasIndex, Polygon2 subject, Polygon2 clip) {
         var canvas = host.CreateAndAddCanvas(canvasIndex);
         canvas.Transform = Matrix4x4.CreateTranslation(150, 150, 0) * Matrix4x4.CreateScale(2);
         canvas.DrawLineStrip(subject.Points.Concat(new[] { subject.Points[0] }).ToArray(), StrokeStyle.CyanThick3Solid);
         canvas.DrawLineStrip(clip.Points.Concat(new[] { clip.Points[0] }).ToArray(), StrokeStyle.RedThick3Solid);

         canvas.Transform = Matrix4x4.CreateTranslation(450, 150, 0) * Matrix4x4.CreateScale(2);
         canvas.DrawLineStrip(subject.Points.Concat(new[] { subject.Points[0] }).ToArray(), StrokeStyle.CyanHairLineSolid);
         canvas.DrawLineStrip(clip.Points.Concat(new[] { clip.Points[0] }).ToArray(), StrokeStyle.RedHairLineSolid);

         if (PolygonOperations.TryConvexClip(subject, clip, out var result)) {
            canvas.DrawLineStrip(result.Points.Concat(new[] { result.Points[0] }).ToArray(), StrokeStyle.LimeThick5Solid);
         }
      }
   }
}