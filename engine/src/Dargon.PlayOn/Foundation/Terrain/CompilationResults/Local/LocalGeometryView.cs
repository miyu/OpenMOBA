using System;
using System.Collections.Generic;
using System.Linq;
using Dargon.Commons;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.ThirdParty.ClipperLib;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local {
   public class LocalGeometryView {
      private const int kCrossoverAdditionalPathingDilation = 2;

      public readonly LocalGeometryViewManager LocalGeometryViewManager;
      public readonly cDouble HoleDilationRadius;
      public readonly LocalGeometryView Preview;

      public readonly int CrossoverErosionRadius;
      public readonly int CrossoverDilationFactor;

      private readonly Polygon2 ClipperExtentsHoleClipPolygon;
      private readonly Guid guid = Guid.NewGuid();

      public LocalGeometryView(LocalGeometryViewManager localGeometryViewManager, cDouble holeDilationRadius, LocalGeometryView preview) {
         LocalGeometryViewManager = localGeometryViewManager;
         HoleDilationRadius = holeDilationRadius;
         Preview = preview ?? this;

         CrossoverErosionRadius = (int)CDoubleMath.Ceiling((HoleDilationRadius * (cDouble)2));
         CrossoverDilationFactor = (CrossoverErosionRadius / 2) + kCrossoverAdditionalPathingDilation;

         ClipperExtentsHoleClipPolygon = Polygon2.CreateRect(
            -InternalTerrainCompilationConstants.SectorClipBounds,
            -InternalTerrainCompilationConstants.SectorClipBounds,
            InternalTerrainCompilationConstants.SectorClipBounds * 2 + 1,
            InternalTerrainCompilationConstants.SectorClipBounds * 2 + 1);
      }

      public LocalGeometryJob Job => LocalGeometryViewManager.Job;
      public bool IsPunchedLandEvaluated => _punchedLand != null;
      public Guid Guid => guid;

      private PolyTree _dilatedHolesUnion;
      private PolyTree _punchedLand;
      private Triangulation _triangulation;

      public PolyNode DilatedHolesUnion => _dilatedHolesUnion ?? (_dilatedHolesUnion =
         PolygonOperations.Offset()
                          .Include(Job.TerrainStaticMetadata.LocalExcludedContours)
                          .Include(Job.DynamicHoles.Values.SelectMany(item => item.holeIncludedContours)
                                      .Select(ClipHoleContour)
                                      .Where(p => p != null))
                          .Include(Job.DynamicHoles.Values.SelectMany(item =>
                             item.holeExcludedContours.Select(p => new Polygon2(((IReadOnlyList<IntVector2>)p.Points).Reverse().ToList()))
                             ).Select(ClipHoleContour).Where(p => p != null))
                          .Dilate(HoleDilationRadius)
                          .Cleanup()
                          .Execute());

      private Polygon2 ClipHoleContour(Polygon2 polygon) {
         PolygonOperations.TryConvexClip(polygon, ClipperExtentsHoleClipPolygon, out var result);
         if (result != null) {
            foreach (var p in result.Points) {
               var useFullRange = true;
               ClipperBase.RangeTest(p, ref useFullRange);
            }
         }
         return result;
      }

      public PolyTree ComputeErodedOuterContour() =>
         PolygonOperations.Offset()
                          .Include(Job.TerrainStaticMetadata.LocalIncludedContours)
                          .Erode(HoleDilationRadius)
                          .Cleanup()
                          .Execute();

      public IEnumerable<Polygon2> ComputeCrossoverLandPolys() {
         return Job.CrossoverSegments.Select(tuple => {
            var (segment, inClockness) = tuple;
            var firstToSecond = segment.First.To(segment.Second).ToDoubleVector2();
            var perp = new DoubleVector2(firstToSecond.Y, -firstToSecond.X);
            var extrusionMagnitude = HoleDilationRadius + (cDouble)2;
            var inward = perp * (extrusionMagnitude / perp.Norm2D());
            var outward = perp * (CDoubleMath.cNeg2 / perp.Norm2D());
            if (inClockness == Clockness.ClockWise) {
               inward *= -1;
               outward *= -1;
            }

            var shrink = firstToSecond * (HoleDilationRadius / firstToSecond.Norm2D());
            var points = new List<IntVector2>(new []{
               (segment.First.ToDoubleVector2() + outward + shrink).LossyToIntVector2(),
               (segment.First.ToDoubleVector2() + inward + shrink).LossyToIntVector2(),
               (segment.Second.ToDoubleVector2() + inward - shrink).LossyToIntVector2(),
               (segment.Second.ToDoubleVector2() + outward - shrink).LossyToIntVector2()
            });

            if (inClockness == Clockness.ClockWise) {
               points.Reverse();
            }

            return new Polygon2(points);
         }).ToArray();
      }

      public PolyTree PunchedLand =>
         _punchedLand ?? (_punchedLand =
            PostProcessPunchedLand(
               PolygonOperations.Punch()
                                .Include(ComputeErodedOuterContour().FlattenToPolygonAndIsHoles())
                                .Include(ComputeCrossoverLandPolys())
                                .Exclude(DilatedHolesUnion.FlattenToPolygonAndIsHoles())
                                .Execute()
            ));

      private PolyTree PostProcessPunchedLand(PolyTree punchedLand) {
         void TagSectorSnapshotAndGeometryContext(PolyNode node) {
            node.visibilityGraphNodeData.LocalGeometryView = this;
            node.Childs.ForEach(TagSectorSnapshotAndGeometryContext);
         }

         void TagBoundingVolumeHierarchies(PolyNode node) {
            var contourEdges = node.Contour.Zip(node.Contour.RotateLeft(), IntLineSegment2.Create).ToArray();
            var bvh = BvhILS2.Build(contourEdges);
            node.visibilityGraphNodeData.ContourBvh = bvh;
            node.Childs.ForEach(TagBoundingVolumeHierarchies);
         }

         punchedLand.Prune(HoleDilationRadius);
         TagSectorSnapshotAndGeometryContext(punchedLand);
         TagBoundingVolumeHierarchies(punchedLand);
         return punchedLand;
      }

      public Triangulation Triangulation => _triangulation ?? (_triangulation = new Triangulator().TriangulateRoot(PunchedLand));
   }
}