using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using ClipperLib;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<CrossoverSnapshot> CrossoverSnapshots { get; set; }
      public IReadOnlyList<SectorSnapshot> SectorSnapshots { get; set; }
      public IReadOnlyList<DynamicTerrainHole> TemporaryHoles { get; set; }
   }

   public class CrossoverSnapshot {
      public SectorSnapshot A { get; set; }
      public SectorSnapshot B { get; set; }
      public IntLineSegment3 Segment { get; set; }
   }

   public class SectorSnapshot {
      // Inputs
      public Sector Sector { get; set; }
      public TerrainStaticMetadata StaticMetadata => Sector.StaticMetadata;
      public Matrix4x4 WorldTransform;
      public Matrix4x4 WorldTransformInv;

      public IReadOnlyList<DynamicTerrainHole> TemporaryHoles { get; set; }
      public Dictionary<SectorSnapshot, List<CrossoverSnapshot>> Crossovers { get; set; } = new Dictionary<SectorSnapshot, List<CrossoverSnapshot>>();
      public Dictionary<Tuple<CrossoverSnapshot, CrossoverSnapshot>, List<IntLineSegment2>> BarriersBetweenCrossovers { get; set; } = new Dictionary<Tuple<CrossoverSnapshot, CrossoverSnapshot>, List<IntLineSegment2>>();

      // Outputs
      private readonly Dictionary<double, PolyTree> dilatedHolesUnionCache = new Dictionary<double, PolyTree>();
      private readonly Dictionary<double, Dictionary<DoubleVector2, VisibilityPolygonBuilder>> lineOfSightCaches = new Dictionary<double, Dictionary<DoubleVector2, VisibilityPolygonBuilder>>();
      private readonly Dictionary<double, PolyTree> punchedLandCache = new Dictionary<double, PolyTree>();
      private readonly Dictionary<double, Triangulation> triangulationCache = new Dictionary<double, Triangulation>();
      private readonly Dictionary<double, VisibilityGraph> visibilityGraphCache = new Dictionary<double, VisibilityGraph>();

      private DoubleVector2 WorldToLocal(IntVector3 p) => WorldToLocal(p.ToDoubleVector3());
      private DoubleVector2 WorldToLocal(DoubleVector3 p) => Vector3.Transform(p.ToDotNetVector(), WorldTransformInv).ToOpenMobaVector().XY;

      public PolyTree ComputeDilatedHolesUnion(double holeDilationRadius) {
         PolyTree dilatedHolesUnion;
         if (!dilatedHolesUnionCache.TryGetValue(holeDilationRadius, out dilatedHolesUnion)) {
            dilatedHolesUnion = PolygonOperations.Offset()
                                                 .Include(StaticMetadata.LocalExcludedContours)
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
            var landPoly = PolygonOperations.Offset()
                                            .Include(StaticMetadata.LocalIncludedContours)
                                            .Erode(holeDilationRadius)
                                            .Execute().FlattenToPolygons();

            var crossoverLandPolys = new List<Polygon2>();
            foreach (var kvp in Crossovers) {
               var crossoverErosionFactor = (int)Math.Ceiling(holeDilationRadius * 2);
               var crossoverDilationFactor = crossoverErosionFactor / 2 + 2;
               foreach (var crossover in kvp.Value) {
                  var segment = crossover.Segment;
                  var a = WorldToLocal(segment.First);
                  var b = WorldToLocal(segment.Second);
                  Console.WriteLine("====");
                  Console.WriteLine(segment.First + " " + segment.Second);
                  Console.WriteLine(a + " " + b);
                  var aToB = a.To(b);
                  var aToBMag = aToB.Norm2D();
                  if (aToBMag <= 2 * holeDilationRadius) continue;

                  var shrink = (aToB * crossoverErosionFactor / aToBMag).LossyToIntVector2();
                  //2 * ((aToB * crossoverDilationFactor).ToDoubleVector3() / aToB.Norm2F()).LossyToIntVector3();
                  var crossoverPolyTree = PolylineOperations.ExtrudePolygon(new[] {
                     a.LossyToIntVector2() + shrink,
                     b.LossyToIntVector2() - shrink
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

      public void ComputeCrossoverVisibilities(double holeDilationRadius) {
         var visibilityGraph = ComputeVisibilityGraph(holeDilationRadius);

         var crossovers = Crossovers.Values.SelectMany(cs => cs).ToList();
         var locations = crossovers.Select(c => new IntLineSegment2(WorldToLocal(c.Segment.First).LossyToIntVector2(), WorldToLocal(c.Segment.Second).LossyToIntVector2())).ToList();
         for (var i = 0; i < crossovers.Count; i++) {
            var ca = crossovers[i];
            var a = locations[i];
            for (var j = i + 1; j < crossovers.Count; j++) {
               var cb = crossovers[j];
               var b = locations[j];

               var hull = GeometryOperations.ConvexHull(new[] { a.First, a.Second, b.First, b.Second });
               foreach (var holeContour in StaticMetadata.LocalExcludedContours) {
                  Trace.Assert(holeContour.IsClosed);

                  var contourPoints = holeContour.Points;
                  var interiorClockness = Clockness.Neither;
                  for (i = 0; i < contourPoints.Count - 1; i++) {
//                     var clockness = GeometryOperations.Clockness(contourPoints[i]
                  }
               }
            }
         }
      }

      public VisibilityPolygonBuilder ComputeVisibilityPolygon(DoubleVector2 position, double holeDilationRadius) {
         Dictionary<DoubleVector2, VisibilityPolygonBuilder> lineOfSightCache;
         if (!lineOfSightCaches.TryGetValue(holeDilationRadius, out lineOfSightCache)) {
            lineOfSightCache = new Dictionary<DoubleVector2, VisibilityPolygonBuilder>();
            lineOfSightCaches[holeDilationRadius] = lineOfSightCache;
         }

         VisibilityPolygonBuilder lineOfSight;
         if (!lineOfSightCache.TryGetValue(position, out lineOfSight)) {
            var barriers = ComputeVisibilityGraph(holeDilationRadius).Barriers;
            lineOfSight = new VisibilityPolygonBuilder(position);
            foreach (var barrier in barriers) lineOfSight.Insert(barrier);
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
}
