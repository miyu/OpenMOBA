using System.Collections.Generic;
using OpenMOBA.Foundation.Terrain.Visibility;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<SectorNodeDescription> NodeDescriptions { get; set; }
      public IReadOnlyList<SectorEdgeDescription> EdgeDescriptions { get; set; }
      public TerrainOverlayNetworkManager OverlayNetworkManager { get; set; }
   }
}
