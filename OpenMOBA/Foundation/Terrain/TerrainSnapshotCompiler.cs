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
      public readonly Dictionary<(DynamicTerrainHoleDescription desc, int version), (IReadOnlyList<Polygon2> holeIncludedContours, IReadOnlyList<Polygon2> holeExcludedContours)> DynamicHoles;

      public LocalGeometryJob(TerrainStaticMetadata terrainStaticMetadata, HashSet<IntLineSegment2> crossoverSegments = null) {
         TerrainStaticMetadata = terrainStaticMetadata;
         CrossoverSegments = crossoverSegments ?? new HashSet<IntLineSegment2>();
         DynamicHoles = new Dictionary<(DynamicTerrainHoleDescription, int), (IReadOnlyList<Polygon2> holeIncludedContours, IReadOnlyList<Polygon2> holeExcludedContours)>();
      }

      public override bool Equals(object obj) {
         return obj is LocalGeometryJob other && Equals(other);
      }

      public bool Equals(LocalGeometryJob other) {
         return TerrainStaticMetadata == other.TerrainStaticMetadata &&
                CrossoverSegments.SetEquals(other.CrossoverSegments) &&
                DynamicHoles.Keys.ToHashSet().SetEquals(other.DynamicHoles.Keys);
      }

      public override int GetHashCode() {
         var hash = TerrainStaticMetadata.GetHashCode() * 397;
         hash = CrossoverSegments.Aggregate(hash, (current, x) => current ^ x.GetHashCode());
         hash = DynamicHoles.Keys.Aggregate(hash, (current, x) => current ^ (x.desc.GetHashCode() * (13 + 27 * x.version)));
         return hash;
      }
   }

   public class TerrainSnapshotCompiler : ITerrainSnapshotCompiler {
      private readonly SectorGraphDescriptionStore descriptionStore;
      private Dictionary<LocalGeometryJob, LocalGeometryViewManager> previousLocalGeometryViewManagers = new Dictionary<LocalGeometryJob, LocalGeometryViewManager>();
      private TerrainSnapshot cachedSnapshot;

      public TerrainSnapshotCompiler(SectorGraphDescriptionStore descriptionStore) {
         this.descriptionStore = descriptionStore;
      }

      /// <summary>
      /// For benchmarking purposes
      /// </summary>
      public void InvalidateCaches() {
         cachedSnapshot = null;
      }

      public TerrainSnapshot CompileSnapshot() {
         if (cachedSnapshot?.Version == descriptionStore.Version) return cachedSnapshot;
//         Console.WriteLine("Terrain Snapshot Compile Triggered");

         var nodeDescriptions = descriptionStore.EnumerateSectorNodeDescriptions().ToList();
         var edgeDescriptions = descriptionStore.EnumerateSectorEdgeDescriptions().ToList();
         var dynamicHoleDescriptions = descriptionStore.EnumerateDynamicTerrainHoleDescriptions().ToList();

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
            foreach (var holeDescription in dynamicHoleDescriptions) {
               holeDescription.EnhanceLocalGeometryJob(sectorNodeDescription, ref localGeometryRenderJob);
            }
            localGeometryRenderJobByNodeDescription.Add(sectorNodeDescription, localGeometryRenderJob);
         }

         var localGeometryPreviewJobsByRenderJob = localGeometryRenderJobByNodeDescription.Values.Distinct().ToDictionary(
            renderJob => renderJob,
            renderJob => new LocalGeometryJob(renderJob.TerrainStaticMetadata, renderJob.CrossoverSegments));


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

         return cachedSnapshot = new TerrainSnapshot {
            Version = descriptionStore.Version,
            NodeDescriptions = nodeDescriptions,
            EdgeDescriptions = edgeDescriptions,
            OverlayNetworkManager = terrainOverlayNetworkManager
         };
      }
   }
}
