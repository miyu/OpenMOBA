using System;
using System.Diagnostics;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Motion;
using Dargon.PlayOn.Geometry;

#if use_fixed
using ClipperLib;
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain.CompilationResults {
   public struct Localization {
      public TerrainOverlayNetwork TerrainOverlayNetwork;
      public TerrainOverlayNetworkNode TerrainOverlayNetworkNode;
      public DoubleVector2 LocalPosition;
      public IntVector2 LocalPositionIv2;
      public TriangulationIsland TriangulationIsland;
      public int TriangleIndex;

      public Localization(TerrainOverlayNetwork terrainOverlayNetwork, TerrainOverlayNetworkNode terrainOverlayNetworkNode, DoubleVector2 localPosition, IntVector2 localPositionIv2, TriangulationIsland triangulationIsland, int triangleIndex) {
         TerrainOverlayNetwork = terrainOverlayNetwork;
         TerrainOverlayNetworkNode = terrainOverlayNetworkNode;
         LocalPosition = localPosition;
         LocalPositionIv2 = localPositionIv2;
         TriangulationIsland = triangulationIsland;
         TriangleIndex = triangleIndex;
      }
   }
}