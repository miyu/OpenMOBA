using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;

namespace Dargon.PlayOn.Foundation {
   public class GameLogicFacade {
      private readonly MovementSystem movementSystem;
      private readonly TerrainFacade terrainFacade;

      public GameLogicFacade(TerrainFacade terrainFacade, MovementSystem movementSystem) {
         this.terrainFacade = terrainFacade;
         this.movementSystem = movementSystem;
      }

      public void AddTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainFacade.AddTemporaryHoleDescription(holeDescription);
         // todo: can optimize to only invalidate paths intersecting hole.
         movementSystem.HandleHoleAdded(holeDescription);
      }

      public void RemoveTemporaryHole(DynamicTerrainHoleDescription holeDescription) {
         terrainFacade.RemoveTemporaryHoleDescription(holeDescription);
         movementSystem.InvalidatePaths();
      }
   }
}