using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class SectorSnapshotGeometryContext {
      private const int kCrossoverAdditionalPathingDilation = 2;
      private readonly int _crossoverDilationFactor;
      private readonly int _crossoverErosionDiameterSquared;
      private readonly int _crossoverErosionRadius;
      private readonly double _holeDilationRadius;

      private readonly SectorSnapshot _sectorSnapshot;

      private PolyTree _dilatedHolesUnion;
      private IntLineSegment2?[] _erodedCrossoverSegments;
      private PolyTree _punchedLand;
      private Triangulation _triangulation;

      public SectorSnapshotGeometryContext(SectorSnapshot sectorSnapshot, double holeDilationRadius) {
         _sectorSnapshot = sectorSnapshot;
         _holeDilationRadius = holeDilationRadius;

         _crossoverErosionRadius = (int)Math.Ceiling((double)(_holeDilationRadius * 2));
         _crossoverErosionDiameterSquared = 4 * _crossoverErosionRadius * _crossoverErosionRadius;
         _crossoverDilationFactor = _crossoverErosionRadius / 2 + kCrossoverAdditionalPathingDilation;
      }

      public PolyNode DilatedHolesUnion =>
         _dilatedHolesUnion ?? (_dilatedHolesUnion =
            PolygonOperations.Offset().Include(_sectorSnapshot.StaticMetadata.LocalExcludedContours)
                             .Dilate(_holeDilationRadius)
                             .Execute());

      public IntLineSegment2?[] ErodedBoundaryCrossoverSegments =>
         _erodedCrossoverSegments ?? (_erodedCrossoverSegments =
            _sectorSnapshot.SourceSegmentEdgeDescriptions.Map(edgeDescription =>
               edgeDescription.SourceSegment.TryErode(_crossoverErosionRadius, out IntLineSegment2 erosionResult)
                  ? erosionResult
                  : (IntLineSegment2?)null));

      public PolyTree PunchedLand =>
         _punchedLand ?? (_punchedLand =
            PostProcessPunchedLand(PolygonOperations.Punch()
                                                    .Include(ComputeErodedOuterContour().FlattenToPolygons()).Include(ComputeCrossoverLandPolys())
                                                    .Exclude(DilatedHolesUnion.FlattenToPolygons())
                                                    .Execute()));

      public Triangulation Triangulation => _triangulation ?? (_triangulation = new Triangulator().Triangulate(PunchedLand));

      private PolyTree ComputeErodedOuterContour() {
         return PolygonOperations.Offset().Include(_sectorSnapshot.StaticMetadata.LocalIncludedContours)
                                 .Erode(_holeDilationRadius)
                                 .Execute();
      }

      private IEnumerable<Polygon2> ComputeCrossoverLandPolys() {
         return ErodedBoundaryCrossoverSegments.Where(s => s.HasValue)
                          .SelectMany(s => PolylineOperations.ExtrudePolygon(s.Value.Points, _crossoverDilationFactor)
                                                             .FlattenToPolygons());
      }

      private PolyTree PostProcessPunchedLand(PolyTree punchedLand) {
         void PopulatePolytreeCrossoverLabels() {
//            var crossoverToPolyNode = punchedLand.visibilityGraphTreeData.CrossoverPolyNodes = new Dictionary<Crossover, PolyNode>();
//            var crossoverSnapshotToPolyNode = punchedLand.visibilityGraphTreeData.CrossoverSnapshotPolyNodes = new Dictionary<CrossoverSnapshot, PolyNode>();
//            var erodedCrossoverSegments = ErodedCrossoverSegments;
//            for (var i = 0; i < erodedCrossoverSegments.Length; i++) {
//               var segmentBox = erodedCrossoverSegments[i];
//               if (!segmentBox.HasValue) continue;

//               var segment = segmentBox.Value;

//               PolyNode match;
//               bool isHole;
//               punchedLand.PickDeepestPolynode(segment.First, out match, out isHole);

//               if (isHole) throw new InvalidOperationException("Crossover in hole");

//               if (match.visibilityGraphNodeData.EdgeDescriptions == null) match.visibilityGraphNodeData.EdgeDescriptions = new List<CrossoverSnapshot>();

//               if (match.visibilityGraphNodeData.ErodedCrossoverSegments == null) match.visibilityGraphNodeData.ErodedCrossoverSegments = new List<IntLineSegment2>();

//               match.visibilityGraphNodeData.EdgeDescriptions.Add(_sectorSnapshot.CrossoverSnapshots[i]);
//               match.visibilityGraphNodeData.ErodedCrossoverSegments.Add(segment);
//               crossoverToPolyNode.Add(_sectorSnapshot.CrossoverSnapshots[i].Crossover, match);
//               crossoverSnapshotToPolyNode.Add(_sectorSnapshot.CrossoverSnapshots[i], match);
//            }
         }

         void PrunePolytree(PolyNode polyTree) {
            for (var i = polyTree.Childs.Count - 1; i >= 0; i--) {
               var child = polyTree.Childs[i];
               if (Math.Abs(Clipper.Area(child.Contour)) < 16 * 16) {
                  Console.WriteLine("Prune: " + Clipper.Area(child.Contour) + " " + child.Contour.Count);
                  polyTree.Childs.RemoveAt(i);
                  continue;
               }

               PrunePolytree(child);
            }
         }

         void TagSectorSnapshotAndGeometryContext(PolyNode node) {
            node.visibilityGraphNodeData.SectorSnapshot = _sectorSnapshot;
            node.visibilityGraphNodeData.SectorSnapshotGeometryContext = this;
            node.Childs.ForEach(TagSectorSnapshotAndGeometryContext);
         }

         PrunePolytree(punchedLand);
         PopulatePolytreeCrossoverLabels();
         TagSectorSnapshotAndGeometryContext(punchedLand);
         return punchedLand;
      }
   }
}