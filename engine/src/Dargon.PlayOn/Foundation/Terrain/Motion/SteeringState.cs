using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public enum FlockingStatus {
      Disabled = 0,
      EnabledIdle,
      EnabledExecutingRoadmap,
      EnabledInvalidatedRoadmap,
   }
}