using System;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace OpenMOBA.Foundation.Terrain {
   public interface ITerrainSnapshotCompiler {
      TerrainSnapshot CompileSnapshot();
   }

   public struct LocalGeometryJob {
      public readonly TerrainStaticMetadata TerrainStaticMetadata;
      public readonly HashSet<IntLineSegment2> CrossoverSegments;

      public LocalGeometryJob(TerrainStaticMetadata terrainStaticMetadata) {
         TerrainStaticMetadata = terrainStaticMetadata;
         CrossoverSegments = new HashSet<IntLineSegment2>();
      }

      public override bool Equals(object obj) {
         return obj is LocalGeometryJob other && Equals(other);
      }

      public bool Equals(LocalGeometryJob other) {
         return TerrainStaticMetadata == other.TerrainStaticMetadata &&
                CrossoverSegments.SetEquals(other.CrossoverSegments);
      }

      public override int GetHashCode() {
         var hash = TerrainStaticMetadata.GetHashCode() * 397;
         return CrossoverSegments.Aggregate(hash, (current, x) => current ^ x.GetHashCode());
      }
   }

   public class TerrainSnapshotCompiler : ITerrainSnapshotCompiler {
      private readonly SectorGraphDescriptionStore descriptionStore;
      private Dictionary<LocalGeometryJob, LocalGeometryViewManager> previousLocalGeometryViewManagers = new Dictionary<LocalGeometryJob, LocalGeometryViewManager>();
      private TerrainSnapshot cachedSnapshot;

      public TerrainSnapshotCompiler(SectorGraphDescriptionStore descriptionStore) {
         this.descriptionStore = descriptionStore;
      }

      public TerrainSnapshot CompileSnapshot() {
         if (cachedSnapshot?.Version == descriptionStore.Version) return cachedSnapshot;

         var nodeDescriptions = descriptionStore.EnumerateSectorNodeDescriptions().ToHashSet();
         var edgeDescriptions = descriptionStore.EnumerateSectorEdgeDescriptions().ToHashSet();

         //----------------------------------------------------------------------------------------
         // Plan Local Geometry Jobs
         //----------------------------------------------------------------------------------------
         var edgeDescriptionsByNodeDescription = edgeDescriptions.ToLookup(edge => edge.Source);

         var localGeometryRenderJobByNodeDescription = new Dictionary<SectorNodeDescription, LocalGeometryJob>();
         foreach (var sectorNodeDescription in nodeDescriptions) {
            var localGeometryRenderJob = new LocalGeometryJob(sectorNodeDescription.StaticMetadata);
            foreach (var edgeDescription in edgeDescriptionsByNodeDescription[sectorNodeDescription]) {
               edgeDescription.EnhanceLocalGeometryJob(ref localGeometryRenderJob);
            }
            localGeometryRenderJobByNodeDescription.Add(sectorNodeDescription, localGeometryRenderJob);
         }

         var localGeometryPreviewJobsByRenderJob = localGeometryRenderJobByNodeDescription.Values.ToDictionary(
            localGeometryJob => localGeometryJob,
            localGeometryJob => new LocalGeometryJob(localGeometryJob.TerrainStaticMetadata));


         // TODO: Optimization: Pathfind on 'known' hole-less geometry.
         // If a 'dirty' uncalculated snapshot is in the final path, calculate and redo pathfind.

         //----------------------------------------------------------------------------------------
         // Local Geometry Jobs => Local Geometry View Managers
         // Which are able to efficiently find paths from one point to another, but don't have the
         // concept of a 'crossover' (though they know to negate character radius at certain segs)
         // Support adding 'endpoints' which may be labeled + looking up all endpoints of a label.
         //----------------------------------------------------------------------------------------
         var localGeometryPreviewJobs = localGeometryPreviewJobsByRenderJob.Values.ToHashSet();
         var localGeometryPreviewJobViewManagers = localGeometryPreviewJobs.ToDictionary(
            job => job,
            job => previousLocalGeometryViewManagers.GetValueOrDefault(job)
                   ?? new LocalGeometryViewManager(job));

         var localGeometryRenderJobs = localGeometryRenderJobByNodeDescription.Values.ToHashSet();
         var localGeometryRenderJobViewManagers = localGeometryRenderJobs.ToDictionary(
            job => job,
            job => previousLocalGeometryViewManagers.GetValueOrDefault(job)
                   ?? localGeometryPreviewJobViewManagers.GetValueOrDefault(job)
                   ?? new LocalGeometryViewManager(job, localGeometryPreviewJobViewManagers[localGeometryPreviewJobsByRenderJob[job]]));

         previousLocalGeometryViewManagers = new Dictionary<LocalGeometryJob, LocalGeometryViewManager>();
         foreach (var kvp in localGeometryPreviewJobViewManagers.Concat(localGeometryRenderJobViewManagers)) {
            previousLocalGeometryViewManagers[kvp.Key] = kvp.Value;
         }

         //----------------------------------------------------------------------------------------
         // Return Terrain Overlay Network Manager
         //----------------------------------------------------------------------------------------
         var localGeometryViewManagerBySectorNodeDescription = nodeDescriptions.ToDictionary(
            nodeDescription => nodeDescription,
            nodeDescription => localGeometryRenderJobViewManagers[localGeometryRenderJobByNodeDescription[nodeDescription]]);
         var terrainOverlayNetworkManager = new TerrainOverlayNetworkManager(localGeometryViewManagerBySectorNodeDescription, edgeDescriptions);

         //----------------------------------------------------------------------------------------
         // Build Preview Terrain Overlay Network
         // Which has the concept of 'crossovers' and nodes
         //----------------------------------------------------------------------------------------
         // var nodes = descriptionStore
         //    .EnumerateSectorNodeDescriptions()
         //    .Select(nodeDescription => new TerrainNode(
         //       nodeDescription,
         //       localGeometryRenderJobViewManagers[localGeometryRenderJobByNodeDescription[nodeDescription]]
         //          .PreviewViewManager
         //    ));


         //         var sectorToSnapshot = new Dictionary<SectorNodeDescription, SectorSnapshot>();
         //         foreach (var sector in descriptionStore.EnumerateSectorNodeDescriptions()) {
         //            var snapshot = new SectorSnapshot {
         //               SectorNodeDescription = sector,
         //               WorldTransform = sector.InstanceMetadata.WorldTransform,
         //               WorldTransformInv = sector.InstanceMetadata.WorldTransformInv,
         //            };
         //            sectorToSnapshot.Add(sector, snapshot);
         //         }
         //
         //         foreach (var edgeDescription in descriptionStore.EnumerateSectorEdgeDescriptions()) {
         //            edgeDescription.
         ////            sectorToSnapshot[edge.Source]..Add(edge);
         //         }
         //
         //         return cachedSnapshot = new TerrainSnapshot {
         //            Version = descriptionStore.Version,
         //            SectorSnapshots = sectorToSnapshot.Values.ToList(),
         //            TemporaryHoles = new DynamicTerrainHoleDescription[0]
         //         };
      }
   }
}
