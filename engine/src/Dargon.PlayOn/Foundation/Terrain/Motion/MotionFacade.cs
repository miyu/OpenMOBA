using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class MotionFacade {
      private readonly TerrainFacade terrainFacade;
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly StatisticsCalculator statisticsCalculator;
      private readonly MotionOperations motionOperations;
      private readonly TriangulationWalker3D triangulationWalker3D;

      public MotionFacade(TerrainFacade terrainFacade, PathfinderCalculator pathfinderCalculator, StatisticsCalculator statisticsCalculator, MotionOperations motionOperations, TriangulationWalker3D triangulationWalker3D) {
         this.terrainFacade = terrainFacade;
         this.pathfinderCalculator = pathfinderCalculator;
         this.statisticsCalculator = statisticsCalculator;
         this.motionOperations = motionOperations;
         this.triangulationWalker3D = triangulationWalker3D;
      }

      public void SetPathfindingDestination(Entity entity, DoubleVector3 destination) => motionOperations.SetPathfindingDestination(entity, destination);

      public void FixEntityInHole(Entity e) => motionOperations.FixEntityInHole(e);

      public void HandleHoleAdded(DynamicTerrainHoleDescription holeDescription) => throw new NotImplementedException();

      public void ImmobilizeAsBuilding(Entity e) { }

      public double VectorWalk(Entity e, DoubleVector3 direction, double distance) {
         var mc = e.MotionComponent;
         var res = triangulationWalker3D.WalkTriangulation(mc.Internals.Localization, direction, distance);
         mc.Internals.Localization = res.Item1;
         mc.Internals.Pose.WorldPosition = res.Item1.TerrainOverlayNetworkNode.SectorNodeDescription.LocalToWorld(mc.Internals.Localization.LocalPosition);
         return res.Item2;
      }

      public void InvalidatePaths() => throw new NotImplementedException();
   }
}
