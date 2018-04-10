using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Canvas3D;
using OpenMOBA;
using OpenMOBA.DataStructures;
using OpenMOBA.Debugging;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;

namespace FogOfWarTests {
   public class Program {
      private static readonly float renderScale = 1.0f;
      private static readonly Size bounds = new Size(1280, 720);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(0, 0), CreateCanvasProjector());
      private static int frameCounter = 0;

      public static void Main(string[] args) {
         var sectorGraphDescriptionStore = new SectorGraphDescriptionStore();
         var snapshotCompiler = new TerrainSnapshotCompiler(sectorGraphDescriptionStore);
         var terrainService = new TerrainService(sectorGraphDescriptionStore, snapshotCompiler);

         // Add test sectors
         var leftSnd = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.HashCircle2);
//         leftSnd.EnableDebugHighlight = true;
         leftSnd.WorldTransform = Matrix4x4.CreateScale(1000.0f / 60000.0f) * Matrix4x4.CreateTranslation(-1000, 0, 0);
         terrainService.AddSectorNodeDescription(leftSnd);

         var centerSnd = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.Blank2D);
         centerSnd.EnableDebugHighlight = true;
         centerSnd.WorldTransform = Matrix4x4.CreateScale(1000.0f / 60000.0f);
         terrainService.AddSectorNodeDescription(centerSnd);

         /*+		[0]	{(-30000, -18000)}	OpenMOBA.Geometry.IntVector2
+		[1]	{(-30000, -6000)}	OpenMOBA.Geometry.IntVector2
+		[2]	{(-6000, -6000)}	OpenMOBA.Geometry.IntVector2
+		[3]	{(-6000, -18000)}	OpenMOBA.Geometry.IntVector2
+		[4]	{(-30000, -18000)}	OpenMOBA.Geometry.IntVector2
   */
         var rightSnd = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.HashCircle2);
         rightSnd.WorldTransform = Matrix4x4.CreateScale(1000.0f / 60000.0f) * Matrix4x4.CreateTranslation(1000, 0, 0);
         terrainService.AddSectorNodeDescription(rightSnd);

         // edges between test sectors

         var rightTopSegment = new IntLineSegment2(new IntVector2(30000, 6000), new IntVector2(30000, 18000));
         var leftTopSegment = new IntLineSegment2(new IntVector2(-30000, 6000), new IntVector2(-30000, 18000));
         var rightBottomSegment = new IntLineSegment2(new IntVector2(30000, -18000), new IntVector2(30000, -6000));
         var leftBottomSegment = new IntLineSegment2(new IntVector2(-30000, -18000), new IntVector2(-30000, -6000));

         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            leftSnd, centerSnd,
            rightTopSegment,
            leftTopSegment));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            centerSnd, leftSnd,
            leftTopSegment,
            rightTopSegment));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            leftSnd, centerSnd,
            rightBottomSegment,
            leftBottomSegment));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            centerSnd, leftSnd,
            leftBottomSegment,
            rightBottomSegment));

         //
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            centerSnd, rightSnd,
            rightTopSegment,
            leftTopSegment));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            rightSnd, centerSnd,
            leftTopSegment,
            rightTopSegment));

         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            centerSnd, rightSnd,
            rightBottomSegment,
            leftBottomSegment));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            rightSnd, centerSnd,
            leftBottomSegment,
            rightBottomSegment));

         // add some obstacles
//         terrainService.AddTemporaryHoleDescription(terrainService.CreateHoleDescription(
//            HoleStaticMetadata.CreateRectangleHoleMetadata(-500, 250, 60, 60, 0)));
//         terrainService.AddTemporaryHoleDescription(terrainService.CreateHoleDescription(
//            HoleStaticMetadata.CreateRectangleHoleMetadata(0, 130, 60, 60, 0)));
         for (var i = 0; i < 100; i++) {
            break;
            terrainService.AddTemporaryHoleDescription(terrainService.CreateHoleDescription(
               HoleStaticMetadata.CreateRectangleHoleMetadata(
                  random.Next(-1500, 1500), 
                  random.Next(-500, 500), 
                  random.Next(10, 50), 
                  random.Next(10, 50), 
                  random.NextDouble() * Math.PI * 2)));
         }

         var terrainSnapshot = terrainService.CompileSnapshot();
         var overlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(30);

         for (var i = 0; i < 360; i += 10) {
            var canvas = host.CreateAndAddCanvas(i);
            canvas.BatchDraw(() => {

               foreach (var terrainNode in overlayNetwork.TerrainNodes) {
                  canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;
                  canvas.DrawPolyNode(terrainNode.LandPolyNode, StrokeStyle.BlackHairLineSolid, StrokeStyle.DarkRedHairLineSolid);
               }

               foreach (var terrainNode in overlayNetwork.TerrainNodes) {
                  canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;
                  foreach (var outboundEdgeGroup in terrainNode.OutboundEdgeGroups) {
                     foreach (var outboundEdge in outboundEdgeGroup.Value) {
                        Console.WriteLine(outboundEdge.EdgeJob.SourceSegment);
                        canvas.DrawLine(outboundEdge.EdgeJob.SourceSegment, StrokeStyle.CyanThick3Solid);
                     }
                  }
               }

               foreach (var terrainNode in overlayNetwork.TerrainNodes) {
                  canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;

                  foreach (var hole in terrainNode.LocalGeometryView.Job.DynamicHoles) {
                     var (holeIncludedContours, holeExcludedContours) = hole.Value;
                     canvas.DrawPolygonContours(holeIncludedContours, StrokeStyle.RedHairLineSolid);
                     canvas.DrawPolygonContours(holeExcludedContours, StrokeStyle.OrangeHairLineSolid);
                  }
               }

               int asdfa = -1;
               foreach (var terrainNode in overlayNetwork.TerrainNodes) {
                  asdfa++;
                  canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;

                  if (terrainNode.SectorNodeDescription.EnableDebugHighlight) {
                     var visibilityPolygonOrigin = IntVector2.FromRadiusAngle(50 * 60, i * Math.PI / 180) + new IntVector2(0, 0);
                     canvas.DrawCrossSectorVisibilityPolygon(terrainNode, visibilityPolygonOrigin);
                  }
               }
            });
         }
      }

      private static PerspectiveProjector CreateCanvasProjector() {
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
   }
}
 