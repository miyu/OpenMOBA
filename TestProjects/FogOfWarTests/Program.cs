using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Canvas3D;
using OpenMOBA;
using OpenMOBA.DataStructures;
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
         var leftSnd = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.Test2D);
         leftSnd.EnableDebugHighlight = true;
         leftSnd.WorldTransform = Matrix4x4.CreateTranslation(-1500, -500, 0);
         terrainService.AddSectorNodeDescription(leftSnd);

         var centerSnd = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.Blank2D);
         centerSnd.WorldTransform = Matrix4x4.CreateTranslation(-500, -500, 0);
         terrainService.AddSectorNodeDescription(centerSnd);

         var rightSnd = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.HashCircle2);
         rightSnd.WorldTransform = Matrix4x4.CreateTranslation(500, -500, 0);
         terrainService.AddSectorNodeDescription(rightSnd);

         // edges between test sectors
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            leftSnd, centerSnd,
            new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800)),
            new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800))));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            centerSnd, leftSnd,
            new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)),
            new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800))));

         // 
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
            centerSnd, rightSnd,
            new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800)),
            new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800))));
//         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(
//            rightSnd, centerSnd,
//            new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)),
//            new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800))));

         var terrainSnapshot = terrainService.CompileSnapshot();
         var overlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(15);
         var canvas = host.CreateAndAddCanvas(0);
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

         int asdfa = -1;
         foreach (var terrainNode in overlayNetwork.TerrainNodes) {
            asdfa++;
            if (asdfa != 0 && asdfa != 2) continue;
//            if (asdfa != 2) continue;

            canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;
            canvas.DrawRectangle(new IntRect2(0, 0, 30, 30), 0, StrokeStyle.RedHairLineSolid);

            if (terrainNode.SectorNodeDescription.EnableDebugHighlight) {
               var visibilityPolygonOrigin = new IntVector2(950, 700);
               canvas.DrawPoint(visibilityPolygonOrigin, StrokeStyle.RedThick25Solid);
               var visibilityPolygon = VisibilityPolygon.Create(visibilityPolygonOrigin.ToDoubleVector2(), terrainNode.LandPolyNode.FindContourAndChildHoleBarriers());

//               foreach (var outboundEdgeGroup in terrainNode.OutboundEdgeGroups) {
//                  foreach (var outboundEdge in outboundEdgeGroup.Value) {
//                     visibilityPolygon.ClearBeyond(outboundEdge.EdgeJob.SourceSegment.LossyToIntLineSegment2());
//                  }
//               }
               canvas.DrawVisibilityPolygon(visibilityPolygon, fillStyle: new FillStyle(Color.FromArgb(120, 255, 0, 0)));
            } else {
               var visibilityPolygonOrigin = new IntVector2(950 - 1000, 700);
               canvas.DrawPoint(visibilityPolygonOrigin, StrokeStyle.RedThick25Solid);
               var visibilityPolygon = new VisibilityPolygon(
                  visibilityPolygonOrigin.ToDoubleVector2(),
                  new[] {
                     new VisibilityPolygon.IntervalRange {
                        Id = VisibilityPolygon.RANGE_ID_INFINITESIMALLY_NEAR,
                        ThetaStart = 0,
                        ThetaEnd = VisibilityPolygon.TwoPi
                     }, 
                  });

               foreach (var inboundEdgeGroup in terrainNode.InboundEdgeGroups) {
                  foreach (var inboundEdge in inboundEdgeGroup.Value) {
                     visibilityPolygon.ClearBefore(inboundEdge.EdgeJob.DestinationSegment.LossyToIntLineSegment2());
                  }
               }

               foreach (var seg in terrainNode.LandPolyNode.FindContourAndChildHoleBarriers()) {
                  if (GeometryOperations.Clockness(visibilityPolygon.Origin, seg.First.ToDoubleVector2(), seg.Second.ToDoubleVector2()) == Clockness.CounterClockwise) {
                     continue;
                  }
                  visibilityPolygon.Insert(seg);
               }

               canvas.DrawVisibilityPolygon(visibilityPolygon, fillStyle: new FillStyle(Color.FromArgb(120, 0, 0, 255)));
            }
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