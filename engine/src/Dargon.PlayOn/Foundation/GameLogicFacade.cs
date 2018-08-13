using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Foundation.Terrain.Motion;

namespace Dargon.PlayOn.Foundation {
   public class GameLogicFacade {
      private readonly TerrainFacade terrainFacade;
      private readonly MotionFacade motionFacade;

      public GameLogicFacade(TerrainFacade terrainFacade, MotionFacade motionFacade) {
         this.terrainFacade = terrainFacade;
         this.motionFacade = motionFacade;
      }

      public void AddTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainFacade.AddTemporaryHoleDescription(holeDescription);
         // todo: can optimize to only invalidate paths intersecting hole.
         motionFacade.HandleHoleAdded(holeDescription);
      }

      public void RemoveTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainFacade.RemoveTemporaryHoleDescription(holeDescription);
         motionFacade.InvalidatePaths();
      }
   }
}