using System.Collections.Generic;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Foundation.Terrain.Declarations;

namespace OpenMOBA.Foundation.Terrain.CompilationResults {
   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<SectorNodeDescription> NodeDescriptions { get; set; }
      public IReadOnlyList<SectorEdgeDescription> EdgeDescriptions { get; set; }
      public TerrainOverlayNetworkManager OverlayNetworkManager { get; set; }
   }
}
