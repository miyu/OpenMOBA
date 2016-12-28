using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain {
   public class MapConfiguration {
      public Size Size { get; set; }
      public List<Polygon> StaticHolePolygons { get; set; }
   }

   public class TerrainSnapshot {
      public int Version { get; set; }
      public MapConfiguration MapConfiguration { get; set; }
      public IReadOnlyList<TerrainHole> TemporaryHoles { get; set; }

      private readonly Dictionary<double, VisibilityGraph> visibilityGraphCache = new Dictionary<double, VisibilityGraph>();

      public VisibilityGraph ComputeVisibilityGraph(double characterRadius) {
         if (double.IsNaN(characterRadius) || double.IsInfinity(characterRadius)) {
            throw new ArgumentException(nameof(characterRadius));
         }
         VisibilityGraph visibilityGraph;
         if (!visibilityGraphCache.TryGetValue(characterRadius, out visibilityGraph)) {
            visibilityGraph = VisibilityGraphOperations.CreateVisibilityGraph(
               MapConfiguration.Size,
               PolygonOperations.Offset()
                                .Include(MapConfiguration.StaticHolePolygons)
                                .Include(TemporaryHoles.SelectMany(h => h.Polygons))
                                .Dilate(15)
                                .Execute()
                                .FlattenToPolygons());
            visibilityGraphCache[characterRadius] = visibilityGraph;
         }
         return visibilityGraph;
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

   public class Blockade {
      public GameTime EndTime { get; set; }
   }
}
