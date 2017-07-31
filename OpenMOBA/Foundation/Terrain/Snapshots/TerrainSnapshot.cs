using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ClipperLib;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<CrossoverSnapshot> Crossovers { get; set; }
      public IReadOnlyList<SectorSnapshot> SectorSnapshots { get; set; }
      public IReadOnlyList<DynamicTerrainHole> TemporaryHoles { get; set; }
      public Dictionary<double, PathfindingContext> PathfindingContexts { get; set; }
   }

   public class PathfindingContext {
      public IReadOnlyList<SectorSnapshot> SectorSnapshots { get; set; }

      public void FindNearestSector(IntVector3 query) {
         foreach (var sector in SectorSnapshots) {
         }
      }
   }

   public class CrossoverSnapshot {
      public Crossover Crossover { get; set; }
      public SectorSnapshot Remote { get; set; }
      public IntLineSegment2 LocalSegment { get; set; }
      public IntLineSegment2 RemoteSegment { get; set; }
   }

   public class SectorSnapshot {
      public Sector Sector { get; set; }
      public TerrainStaticMetadata StaticMetadata => Sector.StaticMetadata;
      public Matrix4x4 WorldTransform;
      public Matrix4x4 WorldTransformInv;

      public List<CrossoverSnapshot> CrossoverSnapshots { get; set; } = new List<CrossoverSnapshot>();
      public Dictionary<Tuple<CrossoverSnapshot, CrossoverSnapshot>, List<IntLineSegment2>> BarriersBetweenCrossovers { get; set; } = new Dictionary<Tuple<CrossoverSnapshot, CrossoverSnapshot>, List<IntLineSegment2>>();

      // Helper for transforms
      public DoubleVector2 WorldToLocal(IntVector3 p) => WorldToLocal(p.ToDoubleVector3());
      public DoubleVector2 WorldToLocal(DoubleVector3 p) => Vector3.Transform(p.ToDotNetVector(), WorldTransformInv).ToOpenMobaVector().XY;
      public IntLineSegment2 WorldToLocal(IntLineSegment3 s) => new IntLineSegment2(WorldToLocal(s.First).LossyToIntVector2(), WorldToLocal(s.Second).LossyToIntVector2());

      // Geometry context
      public Dictionary<double, SectorSnapshotGeometryContext> GeometryContextsByHoleDilationRadius { get; set; } = new Dictionary<double, SectorSnapshotGeometryContext>();

      public SectorSnapshotGeometryContext GetGeometryContext(double holeDilationRadius) {
         if (GeometryContextsByHoleDilationRadius.TryGetValue(holeDilationRadius, out SectorSnapshotGeometryContext cachedResult)) {
            return cachedResult;
         }
         return GeometryContextsByHoleDilationRadius[holeDilationRadius] = new SectorSnapshotGeometryContext(this, holeDilationRadius);
      }
   }

   public class SectorSnapshotGeometryContext {
      private const int kCrossoverAdditionalPathingDilation = 2;

      private readonly SectorSnapshot _sectorSnapshot;
      private readonly double _holeDilationRadius;
      private readonly int _crossoverErosionRadius;
      private readonly int _crossoverErosionDiameterSquared;
      private readonly int _crossoverDilationFactor;

      private PolyTree _dilatedHolesUnion;
      private IntLineSegment2?[] _erodedCrossoverSegments;
      private PolyTree _punchedLand;
      private Triangulation _triangulation;

      public SectorSnapshotGeometryContext(SectorSnapshot sectorSnapshot, double holeDilationRadius) {
         _sectorSnapshot = sectorSnapshot;
         _holeDilationRadius = holeDilationRadius;

         _crossoverErosionRadius = (int)Math.Ceiling(_holeDilationRadius * 2);
         _crossoverErosionDiameterSquared = 4 * _crossoverErosionRadius * _crossoverErosionRadius;
         _crossoverDilationFactor = _crossoverErosionRadius / 2 + kCrossoverAdditionalPathingDilation;
      }

      public PolyNode DilatedHolesUnion =>
         _dilatedHolesUnion ?? (_dilatedHolesUnion = PolygonOperations.Offset()
                                                                      .Include(_sectorSnapshot.StaticMetadata.LocalExcludedContours)
                                                                      .Dilate(_holeDilationRadius)
                                                                      .Execute());

      public IntLineSegment2?[] ErodedCrossoverSegments =>
         _erodedCrossoverSegments ?? (_erodedCrossoverSegments = _sectorSnapshot.CrossoverSnapshots.Map(crossover =>
            crossover.LocalSegment.TryErode(_crossoverErosionRadius, out IntLineSegment2 erosionResult)
               ? erosionResult
               : (IntLineSegment2?)null));

      private PolyTree ComputeErodedOuterContour() =>
         PolygonOperations.Offset()
                          .Include(_sectorSnapshot.StaticMetadata.LocalIncludedContours)
                          .Erode(_holeDilationRadius)
                          .Execute();

      private IEnumerable<Polygon2> ComputeCrossoverLandPolys() =>
         ErodedCrossoverSegments.Where(s => s.HasValue)
                                .SelectMany(s => PolylineOperations.ExtrudePolygon(s.Value.Points, _crossoverDilationFactor)
                                                                   .FlattenToPolygons());

      public PolyTree PunchedLand =>
         _punchedLand ?? (_punchedLand = PostProcessPunchedLand(
            PolygonOperations.Punch()
                             .Include(ComputeErodedOuterContour().FlattenToPolygons())
                             .Include(ComputeCrossoverLandPolys())
                             .Exclude(DilatedHolesUnion.FlattenToPolygons())
                             .Execute()));

      private PolyTree PostProcessPunchedLand(PolyTree punchedLand) {
         void PopulatePolytreeCrossoverLabels() {
            var crossoverToPolyNode = punchedLand.visibilityGraphTreeData.CrossoverPolyNodes = new Dictionary<Crossover, PolyNode>();
            var crossoverSnapshotToPolyNode = punchedLand.visibilityGraphTreeData.CrossoverSnapshotPolyNodes = new Dictionary<CrossoverSnapshot, PolyNode>();
            var erodedCrossoverSegments = ErodedCrossoverSegments;
            for (var i = 0; i < erodedCrossoverSegments.Length; i++) {
               var segmentBox = erodedCrossoverSegments[i];
               if (!segmentBox.HasValue) {
                  continue;
               }
         
               var segment = segmentBox.Value;
         
               PolyNode match;
               bool isHole;
               punchedLand.PickDeepestPolynode(segment.First, out match, out isHole);
         
               if (isHole) {
                  throw new InvalidOperationException("Crossover in hole");
               }
         
               if (match.visibilityGraphNodeData.CrossoverSnapshots == null) {
                  match.visibilityGraphNodeData.CrossoverSnapshots = new List<CrossoverSnapshot>();
               }
         
               if (match.visibilityGraphNodeData.ErodedCrossoverSegments == null) {
                  match.visibilityGraphNodeData.ErodedCrossoverSegments = new List<IntLineSegment2>();
               }
         
               match.visibilityGraphNodeData.CrossoverSnapshots.Add(_sectorSnapshot.CrossoverSnapshots[i]);
               match.visibilityGraphNodeData.ErodedCrossoverSegments.Add(segment);
               crossoverToPolyNode.Add(_sectorSnapshot.CrossoverSnapshots[i].Crossover, match);
               crossoverSnapshotToPolyNode.Add(_sectorSnapshot.CrossoverSnapshots[i], match);
            }
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

         PrunePolytree(punchedLand);
         PopulatePolytreeCrossoverLabels();
         return punchedLand;
      }

      public Triangulation Triangulation => _triangulation ?? (_triangulation = new Triangulator().Triangulate(PunchedLand));
   }

   //      private const int kCrossoverAdditionalPathingDilation = 2;
   //
   //      // Outputs
   //      private readonly Dictionary<double, PolyTree> dilatedHolesUnionCache = new Dictionary<double, PolyTree>();
   //      private readonly Dictionary<double, IntLineSegment2?[]> erodedCrossoverSegmentsCache = new Dictionary<double, IntLineSegment2?[]>();
   //      private readonly Dictionary<double, Dictionary<DoubleVector2, VisibilityPolygon>> lineOfSightCaches = new Dictionary<double, Dictionary<DoubleVector2, VisibilityPolygon>>();
   //      private readonly Dictionary<double, PolyTree> punchedLandCache = new Dictionary<double, PolyTree>();
   //      private readonly Dictionary<double, Triangulation> triangulationCache = new Dictionary<double, Triangulation>();
   //      private readonly Dictionary<double, SectorVisibilityGraph> visibilityGraphCache = new Dictionary<double, SectorVisibilityGraph>();
   //
   //      public DoubleVector2 WorldToLocal(IntVector3 p) => WorldToLocal(p.ToDoubleVector3());
   //      public DoubleVector2 WorldToLocal(DoubleVector3 p) => Vector3.Transform(p.ToDotNetVector(), WorldTransformInv).ToOpenMobaVector().XY;
   //      public IntLineSegment2 WorldToLocal(IntLineSegment3 s) => new IntLineSegment2(WorldToLocal(s.First).LossyToIntVector2(), WorldToLocal(s.Second).LossyToIntVector2());
   //
   //      public PolyTree ComputeDilatedHolesUnion(double holeDilationRadius) {
   //         PolyTree dilatedHolesUnion;
   //         if (!dilatedHolesUnionCache.TryGetValue(holeDilationRadius, out dilatedHolesUnion)) {
   //            dilatedHolesUnion = PolygonOperations.Offset()
   //                                                 .Include(StaticMetadata.LocalExcludedContours)
   //                                                 .Dilate(holeDilationRadius)
   //                                                 .Execute();
   //            dilatedHolesUnionCache[holeDilationRadius] = dilatedHolesUnion;
   //         }
   //         return dilatedHolesUnion;
   //      }
   //
   //      public IntLineSegment2?[] ComputeErodedCrossoverSegments(double segmentDilationRadius) {
   //         IntLineSegment2?[] result;
   //         if (erodedCrossoverSegmentsCache.TryGetValue(segmentDilationRadius, out result)) {
   //            return result;
   //         }
   //
   //         result = new IntLineSegment2?[CrossoverSnapshots.Count];
   //         for (var i = 0; i < result.Length; i++) {
   //            var segment = CrossoverSnapshots[i].LocalSegment;
   //            var a = segment.First.ToDoubleVector2();
   //            var b = segment.Second.ToDoubleVector2();
   //            var aToB = a.To(b);
   //            var aToBMag = aToB.Norm2D();
   //            if (aToBMag <= 2 * segmentDilationRadius) continue;
   //
   //            var shrink = (aToB * segmentDilationRadius / aToBMag).LossyToIntVector2();
   //            result[i] = new IntLineSegment2(
   //               a.LossyToIntVector2() + shrink,
   //               b.LossyToIntVector2() - shrink);
   //         }
   //         return erodedCrossoverSegmentsCache[segmentDilationRadius] = result;
   //      }
   //
   //      public PolyTree ComputePunchedLand(double holeDilationRadius) {
   //         PolyTree punchedLand;
   //         if (punchedLandCache.TryGetValue(holeDilationRadius, out punchedLand)) {
   //            return punchedLand;
   //         }
   //
   //         var landPoly = PolygonOperations.Offset()
   //                                         .Include(StaticMetadata.LocalIncludedContours)
   //                                         .Erode(holeDilationRadius)
   //                                         .Execute().FlattenToPolygons();
   //
   //         var crossoverErosionFactor = (int)Math.Ceiling(holeDilationRadius * 2);
   //         var crossoverDilationFactor = crossoverErosionFactor / 2 + 2;
   //         var erodedCrossovers = ComputeErodedCrossoverSegments(crossoverErosionFactor);
   //         var crossoverLandPolys = erodedCrossovers.Where(s => s.HasValue)
   //                                                  .SelectMany(s => PolylineOperations.ExtrudePolygon(s.Value.Points, crossoverDilationFactor)
   //                                                                                     .FlattenToPolygons());
   //
   //         var dilatedHolesUnion = ComputeDilatedHolesUnion(holeDilationRadius);
   //         punchedLand = PolygonOperations.Punch()
   //                                          .Include(landPoly)
   //                                          .Include(crossoverLandPolys)
   //                                          .Exclude(dilatedHolesUnion.FlattenToPolygons())
   //                                          .Execute();
   //
   //         PrunePolytree(punchedLand);
   //         PopulatePolytreeCrossoverLabels(punchedLand, holeDilationRadius);
   //         punchedLandCache[holeDilationRadius] = punchedLand;
   //         return punchedLand;
   //      }
   //
   //      private void PopulatePolytreeCrossoverLabels(PolyTree punchedLand, double holeDilationRadius) {
   //         var crossoverToPolyNode = punchedLand.visibilityGraphTreeData.CrossoverPolyNodes = new Dictionary<Crossover, PolyNode>();
   //         var crossoverSnapshotToPolyNode = punchedLand.visibilityGraphTreeData.CrossoverSnapshotPolyNodes = new Dictionary<CrossoverSnapshot, PolyNode>();
   //         var erodedCrossoverSegments = ComputeErodedCrossoverSegments(holeDilationRadius + kCrossoverAdditionalPathingDilation);
   //         for (var i = 0; i < erodedCrossoverSegments.Length; i++) {
   //            var segmentBox = erodedCrossoverSegments[i];
   //            if (!segmentBox.HasValue) {
   //               continue;
   //            }
   //
   //            var segment = segmentBox.Value;
   //
   //            PolyNode match;
   //            bool isHole;
   //            punchedLand.PickDeepestPolynode(segment.First, out match, out isHole);
   //
   //            if (isHole) {
   //               throw new InvalidOperationException("Crossover in hole");
   //            }
   //
   //            if (match.visibilityGraphNodeData.CrossoverSnapshots == null) {
   //               match.visibilityGraphNodeData.CrossoverSnapshots = new List<CrossoverSnapshot>();
   //            }
   //
   //            if (match.visibilityGraphNodeData.ErodedCrossoverSegments == null) {
   //               match.visibilityGraphNodeData.ErodedCrossoverSegments = new List<IntLineSegment2>();
   //            }
   //
   //            match.visibilityGraphNodeData.CrossoverSnapshots.Add(CrossoverSnapshots[i]);
   //            match.visibilityGraphNodeData.ErodedCrossoverSegments.Add(segment);
   //            crossoverToPolyNode.Add(CrossoverSnapshots[i].Crossover, match);
   //            crossoverSnapshotToPolyNode.Add(CrossoverSnapshots[i], match);
   //         }
   //      }
   //
   //      private void PrunePolytree(PolyNode polyTree) {
   //         for (var i = polyTree.Childs.Count - 1; i >= 0; i--) {
   //            var child = polyTree.Childs[i];
   //            if (Math.Abs(Clipper.Area(child.Contour)) < 16 * 16) {
   //               Console.WriteLine("Prune: " + Clipper.Area(child.Contour) + " " + child.Contour.Count);
   //               polyTree.Childs.RemoveAt(i);
   //               continue;
   //            }
   //
   //            PrunePolytree(child);
   //         }
   //      }
   //
   //      public Triangulation ComputeTriangulation(double holeDilationRadius) {
   //         Triangulation triangulation;
   //         if (!triangulationCache.TryGetValue(holeDilationRadius, out triangulation)) {
   //            var triangulator = new Triangulator();
   //            triangulation = triangulator.Triangulate(ComputePunchedLand(holeDilationRadius));
   //            triangulationCache[holeDilationRadius] = triangulation;
   //         }
   //         return triangulation;
   //      }
}
