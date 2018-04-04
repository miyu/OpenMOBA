using System;
using System.Numerics;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.CompilationResults.Overlay {
   public static class TerrainOverlayNetworkExtensions {
      public static bool TryFindTerrainOverlayNode(this TerrainOverlayNetwork terrainOverlayNetwork, Vector3 p, out TerrainOverlayNetworkNode result) {
         foreach (var bvhNode in terrainOverlayNetwork.NodeBvh.FindIntersectingLeaves(p.ToOpenMobaVector()))
            for (var i = bvhNode.StartIndexInclusive; i < bvhNode.EndIndexExclusive; i++) {
               var node = bvhNode.Values[i];

               var pLocal = Vector3.Transform(p, node.SectorNodeDescription.WorldTransformInv);
               var pLocalXy = new IntVector2((int)pLocal.X, (int)pLocal.Y); // todo: correctness issues

               if (!node.LandPolyNode.PointInLandPolygonNonrecursive(pLocalXy)) continue;

               if (Math.Abs(pLocal.Z) > 1E-3f) continue;

               result = node;
               return true;
            }
         result = null;
         return false;
      }
   }
}
