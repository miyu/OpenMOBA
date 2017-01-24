using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ClipperLib;

namespace OpenMOBA.Foundation.Terrain {
   public class MapConfiguration {
      public Size Size { get; set; }
      public List<Polygon> StaticHolePolygons { get; set; }
   }

   public enum ClipperPointInPolygonResult {
      OutsidePolygon = 0,
      OnPolygon = -1,
      InPolygon = 1
   }

   public class TerrainSnapshot {
      public int Version { get; set; }
      public MapConfiguration MapConfiguration { get; set; }
      public IReadOnlyList<TerrainHole> TemporaryHoles { get; set; }

      private readonly Dictionary<double, PolyTree> dilatedHolesUnionCache = new Dictionary<double, PolyTree>();
      private readonly Dictionary<double, PolyTree> punchedLandCache = new Dictionary<double, PolyTree>();
      private readonly Dictionary<double, VisibilityGraph> visibilityGraphCache = new Dictionary<double, VisibilityGraph>();
      private readonly Dictionary<double, Triangulation> triangulationCache = new Dictionary<double, Triangulation>();

      public PolyTree ComputeDilatedHolesUnion(double holeDilationRadius) {
         PolyTree dilatedHolesUnion;
         if (!dilatedHolesUnionCache.TryGetValue(holeDilationRadius, out dilatedHolesUnion)) {
            dilatedHolesUnion = PolygonOperations.Offset()
                                                 .Include(MapConfiguration.StaticHolePolygons)
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
            var landPoly = Polygon.CreateRect(
               (int)holeDilationRadius,
               (int)holeDilationRadius,
               (int)(MapConfiguration.Size.Width - 2 * holeDilationRadius),
               (int)(MapConfiguration.Size.Height - 2 * holeDilationRadius));
            var dilatedHolesUnion = ComputeDilatedHolesUnion(holeDilationRadius);
            punchedLand = PolygonOperations.Punch()
                                           .Include(landPoly)
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

   public static class TerrainSnapshotQueryOperations {
      public static bool IsInHole(this TerrainSnapshot terrainSnapshot, double holeDilationRadius, IntVector2 query) {
         var punchedLandPolytree = terrainSnapshot.ComputePunchedLand(holeDilationRadius);
         punchedLandPolytree.AssertIsContourlessRootHolePunchResult();

         PolyNode pickedNode;
         bool isHole;
         punchedLandPolytree.PickDeepestPolynode(query.ToClipperPoint(), out pickedNode, out isHole);

         return isHole;
      }
      
      /// <summary>
      /// Note: Containment definition varies by hole vs terrain: Containment for holes
      /// does not include the hole edge, containment for terrain includes the terrain edge.
      /// This is important, else e.g. knockback + terrain push placing an entity on an edge
      /// would potentially infinite loop.
      /// </summary>
      public static bool FindNearestLandPointAndIsInHole(this TerrainSnapshot terrainSnapshot, double holeDilationRadius, IntVector2 query, out IntVector2 nearestLandPoint) {
         var punchedLandPolytree = terrainSnapshot.ComputePunchedLand(holeDilationRadius);
         punchedLandPolytree.AssertIsContourlessRootHolePunchResult();

         PolyNode pickedNode;
         bool isHole;
         punchedLandPolytree.PickDeepestPolynode(query.ToClipperPoint(), out pickedNode, out isHole);

         // If query point not in a hole, nearest land point is query point
         if (!isHole) {
            nearestLandPoint = query;
            return false;
         }

         // Else, two cases to consider: nearest point is on an island inside this hole, alternatively
         // and (only if the hole has a contour), nearest point is on the hole contour.
         nearestLandPoint = IntVector2.Zero;
         float bestDistance = float.PositiveInfinity;
         if (pickedNode.Contour.Any()) {
            // the hole has a contour; that is, it's a hole inside of a landmass
            var result = GeometryOperations.FindNearestPoint(pickedNode.Contour, query);
            bestDistance = result.Distance;
            nearestLandPoint = result.NearestPoint;
         }

         foreach (var childLandNode in pickedNode.Childs) {
            var result = GeometryOperations.FindNearestPoint(childLandNode.Contour, query);
            if (result.Distance < bestDistance) {
               bestDistance = result.Distance;
               nearestLandPoint = result.NearestPoint;
            }
         }
         return true;
      }
   }

   public class TerrainService {
      private readonly HashSet<TerrainHole> temporaryHoles = new HashSet<TerrainHole>();
      private readonly MapConfiguration mapConfiguration;
      private readonly GameTimeService gameTimeService;
      private int version;
      private TerrainSnapshot cachedSnapshot;

      public TerrainService(MapConfiguration mapConfiguration, GameTimeService gameTimeService) {
         this.mapConfiguration = mapConfiguration;
         this.gameTimeService = gameTimeService;
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

         return cachedSnapshot = new TerrainSnapshot {
            TemporaryHoles = temporaryHoles.ToList(),
            MapConfiguration = mapConfiguration,
            Version = version
         };
      }
   }

   public class TerrainHole {
      public IReadOnlyList<Polygon> Polygons { get; set; }
   }

   public static class TerrainHoleHelpers {
      public static bool ContainsPoint(this TerrainHole terrainHole, double holeDilationRadius, DoubleVector2 point) {
         // Padding so that when flooring the point, we don't accidentally say a point isn't
         // in the hole when in reality, it is. 
         var paddedHoleShapeUnion = PolygonOperations.Offset()
                                                     .Dilate(holeDilationRadius)
                                                     .Include(terrainHole.Polygons)
                                                     .Execute();

         PolyNode node;
         bool isHole;
         paddedHoleShapeUnion.PickDeepestPolynodeGivenHoleShapePolytree(point.LossyToIntVector2().ToClipperPoint(), out node, out isHole);

         // we want land inside the hole-shape-union because we want to know if we're in the hole, not a hole of the hole shape.
         return !isHole;
      }
   }
}
