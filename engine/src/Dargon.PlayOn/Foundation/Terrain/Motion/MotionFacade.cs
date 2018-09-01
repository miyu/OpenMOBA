using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class MotionFacade {
      private readonly TerrainFacade terrainFacade;
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly StatisticsCalculator statisticsCalculator;
      private readonly MotionOperations motionOperations;

      public MotionFacade(TerrainFacade terrainFacade, PathfinderCalculator pathfinderCalculator, StatisticsCalculator statisticsCalculator, MotionOperations motionOperations) {
         this.terrainFacade = terrainFacade;
         this.pathfinderCalculator = pathfinderCalculator;
         this.statisticsCalculator = statisticsCalculator;
         this.motionOperations = motionOperations;
      }

      public void SetPathfindingDestination(Entity entity, DoubleVector3 destination) => motionOperations.SetPathfindingDestination(entity, destination);

      public void FixEntityInHole(Entity e) => motionOperations.FixEntityInHole(e);

      public void HandleHoleAdded(DynamicTerrainHoleDescription holeDescription) => throw new NotImplementedException();

      public void ImmobilizeAsBuilding(Entity e) { }

      public void InvalidatePaths() => throw new NotImplementedException();
   }
}
