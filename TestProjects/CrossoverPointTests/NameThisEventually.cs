using OpenMOBA;
using OpenMOBA.DataStructures;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;
using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Xunit;

namespace CrossoverPointTests {
   public class NameThisEventually {
      public const bool kEnableDebugRender = false;

      public static void Main() {
         new NameThisEventually().A();
         new NameThisEventually().B();
         new NameThisEventually().C();
         new NameThisEventually().D();
         new NameThisEventually().E();
         new NameThisEventually().F();
      }

      private LocalGeometryView BuildLgv(TerrainStaticMetadata mapStaticMetadata, double holeDilationRadius, params IHoleStaticMetadata[] holeMetadatas) {
         var store = new SectorGraphDescriptionStore();
         var terrainService = new TerrainService(store, new TerrainSnapshotCompiler(store));

         var sector = terrainService.CreateSectorNodeDescription(mapStaticMetadata);
         sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(-500, -500, 0));
         terrainService.AddSectorNodeDescription(sector);

         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector, sector, left2, left2));

         foreach (var holeMetadata in holeMetadatas) {
            var terrainHole = terrainService.CreateHoleDescription(holeMetadata);
            terrainService.AddTemporaryHoleDescription(terrainHole);
         }

         var terrainOverlayNetwork = terrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);
         return terrainOverlayNetwork.TerrainNodes.First().LocalGeometryView;
      }

      private IHoleStaticMetadata BuildRectangleHole(int x, int y, int width, int height, long rotationBits = -1) {
         var rotation = rotationBits == -1 ? 0.0 : BitConverter.Int64BitsToDouble(rotationBits);
         var contour = Polygon2.CreateRect(-width / 2, -height / 2, width, height).Points;
         var transform = Matrix3x2.CreateRotation((float)rotation);
         contour = contour.Map(p => Vector2.Transform(p.ToDoubleVector2().ToDotNetVector(), transform).ToOpenMobaVector().LossyToIntVector2())
                          .Map(p => p + new IntVector2(x, y))
                          .ToList();

         var bounds = IntRect2.BoundingPoints(contour.ToArray()).ToDotNetRectangle();

         return new PrismHoleStaticMetadata {
            LocalBoundary = bounds,
            LocalIncludedContours = new[] { new Polygon2(contour, false) }
         };
      }

      [Fact]
      public void A() {
         var localGeometryView = BuildLgv(SectorMetadataPresets.Blank2D, 15, BuildRectangleHole(-486, 213, 119, 164));
         var seg = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         DebugRenderLocalGeometryView(localGeometryView, seg);
         var results = PortalSectorEdgeDescription.X(seg, localGeometryView).ToArray();
         Assert.Equal(0, results.Length);
      }

      [Fact]
      public void B() {
         var localGeometryView = BuildLgv(SectorMetadataPresets.Blank2D, 14, BuildRectangleHole(-486, 213, 119, 164));
         var seg = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         DebugRenderLocalGeometryView(localGeometryView, seg);
         var results = PortalSectorEdgeDescription.X(seg, localGeometryView).ToArray();
         Assert.Equal(0, results.Length);
      }

      [Fact]
      public void C() {
         var localGeometryView = BuildLgv(SectorMetadataPresets.Blank2D, 14, BuildRectangleHole(-664, 133, 174, 188));
         var seg = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         DebugRenderLocalGeometryView(localGeometryView, seg);
         var results = PortalSectorEdgeDescription.X(seg, localGeometryView).ToArray();
         Assert.Equal(2, results.Length);
      }

      [Fact]
      public void D() {
         var localGeometryView = BuildLgv(
            SectorMetadataPresets.Blank2D,
            14,
            BuildRectangleHole(-517, 107, 5, 5, 4607552631852924299),
            BuildRectangleHole(-506, 100, 6, 7, 4613867344244209246),
            BuildRectangleHole(-505, 136, 6, 7, 4615794974257582119)
         );
         var seg = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         var results = PortalSectorEdgeDescription.X(seg, localGeometryView).ToArray();
         DebugRenderLocalGeometryView(localGeometryView, seg);
         Assert.Equal(2, results.Length);
      }

      [Fact]
      public void E() {
         var localGeometryView = BuildLgv(
            SectorMetadataPresets.HashCircle2,
            0
         );
         var seg = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         var results = PortalSectorEdgeDescription.X(seg, localGeometryView).ToArray();
         DebugRenderLocalGeometryView(localGeometryView, seg);
         Assert.Equal(results.Map(x => x.Item2), new[] { 0.0, 1.0 });
      }

      [Fact]
      public void F() {
         var localGeometryView = BuildLgv(
            SectorMetadataPresets.HashCircle2,
            0
         );
         var seg = new IntLineSegment2(new IntVector2(-2, 620), new IntVector2(-2, 780));
         var results = PortalSectorEdgeDescription.X(seg, localGeometryView).ToArray();
         DebugRenderLocalGeometryView(localGeometryView, seg);
         Assert.Equal(results.Map(x => x.Item2), new[] { 0.0, 1.0 });
      }

      private void DebugRenderLocalGeometryView(LocalGeometryView localGeometryView, params IntLineSegment2[] segs) {
         if (!kEnableDebugRender) return;

         Size bounds = new Size(1280, 720);
         var renderScale = 1.0f;

         PerspectiveProjector CreateCanvasProjector() {
            var rotation = 95 * Math.PI / 180.0;
            var displaySize = new Size((int)(bounds.Width * renderScale), (int)(bounds.Height * renderScale));
            var center = new DoubleVector3(0, 0, 0);
            var projector = new PerspectiveProjector(
               center + DoubleVector3.FromRadiusAngleAroundXAxis(1000, rotation),
               center,
               DoubleVector3.FromRadiusAngleAroundXAxis(1, rotation - Math.PI / 2),
               displaySize.Width,
               displaySize.Height);
            return projector;
         }

         DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(0, 0), CreateCanvasProjector());
         var canvas = host.CreateAndAddCanvas(0);
         canvas.DrawPolyNode(localGeometryView.PunchedLand);
         foreach (var hole in localGeometryView.Job.DynamicHoles) {
            canvas.DrawPolygonContours(hole.Value.holeExcludedContours, StrokeStyle.OrangeHairLineSolid);
            canvas.DrawPolygonContours(hole.Value.holeIncludedContours, StrokeStyle.RedHairLineSolid);
         }
         foreach (var seg in segs) {
            canvas.DrawLine(seg, StrokeStyle.CyanHairLineSolid);
         }
      }
   }
}
