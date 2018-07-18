using System.Collections.Generic;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Declarations;

namespace Dargon.PlayOn.Foundation.Terrain.CompilationResults {
   public class TerrainSnapshot {
      public int Version { get; set; }
      public IReadOnlyList<SectorNodeDescription> NodeDescriptions { get; set; }
      public IReadOnlyList<SectorEdgeDescription> EdgeDescriptions { get; set; }
      public TerrainOverlayNetworkManager OverlayNetworkManager { get; set; }
   }
}
