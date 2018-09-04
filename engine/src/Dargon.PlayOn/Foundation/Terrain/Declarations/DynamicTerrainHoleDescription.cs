using System;
using System.Collections.Generic;
using System.Numerics;
using Dargon.Commons;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Declarations {
   /// <summary>
   /// Considered internal to TerrainService
   /// </summary>
   public class DynamicTerrainHoleDescription {
      private readonly TerrainFacade terrainFacade;

      internal DynamicTerrainHoleDescription(TerrainFacade terrainFacade, IHoleStaticMetadata staticMetadata) {
         this.terrainFacade = terrainFacade;
         this.StaticMetadata = staticMetadata;

         RecomputeWorldAABB();
      }

      // Internals touched by terrain service
      internal int Version;

      public IHoleStaticMetadata StaticMetadata;
      internal HoleInstanceMetadata InstanceMetadata = new HoleInstanceMetadata();

      // Publics accessible by game logic
      public Matrix4x4 WorldTransform {
         get => InstanceMetadata.WorldTransform;
         set {
            if (InstanceMetadata.WorldTransform == value) return;

            InstanceMetadata.WorldTransform = value;
            var inverted = Matrix4x4.Invert(WorldTransform, out InstanceMetadata.WorldTransformInv);
            Assert.IsTrue(inverted, "Unable to invert transformation matrix!?");

            RecomputeWorldAABB();
            Version++;
         }
      }

      public bool EnableHolePathfindingWaypoints {
         get => InstanceMetadata.EnableHolePathfindingWaypoints;
         set {
            if (InstanceMetadata.EnableHolePathfindingWaypoints == value) return;
            InstanceMetadata.EnableHolePathfindingWaypoints = value;
            Version++;
         }
      }

      private void RecomputeWorldAABB() {
         InstanceMetadata.WorldAABB = StaticMetadata.ComputeWorldAABB(InstanceMetadata.WorldTransform);
      }

      public Matrix4x4 WorldTransformInv => InstanceMetadata.WorldTransformInv;

      public bool EnableDebugHighlight { get; set; }

      public AxisAlignedBoundingBox3 WorldBounds => InstanceMetadata.WorldAABB;

      public void EnhanceLocalGeometryJob(SectorNodeDescription sectorNodeDescription, ref LocalGeometryJob localGeometryRenderJob) {
         if (!WorldBounds.Intersects(sectorNodeDescription.WorldBounds)) {
            return;
         }

         IReadOnlyList<Polygon2> projectedHoleIncludedContours, projectedHoleExcludedContours;
         if (!StaticMetadata.TryProjectOnto(InstanceMetadata, sectorNodeDescription, out projectedHoleIncludedContours, out projectedHoleExcludedContours)) {
            return;
         }

         localGeometryRenderJob.DynamicHoles[(this, Version)] = (projectedHoleIncludedContours, projectedHoleExcludedContours);
      }
   }
}