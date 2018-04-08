using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Canvas3D;
using OpenMOBA;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
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
         leftSnd.EnableDebugHighlight = true;
         leftSnd.WorldTransform = Matrix4x4.CreateTranslation(-1500, -500, 0);
         terrainService.AddSectorNodeDescription(leftSnd);

         var centerSnd = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.Test2D);
         centerSnd.WorldTransform = Matrix4x4.CreateTranslation(-500, -500, 0);
         terrainService.AddSectorNodeDescription(centerSnd);

         // edges between test sectors
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            leftSnd, centerSnd,
            new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800)),
            new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800))));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            centerSnd, leftSnd,
            new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)),
            new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800))));

         var terrainSnapshot = terrainService.CompileSnapshot();
         var overlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(0);
         var canvas = host.CreateAndAddCanvas(0);
         foreach (var terrainNode in overlayNetwork.TerrainNodes) {
            canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;
            canvas.DrawPolyNode(terrainNode.LandPolyNode);

            Console.WriteLine(terrainNode.OutboundEdgeGroups.Count + " " + terrainNode.InboundEdgeGroups.Count);
            foreach (var outboundEdgeGroup in terrainNode.OutboundEdgeGroups) {
               foreach (var outboundEdge in outboundEdgeGroup.Value) {
                  canvas.DrawLine(outboundEdge.EdgeJob.SourceSegment, StrokeStyle.RedHairLineSolid);
               }
            }

            if (!terrainNode.SectorNodeDescription.EnableDebugHighlight) continue;

            var visibilityPolygonOrigin = new IntVector2(700, 700);
            canvas.DrawPoint(visibilityPolygonOrigin, StrokeStyle.RedThick25Solid);
            var visibilityPolygon = VisibilityPolygon.Create(visibilityPolygonOrigin.ToDoubleVector2(), terrainNode.LandPolyNode.FindContourAndChildHoleBarriers());
            canvas.DrawVisibilityPolygon(visibilityPolygon);
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