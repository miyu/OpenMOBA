using System;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ClipperLib;

namespace OpenMOBA.Foundation.Terrain {
   public enum ClipperPointInPolygonResult {
      OutsidePolygon = 0,
      OnPolygon = -1,
      InPolygon = 1
   }

   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<CrossoverSnapshot> CrossoverSnapshots { get; set; }
      public IReadOnlyList<SectorSnapshot> SectorSnapshots { get; set; }
      public IReadOnlyList<TerrainHole> TemporaryHoles { get; set; }
   }

   public class Sector {
      public Rectangle AbsoluteBounds { get; set; }
      public List<Polygon> StaticHolePolygons { get; set; }
   }

   public class Crossover {
      public Sector A { get; set; }
      public Sector B { get; set; }
      public IntLineSegment3 Segment { get; set; }
   }

   public class CrossoverSnapshot {
      public SectorSnapshot A { get; set; }
      public SectorSnapshot B { get; set; }
      public IntLineSegment3 Segment { get; set; }
   }

   public class SectorSnapshot {
      public Sector Sector { get; set; }
      public Rectangle AbsoluteBounds { get; set; }
      public List<Polygon> StaticHolePolygons { get; set; }
      public IReadOnlyList<TerrainHole> TemporaryHoles { get; set; }
      public Dictionary<SectorSnapshot, List<CrossoverSnapshot>> Crossovers { get; set; } = new Dictionary<SectorSnapshot, List<CrossoverSnapshot>>();

      private readonly Dictionary<double, PolyTree> dilatedHolesUnionCache = new Dictionary<double, PolyTree>();
      private readonly Dictionary<double, PolyTree> punchedLandCache = new Dictionary<double, PolyTree>();
      private readonly Dictionary<double, VisibilityGraph> visibilityGraphCache = new Dictionary<double, VisibilityGraph>();
      private readonly Dictionary<double, Dictionary<DoubleVector2, AngularVisibleSegmentStore>> lineOfSightCaches = new Dictionary<double, Dictionary<DoubleVector2, AngularVisibleSegmentStore>>();
      private readonly Dictionary<double, Triangulation> triangulationCache = new Dictionary<double, Triangulation>();

      public PolyTree ComputeDilatedHolesUnion(double holeDilationRadius) {
         PolyTree dilatedHolesUnion;
         if (!dilatedHolesUnionCache.TryGetValue(holeDilationRadius, out dilatedHolesUnion)) {
            dilatedHolesUnion = PolygonOperations.Offset()
                                                 .Include(StaticHolePolygons)
                                                 .Include(TemporaryHoles.SelectMany(h => h.Polygons))
                                                 .Dilate(holeDilationRadius)
                                                 .Execute();
            dilatedHolesUnionCache[holeDilationRadius] = dilatedHolesUnion;
         }
         return dilatedHolesUnion;
      }

      public PolyTree ComputePunchedLand(double holeDilationRadius) {
         PolyTree punchedLand;
         if (!punchedLandCache.TryGetValue(holeDilationRadius, out punchedLand)) {
            var landPoly = Polygon.CreateRectXY(
               (int)holeDilationRadius + AbsoluteBounds.X,
               (int)holeDilationRadius + AbsoluteBounds.Y,
               (int)(AbsoluteBounds.Width - 2 * holeDilationRadius),
               (int)(AbsoluteBounds.Height - 2 * holeDilationRadius),
               0);

            var crossoverLandPolys = new List<Polygon>();
            foreach (var kvp in Crossovers) {
               var crossoverErosionFactor = (int)Math.Ceiling(holeDilationRadius * 2);
               var crossoverDilationFactor = crossoverErosionFactor / 2 + 2;
               foreach (var crossover in kvp.Value) {
                  var segment = crossover.Segment;
                  var a = segment.First;
                  var b = segment.Second;
                  var aToB = a.To(b);
                  var aToBMag = aToB.Norm2F();
                  if (aToB.XY == IntVector2.Zero || aToBMag <= 2 * holeDilationRadius) continue;

                  var shrink = ((aToB.ToDoubleVector3() * crossoverErosionFactor) / aToB.Norm2F()).LossyToIntVector3();
                     //2 * ((aToB * crossoverDilationFactor).ToDoubleVector3() / aToB.Norm2F()).LossyToIntVector3();
                  var crossoverPolyTree = PolylineOperations.ExtrudePolygon(new[] {
                     crossover.Segment.First + shrink,
                     crossover.Segment.Second - shrink
                  }, crossoverDilationFactor);
                  crossoverLandPolys.AddRange(crossoverPolyTree.FlattenToPolygons());
               }
            }

            var dilatedHolesUnion = ComputeDilatedHolesUnion(holeDilationRadius);
            punchedLand = PolygonOperations.Punch()
                                           .Include(landPoly)
                                           .Include(crossoverLandPolys)
                                           .Exclude(dilatedHolesUnion.FlattenToPolygons())
                                           .Execute();
            punchedLandCache[holeDilationRadius] = punchedLand;
         }
         return punchedLand;
      }

      public VisibilityGraph ComputeVisibilityGraph(double holeDilationRadius) {
         VisibilityGraph visibilityGraph;
         if (!visibilityGraphCache.TryGetValue(holeDilationRadius, out visibilityGraph)) {
            var punchedLand = ComputePunchedLand(holeDilationRadius);
            visibilityGraph = VisibilityGraphOperations.CreateVisibilityGraph(punchedLand);
            visibilityGraphCache[holeDilationRadius] = visibilityGraph;
         }
         return visibilityGraph;
      }

      public AngularVisibleSegmentStore ComputeLineOfSight(DoubleVector2 position, double holeDilationRadius) {
         Dictionary<DoubleVector2, AngularVisibleSegmentStore> lineOfSightCache;
         if (!lineOfSightCaches.TryGetValue(holeDilationRadius, out lineOfSightCache)) {
            lineOfSightCache = new Dictionary<DoubleVector2, AngularVisibleSegmentStore>();
            lineOfSightCaches[holeDilationRadius] = lineOfSightCache;
         }

         AngularVisibleSegmentStore lineOfSight;
         if (!lineOfSightCache.TryGetValue(position, out lineOfSight)) {
            var barriers = ComputeVisibilityGraph(holeDilationRadius).Barriers;
            lineOfSight = new AngularVisibleSegmentStore(position);
            foreach (var barrier in barriers) {
               lineOfSight.Insert(barrier);
            }
            lineOfSightCache[position] = lineOfSight;
         }

         return lineOfSight;
      }

      public Triangulation ComputeTriangulation(double holeDilationRadius) {
         Triangulation triangulation;
         if (!triangulationCache.TryGetValue(holeDilationRadius, out triangulation)) {
            var triangulator = new Triangulator();
            triangulation = triangulator.Triangulate(ComputePunchedLand(holeDilationRadius));
            triangulationCache[holeDilationRadius] = triangulation;
         }
         return triangulation;
      }
   }

   public static class TerrainQueryOperations {
      public static bool IsInHole(this SectorSnapshot sectorSnapshot, double holeDilationRadius, IntVector3 query) {
         var punchedLandPolytree = sectorSnapshot.ComputePunchedLand(holeDilationRadius);
         punchedLandPolytree.AssertIsContourlessRootHolePunchResult();

         PolyNode pickedNode;
         bool isHole;
         punchedLandPolytree.PickDeepestPolynode(query.XY, out pickedNode, out isHole);

         return isHole;
      }
      
      /// <summary>
      /// Note: Containment definition varies by hole vs terrain: Containment for holes
      /// does not include the hole edge, containment for terrain includes the terrain edge.
      /// This is important, else e.g. knockback + terrain push placing an entity on an edge
      /// would potentially infinite loop.
      /// </summary>
      public static bool FindNearestLandPointAndIsInHole(this SectorSnapshot sectorSnapshot, double holeDilationRadius, DoubleVector3 query, out DoubleVector3 nearestLandPoint) {
         var punchedLandPolytree = sectorSnapshot.ComputePunchedLand(holeDilationRadius);
         punchedLandPolytree.AssertIsContourlessRootHolePunchResult();

         PolyNode pickedNode;
         bool isHole;
         punchedLandPolytree.PickDeepestPolynode(query.XY.LossyToIntVector2(), out pickedNode, out isHole);

         // If query point not in a hole, nearest land point is query point
         if (!isHole) {
            nearestLandPoint = query;
            return false;
         }

         // Else, two cases to consider: nearest point is on an island inside this hole, alternatively
         // and (only if the hole has a contour), nearest point is on the hole contour.
         nearestLandPoint = DoubleVector3.Zero;
         double bestDistance = double.PositiveInfinity;
         if (pickedNode.Contour.Any()) {
            // the hole has a contour; that is, it's a hole inside of a landmass
            var result = GeometryOperations.FindNearestPointXYZOnContour(pickedNode.Contour, query);
            bestDistance = result.Distance;
            nearestLandPoint = result.NearestPoint;
         }

         foreach (var childLandNode in pickedNode.Childs) {
            var result = GeometryOperations.FindNearestPointXYZOnContour(childLandNode.Contour, query);
            if (result.Distance < bestDistance) {
               bestDistance = result.Distance;
               nearestLandPoint = result.NearestPoint;
            }
         }
         return true;
      }
   }

   public class TerrainService {
      private readonly HashSet<Sector> sectors = new HashSet<Sector>();
      private readonly HashSet<Crossover> hackComputedSectorCrossovers = new HashSet<Crossover>();
      private readonly HashSet<TerrainHole> temporaryHoles = new HashSet<TerrainHole>();
      private readonly GameTimeService gameTimeService;
      private int version;
      private TerrainSnapshot cachedSnapshot;

      public TerrainService(GameTimeService gameTimeService) {
         this.gameTimeService = gameTimeService;
      }

      public void AddSector(Sector sector) {
         if (sectors.Add(sector)) {
            version++;
         }
      }

      public void RemoveSector(Sector sector) {
         if (sectors.Remove(sector)) {
            version++;
         }
      }

      public void AddTemporaryHole(TerrainHole hole) {
         if (temporaryHoles.Add(hole)) {
            version++;
         }
      }

      public void RemoveTemporaryHole(TerrainHole hole) {
         if (temporaryHoles.Remove(hole)) {
            version++;
         }
      }

      public TerrainSnapshot BuildSnapshot() {
         if (cachedSnapshot?.Version == version) {
            return cachedSnapshot;
         }

         var sectorToSnapshot = new Dictionary<Sector, SectorSnapshot>();
         var temporaryHolesSnapshot = temporaryHoles.ToList();
         foreach (var sector in sectors) {
            var snapshot = new SectorSnapshot {
               Sector = sector,
               AbsoluteBounds = sector.AbsoluteBounds,
               StaticHolePolygons = sector.StaticHolePolygons,
               TemporaryHoles = temporaryHolesSnapshot
            };
            sectorToSnapshot.Add(sector, snapshot);
         }

         var crossoverToSnapshot = new Dictionary<Crossover, CrossoverSnapshot>();
         foreach (var crossover in hackComputedSectorCrossovers) {
            var snapshot = new CrossoverSnapshot {
               A = sectorToSnapshot[crossover.A],
               B = sectorToSnapshot[crossover.B],
               Segment = crossover.Segment
            };

            List<CrossoverSnapshot> aToBCrossovers;
            if (!snapshot.A.Crossovers.TryGetValue(snapshot.B, out aToBCrossovers)) {
               aToBCrossovers = new List<CrossoverSnapshot>();
               snapshot.A.Crossovers[snapshot.B] = aToBCrossovers;
            }
            aToBCrossovers.Add(snapshot);

            List<CrossoverSnapshot> bToACrossovers;
            if (!snapshot.B.Crossovers.TryGetValue(snapshot.A, out bToACrossovers)) {
               bToACrossovers = new List<CrossoverSnapshot>();
               snapshot.B.Crossovers[snapshot.A] = bToACrossovers;
            }
            bToACrossovers.Add(snapshot);

            crossoverToSnapshot[crossover] = snapshot;
         }

         return cachedSnapshot = new TerrainSnapshot {
            Version = version,
            CrossoverSnapshots = crossoverToSnapshot.Values.ToList(),
            SectorSnapshots = sectorToSnapshot.Values.ToList(),
            TemporaryHoles = temporaryHoles.ToList(),
         };
      }

      // this should be computed automagically at sector snapshot build
      public void HackAddSectorCrossover(Crossover crossover) {
         if (hackComputedSectorCrossovers.Add(crossover)) {
            version++;
         }
      }
   }

   public class TerrainHole {
      public IReadOnlyList<Polygon> Polygons { get; set; }
   }

   public static class TerrainHoleHelpers {
      public static bool ContainsPoint(this TerrainHole terrainHole, double holeDilationRadius, DoubleVector3 point) {
         // Padding so that when flooring the point, we don't accidentally say a point isn't
         // in the hole when in reality, it is. 
         var paddedHoleShapeUnion = PolygonOperations.Offset()
                                                     .Dilate(holeDilationRadius)
                                                     .Include(terrainHole.Polygons)
                                                     .Execute();

         PolyNode node;
         bool isHole;
         paddedHoleShapeUnion.PickDeepestPolynodeGivenHoleShapePolytree(point.XY.LossyToIntVector2(), out node, out isHole);

         // we want land inside the hole-shape-union because we want to know if we're in the hole, not a hole of the hole shape.
         return !isHole;
      }
   }
}
