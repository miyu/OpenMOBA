using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public static class TerrainOverlayNetworkExtensions {
      public static bool TryFindTerrainOverlayNode(this TerrainOverlayNetwork terrainOverlayNetwork, Vector3 p, out TerrainOverlayNetworkNode result) {
         foreach (var node in terrainOverlayNetwork.TerrainNodes) {
            var pLocal = Vector3.Transform(p, node.SectorNodeDescription.WorldTransformInv);
            var pLocalXy = new IntVector2((int)pLocal.X, (int)pLocal.Y); // todo: correctness issues

            if (!node.LandPolyNode.PointInLandPolygonNonrecursive(pLocalXy)) {
               continue;
            }

            if (Math.Abs(pLocal.Z) > 1E-3f) {
               continue;
            }

            result = node;
            return true;
         }
         result = null;
         return false;
      }
   }
}
