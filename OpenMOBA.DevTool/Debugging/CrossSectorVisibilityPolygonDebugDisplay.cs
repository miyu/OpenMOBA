using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class CrossSectorVisibilityPolygonDebugDisplay {
      private static readonly FillStyle kDefaultFillStyle = new FillStyle(Color.FromArgb(130, 255, 255, 0));

      public static void DrawCrossSectorVisibilityPolygon(
         this IDebugCanvas canvas,
         TerrainOverlayNetworkNode terrainNode,
         IntVector2 visibilityPolygonOrigin,
         FillStyle fillStyle = null
      ) {
         fillStyle = fillStyle ?? kDefaultFillStyle;

         canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;

//         canvas.DrawPoint(visibilityPolygonOrigin, StrokeStyle.RedThick25Solid);
         var visibilityPolygon = VisibilityPolygon.Create(visibilityPolygonOrigin.ToDoubleVector2(), terrainNode.LandPolyNode.FindContourAndChildHoleBarriers());
         var visibleCrossoverSegmentsByNeighbor = FindVisibleCrossoverSegmentsByNeighborAndClearLocalAt(canvas, terrainNode, visibilityPolygon, visibilityPolygonOrigin);
         canvas.DrawVisibilityPolygon(visibilityPolygon, fillStyle: fillStyle ?? kDefaultFillStyle, angleBoundaryStrokeStyle: StrokeStyle.None, visibleWallStrokeStyle: StrokeStyle.None);

         var visibilityPolygonOriginWorld = Vector3.Transform(new Vector3(visibilityPolygonOrigin.ToDotNetVector(), 0), terrainNode.SectorNodeDescription.WorldTransform);
         foreach (var (neighbor, inboundCrossoverSegments) in visibleCrossoverSegmentsByNeighbor) {
            var neighborPolygonOrigin = Vector3.Transform(visibilityPolygonOriginWorld, neighbor.SectorNodeDescription.WorldTransformInv);
            Z(canvas, new IntVector2((int)neighborPolygonOrigin.X, (int)neighborPolygonOrigin.Y), neighbor, inboundCrossoverSegments, new HashSet<TerrainOverlayNetworkNode> { terrainNode }, fillStyle);
         }
      }

      private static void Z(this IDebugCanvas canvas, IntVector2 visibilityPolygonOrigin, TerrainOverlayNetworkNode terrainNode, IReadOnlyCollection<IntLineSegment2> inboundCrossoverSegments, HashSet<TerrainOverlayNetworkNode> visited, FillStyle fillStyle) {
         canvas.Transform = terrainNode.SectorNodeDescription.WorldTransform;

//         canvas.DrawPoint(visibilityPolygonOrigin, StrokeStyle.RedThick25Solid);
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

//         Console.WriteLine("====");

         foreach (var seg in terrainNode.LandPolyNode.FindContourAndChildHoleBarriers()) {
            if (GeometryOperations.Clockness(visibilityPolygon.Origin, seg.First.ToDoubleVector2(), seg.Second.ToDoubleVector2()) == Clockness.CounterClockwise) {
               continue;
            }
            visibilityPolygon.Insert(seg);
//            Console.WriteLine(seg);
         }
//         Console.WriteLine("====");

         var visibleCrossoverSegmentsByNeighbor = FindVisibleCrossoverSegmentsByNeighborAndClearLocalAt(canvas, terrainNode, visibilityPolygon, visibilityPolygonOrigin, visited);
         canvas.DrawVisibilityPolygon(visibilityPolygon, fillStyle: fillStyle ?? kDefaultFillStyle, angleBoundaryStrokeStyle: StrokeStyle.None, visibleWallStrokeStyle: StrokeStyle.None);


         var visibilityPolygonOriginWorld = Vector3.Transform(new Vector3(visibilityPolygonOrigin.ToDotNetVector(), 0), terrainNode.SectorNodeDescription.WorldTransform);
         foreach (var (neighbor, nextInboundCrossoverSegments) in visibleCrossoverSegmentsByNeighbor) {
            var neighborPolygonOrigin = Vector3.Transform(visibilityPolygonOriginWorld, neighbor.SectorNodeDescription.WorldTransformInv);
            //visibilityPolygonOrigin
            Z(canvas, new IntVector2((int)neighborPolygonOrigin.X, (int)neighborPolygonOrigin.Y), neighbor, nextInboundCrossoverSegments,
               visited.Concat(new[] { terrainNode }).ToHashSet(), fillStyle);
         }
      }

      private static MultiValueDictionary<TerrainOverlayNetworkNode, IntLineSegment2> FindVisibleCrossoverSegmentsByNeighborAndClearLocalAt(
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
               var locallyClearedSegments = new List<IntLineSegment2>();
               foreach (var (startIndexInclusive, endIndexInclusive) in rangeIndexIntervals) {
                  for (var i = startIndexInclusive; i <= endIndexInclusive; i++) {
                     if (ranges[i].Id == VisibilityPolygon.RANGE_ID_INFINITELY_FAR || ranges[i].Id == VisibilityPolygon.RANGE_ID_INFINITESIMALLY_NEAR) continue;

                     var seg = ranges[i].Segment;

                     var rstart = DoubleVector2.FromRadiusAngle(100, ranges[i].ThetaStart) * 100;
                     var rend = DoubleVector2.FromRadiusAngle(100, ranges[i].ThetaEnd) * 100;

                     double visibleStartT, visibleEndT;
                     if (!GeometryOperations.TryFindNonoverlappingLineLineIntersectionT(localCrossoverSegment.First.ToDoubleVector2(), localCrossoverSegment.Second.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2() + rstart, out visibleStartT) ||
                         !GeometryOperations.TryFindNonoverlappingLineLineIntersectionT(localCrossoverSegment.First.ToDoubleVector2(), localCrossoverSegment.Second.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2(), visibilityPolygonOrigin.ToDoubleVector2() + rend, out visibleEndT)) {
                        // wtf?
                        Console.WriteLine("???");
                        continue;
                     }

                     // Todo: I don't actually understand why visibleEndT > 1 is a thing?
                     // t values are for parameterization of crossover line segment, so must be within [0, 1]
                     if ((visibleStartT < 0 && visibleEndT < 0) || (visibleStartT > 1 && visibleEndT > 1)) continue;
                     visibleStartT = Math.Min(1.0, Math.Max(0.0, visibleStartT));
                     visibleEndT = Math.Min(1.0, Math.Max(0.0, visibleEndT));

                     if (visibilityPolygon.SegmentComparer.Compare(localCrossoverSegment, seg) < 0) {
                        var localVisibleStart = localCrossoverSegment.PointAt(visibleStartT).LossyToIntVector2();
                        var localVisibleEnd = localCrossoverSegment.PointAt(visibleEndT).LossyToIntVector2();

                        var remoteVisibleStart = remoteCrossoverSegment.PointAt(lcsFlipped == rcsFlipped ? visibleStartT : 1.0 - visibleStartT).LossyToIntVector2();
                        var remoteVisibleEnd = remoteCrossoverSegment.PointAt(lcsFlipped == rcsFlipped ? visibleEndT : 1.0 - visibleEndT).LossyToIntVector2();

                        if (localVisibleStart == localVisibleEnd) continue;
                        if (remoteVisibleStart == remoteVisibleEnd) continue;

                        var locallyClearedSegment = new IntLineSegment2(localVisibleStart, localVisibleEnd);
                        locallyClearedSegments.Add(locallyClearedSegment);

                        visibleCrossoverSegmentsByNeighbor.Add(otherTerrainNode, new IntLineSegment2(remoteVisibleStart, remoteVisibleEnd));
                     }
                  }
               }
               foreach (var locallyClearedSegment in locallyClearedSegments) {
                  visibilityPolygon.ClearBefore(locallyClearedSegment);
               }
            }
         }
         return visibleCrossoverSegmentsByNeighbor;
      }
   }
}