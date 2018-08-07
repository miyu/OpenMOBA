using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;

namespace Dargon.PlayOn.Foundation {
   public class GameLogicFacade {
      private readonly MotionSystem motionSystem;
      private readonly TerrainFacade terrainFacade;

      public GameLogicFacade(TerrainFacade terrainFacade, MotionSystem motionSystem) {
         this.terrainFacade = terrainFacade;
         this.motionSystem = motionSystem;
      }

      public void AddTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainFacade.AddTemporaryHoleDescription(holeDescription);
         // todo: can optimize to only invalidate paths intersecting hole.
         motionSystem.HandleHoleAdded(holeDescription);
      }

      public void RemoveTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainFacade.RemoveTemporaryHoleDescription(holeDescription);
         motionSystem.InvalidatePaths();
      }
   }
}