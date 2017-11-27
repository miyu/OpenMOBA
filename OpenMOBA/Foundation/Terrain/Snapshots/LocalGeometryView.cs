using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class LocalGeometryView {
      private const int kCrossoverAdditionalPathingDilation = 2;

      public readonly LocalGeometryViewManager LocalGeometryViewManager;
      public readonly double ActorRadius;
      public readonly LocalGeometryView Preview;

      public readonly int CrossoverErosionRadius;
      public readonly int CrossoverDilationFactor;

      public LocalGeometryView(LocalGeometryViewManager localGeometryViewManager, double actorRadius, LocalGeometryView preview) {
         LocalGeometryViewManager = localGeometryViewManager;
         ActorRadius = actorRadius;
         Preview = preview ?? this;

         CrossoverErosionRadius = (int)Math.Ceiling((double)(ActorRadius * 2));
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
            PolygonOperations.Offset().Include((IEnumerable<Polygon2>)Job.TerrainStaticMetadata.LocalExcludedContours)
                             .Dilate(ActorRadius)
                             .Execute());

      //public IntLineSegment2?[] ErodedBoundaryCrossoverSegments =>
      //   _erodedCrossoverSegments ?? (_erodedCrossoverSegments =
      //      Job.CrossoverSegments.Select(segment =>
      //         segment.TryErode(CrossoverErosionRadius, out IntLineSegment2 erosionResult)
      //            ? erosionResult
      //            : (IntLineSegment2?)null).ToArray());

      private PolyTree ComputeErodedOuterContour() =>
         PolygonOperations.Offset().Include((IEnumerable<Polygon2>)Job.TerrainStaticMetadata.LocalIncludedContours)
                          .Erode(ActorRadius)
                          .Execute();

      private IEnumerable<Polygon2> ComputeCrossoverLandPolys() {
         return Enumerable.Select<IntLineSegment2, Polygon2>(Job.CrossoverSegments, segment => {
            var firstToSecond = segment.First.To(segment.Second).ToDoubleVector2();
            var perp = new DoubleVector2(firstToSecond.Y, -firstToSecond.X);
            var extrusionMagnitude = ActorRadius;
            var extrusion = perp * (extrusionMagnitude / perp.Norm2D());
            var shrink = firstToSecond * (ActorRadius / firstToSecond.Norm2D());
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
                                .Include(PolygonOperations.FlattenToPolygons(ComputeErodedOuterContour())).Include((IEnumerable<Polygon2>)ComputeCrossoverLandPolys())
                                .Exclude(PolygonOperations.FlattenToPolygons(DilatedHolesUnion))
                                .Execute()
            ));

      private PolyTree PostProcessPunchedLand(PolyTree punchedLand) {
         var pruneArea = (2 * ActorRadius + 2) * (2 * ActorRadius + 2);
         void PrunePolytree(PolyNode polyTree) {
            var cleaned = Clipper.CleanPolygon(polyTree.Contour, ActorRadius / 5 + 2);
            polyTree.Contour.Clear();
            polyTree.Contour.AddRange(cleaned);

            for (var i = polyTree.Childs.Count - 1; i >= 0; i--) {
               var child = polyTree.Childs[i];
               if (Math.Abs(Clipper.Area(child.Contour)) < pruneArea) {
                  // Console.WriteLine("Prune: " + Clipper.Area(child.Contour) + " " + child.Contour.Count);
                  polyTree.Childs.RemoveAt(i);
                  continue;
               }

               PrunePolytree(child);
            }
         }

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

         PrunePolytree(punchedLand);
         TagSectorSnapshotAndGeometryContext(punchedLand);
         TagBoundingVolumeHierarchies(punchedLand);
         return punchedLand;
      }

      public Triangulation Triangulation => _triangulation ?? (_triangulation = new Triangulator().Triangulate(PunchedLand));
   }
}