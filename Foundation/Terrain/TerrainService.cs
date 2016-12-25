using System.Collections.Generic;
using System.Drawing;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain
{
    public class MapConfiguration
    {
        public Size Size { get; set; }
        public List<Polygon> StaticHolePolygons { get; set; }
    }

    public class TerrainSnapshot
    {
        public int Version { get; }
    }

    public class TerrainService
    {
        private readonly HashSet<TerrainHole> temporaryHoles = new HashSet<TerrainHole>();
        private readonly MapConfiguration mapConfiguration;

        public TerrainService(MapConfiguration mapConfiguration, GameTimeService gameTimeService)
        {
            this.mapConfiguration = mapConfiguration;
        }

        public void AddTemporaryHole(TerrainHole hole) => temporaryHoles.Add(hole);

        public void RemoveTemporaryHole(TerrainHole hole) => temporaryHoles.Remove(hole);

        public TerrainSnapshot BuildSnapshot(int characterRadius)
        {
            return null;
        }
    }

    public class TerrainHole
    {
        public IReadOnlyList<Polygon> Polygons { get; set; }
    }

    public class Blockade
    {
        public GameTime EndTime { get; set; }
    }
}
