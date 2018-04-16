using System.Collections.Generic;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation {
   public class PathfinderResultContext {
      public readonly (TerrainOverlayNetworkNode, IntVector2) Source;
      public readonly (TerrainOverlayNetworkNode, IntVector2)[] Destinations;
      internal readonly Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge, float)> Predecessors;
      internal readonly ExposedArrayList<PathLink> SourceOptimalLinkToCrossovers;
      internal readonly ExposedArrayList<PathLink>[] DestinationOptimalLinkToCrossoversByDestinationIndex;

      private readonly MotionRoadmap[] roadmapCache;

      public PathfinderResultContext((TerrainOverlayNetworkNode, IntVector2) source, (TerrainOverlayNetworkNode, IntVector2)[] destinations, Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge, float)> predecessors, ExposedArrayList<PathLink> sourceOptimalLinkToCrossovers, ExposedArrayList<PathLink>[] destinationOptimalLinkToCrossoversByDestinationIndex) {
         Source = source;
         Destinations = destinations;
         Predecessors = predecessors;
         SourceOptimalLinkToCrossovers = sourceOptimalLinkToCrossovers;
         DestinationOptimalLinkToCrossoversByDestinationIndex = destinationOptimalLinkToCrossoversByDestinationIndex;

         roadmapCache = new MotionRoadmap[destinations.Length];
      }

      public MotionRoadmap ComputeRoadmap(int destinationIndex) =>
         roadmapCache[destinationIndex] ?? (roadmapCache[destinationIndex] =
            PathfinderCalculator.Backtrack(
               Source.Item1, Source.Item2,
               Destinations[destinationIndex].Item1, Destinations[destinationIndex].Item2,
               Predecessors,
               -destinationIndex - 1,
               SourceOptimalLinkToCrossovers,
               DestinationOptimalLinkToCrossoversByDestinationIndex[destinationIndex]));
   }
}