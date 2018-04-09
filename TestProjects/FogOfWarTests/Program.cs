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
                     Y(canvas, terrainNode, visibilityPolygonOrigin);
                  }
               }
            });
         }
      }


      private static void Y(IDebugCanvas canvas, TerrainOverlayNetworkNode terrainNode, IntVector2 visibilityPolygonOrigin) {
         canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;

         canvas.DrawPoint(visibilityPolygonOrigin, StrokeStyle.RedThick25Solid);
         var visibilityPolygon = VisibilityPolygon.Create(visibilityPolygonOrigin.ToDoubleVector2(), terrainNode.LandPolyNode.FindContourAndChildHoleBarriers());
         canvas.DrawVisibilityPolygon(visibilityPolygon, fillStyle: new FillStyle(Color.FromArgb(130, 255, 255, 0)));

         var visibleCrossoverSegmentsByNeighbor = FindVisibleCrossoverSegmentsByNeighbor(canvas, terrainNode, visibilityPolygon, visibilityPolygonOrigin);

         var visibilityPolygonOriginWorld = Vector3.Transform(new Vector3(visibilityPolygonOrigin.ToDotNetVector(), 0), terrainNode.SectorNodeDescription.WorldTransform);
         foreach (var (neighbor, inboundCrossoverSegments) in visibleCrossoverSegmentsByNeighbor) {
            var neighborPolygonOrigin = Vector3.Transform(visibilityPolygonOriginWorld, neighbor.SectorNodeDescription.WorldTransformInv);
            Z(canvas, new IntVector2((int)neighborPolygonOrigin.X, (int)neighborPolygonOrigin.Y), neighbor, inboundCrossoverSegments, new HashSet<TerrainOverlayNetworkNode> { terrainNode });
         }
      }

      private static void Z(IDebugCanvas canvas, IntVector2 visibilityPolygonOrigin, TerrainOverlayNetworkNode terrainNode, IReadOnlyCollection<IntLineSegment2> inboundCrossoverSegments, HashSet<TerrainOverlayNetworkNode> visited) {
         canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;

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

         foreach (var inboundCrossoverSegment in inboundCrossoverSegments) {
            visibilityPolygon.ClearBefore(inboundCrossoverSegment);
         }

         Console.WriteLine("====");

         foreach (var seg in terrainNode.LandPolyNode.FindContourAndChildHoleBarriers()) {
            if (GeometryOperations.Clockness(visibilityPolygon.Origin, seg.First.ToDoubleVector2(), seg.Second.ToDoubleVector2()) == Clockness.CounterClockwise) {
               continue;
            }
            visibilityPolygon.Insert(seg);
            Console.WriteLine(seg);
         }
         Console.WriteLine("====");

         canvas.DrawVisibilityPolygon(visibilityPolygon, fillStyle: new FillStyle(Color.FromArgb(130, 255, 255, 0)));
         
         var visibleCrossoverSegmentsByNeighbor = FindVisibleCrossoverSegmentsByNeighbor(canvas, terrainNode, visibilityPolygon, visibilityPolygonOrigin, visited);

         var visibilityPolygonOriginWorld = Vector3.Transform(new Vector3(visibilityPolygonOrigin.ToDotNetVector(), 0), terrainNode.SectorNodeDescription.WorldTransform);
         foreach (var (neighbor, nextInboundCrossoverSegments) in visibleCrossoverSegmentsByNeighbor) {
            var neighborPolygonOrigin = Vector3.Transform(visibilityPolygonOriginWorld, neighbor.SectorNodeDescription.WorldTransformInv);
            //visibilityPolygonOrigin
            Z(canvas, new IntVector2((int)neighborPolygonOrigin.X, (int)neighborPolygonOrigin.Y), neighbor, nextInboundCrossoverSegments,
               visited.Concat(new[] { terrainNode }).ToHashSet());
         }
      }

      private static MultiValueDictionary<TerrainOverlayNetworkNode, IntLineSegment2> FindVisibleCrossoverSegmentsByNeighbor(
         IDebugCanvas canvas, 
         TerrainOverlayNetworkNode terrainNode, 
         VisibilityPolygon visibilityPolygon, 
         IntVector2 visibilityPolygonOrigin, 
         HashSet<TerrainOverlayNetworkNode> visited = null) {

         var visibleCrossoverSegmentsByNeighbor = MultiValueDictionary<TerrainOverlayNetworkNode, IntLineSegment2>.Create(() => new HashSet<IntLineSegment2>());
         foreach (var outboundEdgeGroup in terrainNode.OutboundEdgeGroups) {
            var otherTerrainNode = outboundEdgeGroup.Key;
            if (visited?.Contains(otherTerrainNode) ?? false) continue;

            foreach (var outboundEdge in outboundEdgeGroup.Value) {
               var ranges = visibilityPolygon.Get();

               (IntLineSegment2, bool) FlipMaybeSorta(IntLineSegment2 x) =>
                  GeometryOperations.Clockness(visibilityPolygonOrigin, x.First, x.Second) == Clockness.CounterClockwise
                     ? (new IntLineSegment2(x.Second, x.First), true)
                     : (x, false);

               var (localCrossoverSegment, lcsFlipped) = FlipMaybeSorta(outboundEdge.EdgeJob.EdgeDescription.SourceSegment);
               var (remoteCrossoverSegment, rcsFlipped) = FlipMaybeSorta(outboundEdge.EdgeJob.EdgeDescription.DestinationSegment);

               // todo: clamp visibleStartT, visibleEndT to account for agent radius eroding crossover segmetmentnt
               var rangeIndexIntervals = visibilityPolygon.RangeStab(localCrossoverSegment);
               foreach (var (startIndexInclusive, endIndexInclusive) in rangeIndexIntervals) {
                  for (var i = startIndexInclusive; i <= endIndexInclusive; i++) {
                     if (ranges[i].Id == VisibilityPolygon.RANGE_ID_INFINITELY_FAR || ranges[i].Id == VisibilityPolygon.RANGE_ID_INFINITESIMALLY_NEAR) continue;

                     var seg = ranges[i].Segment;

                     var rstart = DoubleVector2.FromRadiusAngle(100, ranges[i].ThetaStart);
                     var rend = DoubleVector2.FromRadiusAngle(100, ranges[i].ThetaEnd);

                     //                        DoubleVector2 visibleStart, visibleEnd;
                     double visibleStartT, visibleEndT;
                     if (!GeometryOperations.TryFindNonoverlappingLineLineIntersectionT(localCrossoverSegment.First.ToDoubleVector2(), localCrossoverSegment.Second.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2() + rstart, out visibleStartT) ||
                         !GeometryOperations.TryFindNonoverlappingLineLineIntersectionT(localCrossoverSegment.First.ToDoubleVector2(), localCrossoverSegment.Second.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2() + rend, out visibleEndT)) {
                        // wtf?
                        Console.WriteLine("???");
                        continue;
                     }
                     if (visibleStartT < 0 || visibleEndT > 1) continue;

                     if (visibilityPolygon.SegmentComparer.Compare(localCrossoverSegment, seg) < 0) {
                        var localVisibleStart = localCrossoverSegment.PointAt(visibleStartT);
                        var localVisibleEnd = localCrossoverSegment.PointAt(visibleEndT);
                        canvas.DrawLine(new DoubleLineSegment2(localVisibleStart, localVisibleEnd), StrokeStyle.LimeThick5Solid);

//                        canvas.FillTriangle(
//                           visibilityPolygonOrigin.ToDoubleVector2(),
//                           localVisibleStart,
//                           localVisibleEnd,
//                           new FillStyle(Color.FromArgb(120, 0, 255, 255)));
                        
                        var remoteVisibleStart = remoteCrossoverSegment.PointAt(lcsFlipped == rcsFlipped ? visibleStartT : 1.0 - visibleStartT);
                        var remoteVisibleEnd = remoteCrossoverSegment.PointAt(lcsFlipped == rcsFlipped ? visibleEndT : 1.0 - visibleEndT);
                        visibleCrossoverSegmentsByNeighbor.Add(otherTerrainNode, new IntLineSegment2(remoteVisibleStart.LossyToIntVector2(), remoteVisibleEnd.LossyToIntVector2()));
                     } else {
                        canvas.DrawLine(seg, StrokeStyle.RedThick5Solid);
                     }
                  }
               }
            }
         }
         return visibleCrossoverSegmentsByNeighbor;
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
 