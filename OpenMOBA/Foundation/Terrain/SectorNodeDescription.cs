using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain {
   public class TerrainStaticMetadata {
      public Rectangle LocalBoundary;
      public IReadOnlyList<Polygon2> LocalIncludedContours;
      public IReadOnlyList<Polygon2> LocalExcludedContours;
   }

   public class SectorInstanceMetadata {
      public Matrix4x4 WorldTransform = Matrix4x4.Identity;
      public Matrix4x4 WorldTransformInv = Matrix4x4.Identity;
//      public HashSet<DynamicTerrainHole> Holes;

      public int CachedSnapshotVersion;
      public SectorSnapshot CachedSnapshot;
   }

   public class SectorNodeDescription {
      private readonly TerrainService terrainService;

      internal SectorNodeDescription(TerrainService terrainService, TerrainStaticMetadata staticMetadata) {
         this.terrainService = terrainService;
         this.StaticMetadata = staticMetadata;
      }

      // Internals touched by terrain service
      internal int Version;
      internal TerrainStaticMetadata StaticMetadata;
      internal SectorInstanceMetadata InstanceMetadata = new SectorInstanceMetadata();

      // Publics accessible by game logic
      public Matrix4x4 WorldTransform
      {
         get => InstanceMetadata.WorldTransform;
         set {
            if (InstanceMetadata.WorldTransform != value) {
               InstanceMetadata.WorldTransform = value;
               var inverted = Matrix4x4.Invert(WorldTransform, out InstanceMetadata.WorldTransformInv);
               if (!inverted) {
                  throw new InvalidOperationException("Unable to invert transformation matrix!?");
               }
               Version++;
            }
         }
      }

      public bool EnableDebugHighlight { get; set; }

//      public IReadOnlyCollection<DynamicTerrainHole> Holes => InstanceMetadata.Holes;
   }

   /// <summary>
   /// Considered internal to TerrainService
   /// </summary>
   public class DynamicTerrainHoleDescription {
      public IReadOnlyList<Polygon2> Polygons { get; set; }
   }
}