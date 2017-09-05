using System;
using System.Collections.Concurrent;
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
      public Dictionary<double, PathfindingContext> PathfindingContexts { get; set; } = new Dictionary<double, PathfindingContext>();

      public PathfindingContext GetPathfindingContext(double holeDilationRadius) {
         if (PathfindingContexts.TryGetValue(holeDilationRadius, out PathfindingContext cachedContext)) {
            return cachedContext;
         }
         return PathfindingContexts[holeDilationRadius] = new PathfindingContext { SectorSnapshots = SectorSnapshots };
      }
   }

   public class PathfindingContext {
      public IReadOnlyList<SectorSnapshot> SectorSnapshots { get; set; }
      public IReadOnlyDictionary<SectorWaypointAndCrossovers, VisibilityPolygon> VisibilityPolygonsFromWaypointsToCrossovers = new Dictionary<SectorWaypointAndCrossovers, VisibilityPolygon>();

      public bool TryFindSector(IntVector3 queryWorld, out SectorSnapshot result) {
         return TryFindSector(queryWorld.ToDoubleVector3(), out result);
      }

      public bool TryFindSector(DoubleVector3 queryWorld, out SectorSnapshot result) {
         return SectorSnapshots.TryFindFirst(sectorSnapshot => {
            var queryLocal = sectorSnapshot.WorldToLocal(queryWorld);
            var localBoundary = sectorSnapshot.StaticMetadata.LocalBoundary;
            return localBoundary.X <= queryLocal.X && queryLocal.X <= localBoundary.Right &&
                   localBoundary.Y <= queryLocal.Y && queryLocal.Y <= localBoundary.Bottom;
         }, out result);
      }

      public void FindVisibilityPolygon() {

      }

      public struct SectorWaypointAndCrossovers {
         public SectorSnapshot SectorSnapshot;
         public IntVector2 Waypoint;
         public CrossoverSnapshot[] Crossovers;

         private sealed class SectorWaypointAndCrossoversEqualityComparer : IEqualityComparer<SectorWaypointAndCrossovers> {
            public bool Equals(SectorWaypointAndCrossovers x, SectorWaypointAndCrossovers y) {
               if (x.SectorSnapshot != y.SectorSnapshot || x.Waypoint != y.Waypoint) {
                  return false;
               }
               if (x.Crossovers.Length != y.Crossovers.Length) {
                  return false;
               }
               for (var i = 0; i < x.Crossovers.Length; i++) {
                  if (x.Crossovers[i] != y.Crossovers[i]) {
                     return false;
                  }
               }
               return true;
            }

            public int GetHashCode(SectorWaypointAndCrossovers obj) {
               unchecked {
                  var hashCode = (obj.SectorSnapshot != null ? obj.SectorSnapshot.GetHashCode() : 0);
                  hashCode = (hashCode * 397) ^ obj.Waypoint.GetHashCode();
                  hashCode = (hashCode * 397) ^ (obj.Crossovers != null ? obj.Crossovers.GetHashCode() : 0);
                  hashCode = (hashCode * 397) ^ obj.Crossovers.Length;
                  foreach (var crossover in obj.Crossovers) {
                     hashCode = (hashCode * 397) ^ crossover.GetHashCode();
                  }
                  return hashCode;
               }
            }
         }

         public static readonly IEqualityComparer<SectorWaypointAndCrossovers> EqualityComparer = new SectorWaypointAndCrossoversEqualityComparer();
      }
   }

   public class CrossoverSnapshot {
      public Crossover Crossover { get; set; }
      public SectorSnapshot Remote { get; set; }
      public IntLineSegment2 LocalSegment { get; set; }
      public IntLineSegment2 RemoteSegment { get; set; }
      public Matrix3x2 LocalToRemote { get; set; }
      public Matrix3x2 RemoteToLocal { get; set; }
      public CrossoverSnapshot RemoteCrossover { get; set; }
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

      public DoubleVector3 LocalToWorld(IntVector2 p) => Vector3.Transform(new IntVector3(p).ToDotNetVector(), WorldTransform).ToOpenMobaVector();
      public DoubleVector3 LocalToWorld(DoubleVector2 p) => Vector3.Transform(new DoubleVector3(p).ToDotNetVector(), WorldTransform).ToOpenMobaVector();
      
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
         _dilatedHolesUnion ?? (_dilatedHolesUnion =
            PolygonOperations.Offset()
                             .Include(_sectorSnapshot.StaticMetadata.LocalExcludedContours)
                             .Dilate(_holeDilationRadius)
                             .Execute());

      public IntLineSegment2?[] ErodedCrossoverSegments =>
         _erodedCrossoverSegments ?? (_erodedCrossoverSegments =
            _sectorSnapshot.CrossoverSnapshots.Map(crossover =>
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
         _punchedLand ?? (_punchedLand =
            PostProcessPunchedLand(PolygonOperations.Punch()
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

         void TagSectorSnapshotAndGeometryContext(PolyNode node) {
            node.visibilityGraphNodeData.SectorSnapshot = this._sectorSnapshot;
            node.visibilityGraphNodeData.SectorSnapshotGeometryContext = this;
            node.Childs.ForEach(TagSectorSnapshotAndGeometryContext);
         }

         PrunePolytree(punchedLand);
         PopulatePolytreeCrossoverLabels();
         TagSectorSnapshotAndGeometryContext(punchedLand);
         return punchedLand;
      }

      public Triangulation Triangulation => _triangulation ?? (_triangulation = new Triangulator().Triangulate(PunchedLand));
   }
}
