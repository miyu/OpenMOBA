using System;
using System.Numerics;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.CompilationResults.Overlay {
   public static class TerrainOverlayNetworkExtensions {
      public static bool TryFindTerrainOverlayNode(this TerrainOverlayNetwork terrainOverlayNetwork, DoubleVector3 p, out TerrainOverlayNetworkNode result) {
         return TryFindTerrainOverlayNode(terrainOverlayNetwork, p, out result, out _);
      }

      public static bool TryFindTerrainOverlayNode(this TerrainOverlayNetwork terrainOverlayNetwork, DoubleVector3 p, out TerrainOverlayNetworkNode result, out DoubleVector3 pLocal) {
         var res = TryFindTerrainOverlayNode(terrainOverlayNetwork, p.ToDotNetVector(), out result, out var pLocalV3);
         pLocal = pLocalV3.ToOpenMobaVector();
         return res;
      }

      public static bool TryFindTerrainOverlayNode(this TerrainOverlayNetwork terrainOverlayNetwork, Vector3 p, out TerrainOverlayNetworkNode result) {
         return TryFindTerrainOverlayNode(terrainOverlayNetwork, p, out result, out _);
      }

      public static bool TryFindTerrainOverlayNode(this TerrainOverlayNetwork terrainOverlayNetwork, Vector3 p, out TerrainOverlayNetworkNode result, out Vector3 pLocal) {
         foreach (var bvhNode in terrainOverlayNetwork.NodeBvh.FindIntersectingLeaves(p.ToOpenMobaVector()))
            for (var i = bvhNode.StartIndexInclusive; i < bvhNode.EndIndexExclusive; i++) {
               var node = bvhNode.Values[i];

               pLocal = Vector3.Transform(p, node.SectorNodeDescription.WorldTransformInv);
               var pLocalXy = new IntVector2((int)pLocal.X, (int)pLocal.Y); // todo: correctness issues

               if (!node.LandPolyNode.PointInLandPolygonNonrecursive(pLocalXy)) continue;
               if (Math.Abs(pLocal.Z) > 1E-3f) continue;

               result = node;
               return true;
            }
         result = null;
         pLocal = default(Vector3);
         return false;
      }

      public static bool Contains(this TerrainOverlayNetworkNode node, IntVector3 world, out DoubleVector3 local) {
         local = node.SectorNodeDescription.WorldToLocal(world);
         return Math.Abs(local.Z) <= 1E-3f &&
                node.LandPolyNode.PointInLandPolygonNonrecursive(local.XY.LossyToIntVector2());
      }

      public static bool Contains(this TerrainOverlayNetworkNode node, DoubleVector3 world, out DoubleVector3 local) {
         local = node.SectorNodeDescription.WorldToLocal(world);
         return Math.Abs(local.Z) <= 1E-3f &&
                node.LandPolyNode.PointInLandPolygonNonrecursive(local.XY.LossyToIntVector2());
      }

      public static DoubleVector3 WorldToLocal(this SectorNodeDescription snd, IntVector3 world) {
         return Vector3.Transform(world.ToDotNetVector(), snd.WorldTransformInv).ToOpenMobaVector();
      }

      public static DoubleVector3 WorldToLocal(this SectorNodeDescription snd, DoubleVector3 world) {
         return Vector3.Transform(world.ToDotNetVector(), snd.WorldTransformInv).ToOpenMobaVector();
      }

      public static DoubleVector3 WorldToLocalNormal(this SectorNodeDescription snd, IntVector3 world) {
         return Vector3.TransformNormal(world.ToDotNetVector(), snd.WorldTransformInv).ToOpenMobaVector();
      }

      public static DoubleVector3 WorldToLocalNormal(this SectorNodeDescription snd, DoubleVector3 world) {
         return Vector3.TransformNormal(world.ToDotNetVector(), snd.WorldTransformInv).ToOpenMobaVector();
      }

      public static DoubleVector3 LocalToWorld(this SectorNodeDescription snd, IntVector2 local) {
         return Vector3.Transform(new Vector3(local.X, local.Y, 0), snd.WorldTransform).ToOpenMobaVector();
      }

      public static DoubleVector3 LocalToWorld(this SectorNodeDescription snd, DoubleVector2 local) {
         return Vector3.Transform(new Vector3((float)local.X, (float)local.Y, 0), snd.WorldTransform).ToOpenMobaVector();
      }

      public static DoubleVector3 LocalToWorldNormal(this SectorNodeDescription snd, IntVector3 local) {
         return Vector3.TransformNormal(new Vector3(local.X, local.Y, local.Z), snd.WorldTransform).ToOpenMobaVector();
      }

      public static DoubleVector3 LocalToWorldNormal(this SectorNodeDescription snd, DoubleVector3 local, float z = 0) {
         return Vector3.TransformNormal(new Vector3((float)local.X, (float)local.Y, (float)local.Z), snd.WorldTransform).ToOpenMobaVector();
      }
   }
}
