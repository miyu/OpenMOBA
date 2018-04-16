using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.CompilationResults.Local {
   public class LocalGeometryView {
      private const int kCrossoverAdditionalPathingDilation = 2;
      private static readonly Polygon2 ClipperExtentsHoleClipPolygon = Polygon2.CreateRect(-ClipperBase.loRange + 10, -ClipperBase.loRange + 10, ClipperBase.loRange * 2 - 20, ClipperBase.loRange * 2 - 20);


      public readonly LocalGeometryViewManager LocalGeometryViewManager;
      public readonly double HoleDilationRadius;
      public readonly LocalGeometryView Preview;

      public readonly int CrossoverErosionRadius;
      public readonly int CrossoverDilationFactor;

      public LocalGeometryView(LocalGeometryViewManager localGeometryViewManager, double holeDilationRadius, LocalGeometryView preview) {
         LocalGeometryViewManager = localGeometryViewManager;
         HoleDilationRadius = holeDilationRadius;
         Preview = preview ?? this;

         CrossoverErosionRadius = (int)Math.Ceiling((double)(HoleDilationRadius * 2));
         CrossoverDilationFactor = (CrossoverErosionRadius / 2) + kCrossoverAdditionalPathingDilation;
      }

      public LocalGeometryJob Job => LocalGeometryViewManager.Job;
      public bool IsPunchedLandEvaluated => _punchedLand != null;

      private PolyTree _dilatedHolesUnion;
      //private IntLineSegment2?[] _erodedCrossoverSegments;
      private PolyTree _punchedLand;
      private Triangulation _triangulation;

      public PolyNode DilatedHolesUnion =>
         _dilatedHolesUnion ?? (_dilatedHolesUnion =
            PolygonOperations.Offset()
                             .Include(Job.TerrainStaticMetadata.LocalExcludedContours)
                             .Include(Job.DynamicHoles.Values.SelectMany(item => item.holeIncludedContours)
                                                             .Select(p => ClipHoleContour(p))
                                                             .Where(p => p != null))
                             .Include(Job.DynamicHoles.Values.SelectMany(item => 
                                 item.holeExcludedContours.Select(p => new Polygon2(((IReadOnlyList<IntVector2>)p.Points).Reverse().ToList(), true))
                             ).Select(p => ClipHoleContour(p)).Where(p => p != null))
                             .Dilate(HoleDilationRadius)
                             .Cleanup()
                             .Execute());

      private Polygon2 ClipHoleContour(Polygon2 polygon) => PolygonOperations.TryConvexClip(polygon, ClipperExtentsHoleClipPolygon, out var result) ? result : null;

      //public IntLineSegment2?[] ErodedBoundaryCrossoverSegments =>
      //   _erodedCrossoverSegments ?? (_erodedCrossoverSegments =
      //      Job.CrossoverSegments.Select(segment =>
      //         segment.TryErode(CrossoverErosionRadius, out IntLineSegment2 erosionResult)
      //            ? erosionResult
      //            : (IntLineSegment2?)null).ToArray());

      public PolyTree ComputeErodedOuterContour() =>
         PolygonOperations.Offset()
                          .Include(Job.TerrainStaticMetadata.LocalIncludedContours)
                          .Erode(HoleDilationRadius)
                          .Cleanup()
                          .Execute();

      internal IEnumerable<Polygon2> ComputeCrossoverLandPolys() {
         return Job.CrossoverSegments.Select(segment => {
            var firstToSecond = segment.First.To(segment.Second).ToDoubleVector2();
            var perp = new DoubleVector2(firstToSecond.Y, -firstToSecond.X);
            var extrusionMagnitude = HoleDilationRadius + 2;
            var extrusion = perp * (extrusionMagnitude / perp.Norm2D());
            var shrink = firstToSecond * (HoleDilationRadius / firstToSecond.Norm2D());
            var points = new List<IntVector2>(new []{
               (segment.First.ToDoubleVector2() - extrusion + shrink).LossyToIntVector2(),
               (segment.First.ToDoubleVector2() + extrusion + shrink).LossyToIntVector2(),
               (segment.Second.ToDoubleVector2() + extrusion - shrink).LossyToIntVector2(),
               (segment.Second.ToDoubleVector2() - extrusion - shrink).LossyToIntVector2()
            });
            return new Polygon2(points, false);
         }).ToArray();
      }

      public PolyTree PunchedLand =>
         _punchedLand ?? (_punchedLand =
            PostProcessPunchedLand(
               PolygonOperations.Punch()
                                .Include(ComputeErodedOuterContour().FlattenToPolygons())
                                .Include(ComputeCrossoverLandPolys())
                                .Exclude(DilatedHolesUnion.FlattenToPolygons())
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