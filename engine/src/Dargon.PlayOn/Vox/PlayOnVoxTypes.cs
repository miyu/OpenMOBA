using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;
using Dargon.Vox;

namespace Dargon.PlayOn.Vox {
   public class PlayOnVoxTypes : VoxTypes {
      public PlayOnVoxTypes() : base(10000) {
         int i = 0;
         Register<IntVector2>(i++);
         Register<Polygon2>(i++);
         Register<TerrainStaticMetadata>(i++);
      }
   }
}
