using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ClipperLib;
using OpenMOBA.Foundation.Terrain.CompilationResults;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain {
   public interface ISourceSegmentEdgeDescription {
      IntLineSegment2 SourceSegment { get; }
   }

   public abstract class SectorEdgeDescription {
      public SectorNodeDescription Source { get; protected set; }
      public SectorNodeDescription Destination { get; protected set; }

      public abstract void EnhanceLocalGeometryJob(ref LocalGeometryJob localGeometryJob);

      public abstract List<EdgeJob> EmitCrossoverJobs(double crossoverPointSpacing, LocalGeometryView sourceLgv, LocalGeometryView destinationLgv);
   }

   public struct CrossoverJob {
      public CrossoverJob(PolyNode sourcePolyNode, PolyNode destinationPolyNode, DoubleVector2 sourceCrossoverPoint, DoubleVector2 destinationCrossoverPoint) {
         SourcePolyNode = sourcePolyNode;
         DestinationPolyNode = destinationPolyNode;
         SourceCrossoverPoint = sourceCrossoverPoint;
         DestinationCrossoverPoint = destinationCrossoverPoint;
      }

      public readonly PolyNode SourcePolyNode;
      public readonly PolyNode DestinationPolyNode;
      public readonly DoubleVector2 SourceCrossoverPoint;
      public readonly DoubleVector2 DestinationCrossoverPoint;

      public void Deconstruct(out PolyNode SourcePolyNode, out PolyNode DestinationPolyNode, out DoubleVector2 SourceCrossoverPoint, out DoubleVector2 DestinationCrossoverPoint) {
         SourcePolyNode = this.SourcePolyNode;
         DestinationPolyNode = this.DestinationPolyNode;
         SourceCrossoverPoint = this.SourceCrossoverPoint;
         DestinationCrossoverPoint = this.DestinationCrossoverPoint;
      }

      public bool Equals(CrossoverJob other) {
         return SourcePolyNode.Equals(other.SourcePolyNode) && DestinationPolyNode.Equals(other.DestinationPolyNode) && SourceCrossoverPoint.Equals(other.SourceCrossoverPoint) && DestinationCrossoverPoint.Equals(other.DestinationCrossoverPoint);
      }

      public override bool Equals(object obj) {
         if (ReferenceEquals(null, obj)) return false;
         return obj is CrossoverJob && Equals((CrossoverJob)obj);
      }

      public override int GetHashCode() {
         unchecked {
            var hashCode = SourcePolyNode.GetHashCode();
            hashCode = (hashCode * 397) ^ DestinationPolyNode.GetHashCode();
            hashCode = (hashCode * 397) ^ SourceCrossoverPoint.GetHashCode();
            hashCode = (hashCode * 397) ^ DestinationCrossoverPoint.GetHashCode();
            return hashCode;
         }
      }
   }

   public struct EdgeJob {
      public PortalSectorEdgeDescription EdgeDescription;
      public PolyNode SourcePolyNode;
      public DoubleLineSegment2 SourceSegment;
      public PolyNode DestinationPolyNode;
      public DoubleLineSegment2 DestinationSegment;
   }

   public class PortalSectorEdgeDescription : SectorEdgeDescription, ISourceSegmentEdgeDescription {
      public IntLineSegment2 SourceSegment { get; protected set; }
      public IntLineSegment2 DestinationSegment { get; protected set; }

      public override void EnhanceLocalGeometryJob(ref LocalGeometryJob localGeometryJob) {
         localGeometryJob.CrossoverSegments.Add(SourceSegment);
      }

      public override List<EdgeJob> EmitCrossoverJobs(double crossoverPointSpacing, LocalGeometryView sourceLgv, LocalGeometryView destinationLgv) {
//         return new List<EdgeJob>();

         var sourceSegmentVector = SourceSegment.First.To(SourceSegment.Second).ToDoubleVector2();
         var destinationSegmentVector = DestinationSegment.First.To(DestinationSegment.Second).ToDoubleVector2();

         var edgeJobs = new List<EdgeJob>();
         var streamSource = X(SourceSegment, sourceLgv).ToArray();
         var streamDest = X(DestinationSegment, destinationLgv).ToArray();
         var crossResult = CrossTheStreams(streamSource, streamDest).ToArray();
         foreach (var (source, dest, tStart, tEnd) in crossResult) {
            var sourceSubSegment = new DoubleLineSegment2(
               SourceSegment.First.ToDoubleVector2() + tStart * sourceSegmentVector,
               SourceSegment.First.ToDoubleVector2() + tEnd * sourceSegmentVector);

            var destinationSubSegment = new DoubleLineSegment2(
               DestinationSegment.First.ToDoubleVector2() + tStart * destinationSegmentVector,
               DestinationSegment.First.ToDoubleVector2() + tEnd * destinationSegmentVector);

            edgeJobs.Add(new EdgeJob {
               EdgeDescription = this,
               SourcePolyNode = source,
               SourceSegment = sourceSubSegment,
               DestinationPolyNode = dest,
               DestinationSegment = destinationSubSegment
            });
         }
         return edgeJobs;
      }

      private IEnumerable<(PolyNode, PolyNode, double, double)> CrossTheStreams(
         IEnumerable<(PolyNode, double)> sourceStream,
         IEnumerable<(PolyNode, double)> destStream
      ) {
         var ss = sourceStream.ToArray();
         var ds = destStream.ToArray();
         for (var i = 0; i < ss.Length; i += 2) {
            for (var j = 0; j < ds.Length; j += 2) {
               var sa = ss[i];
               var sb = ss[i + 1];
               var da = ds[j];
               var db = ds[j + 1];

               if (sb.Item2 <= da.Item2) continue;
               if (db.Item2 <= sa.Item2) continue;

               if (sa.Item2 <= da.Item2 && da.Item2 <= sb.Item2) {
                  if (sa.Item2 <= db.Item2 && db.Item2 <= sb.Item2) {
                     // containment
                     yield return (sa.Item1, da.Item1, da.Item2, db.Item2);
                  } else {
                     // overlap
                     yield return (sa.Item1, da.Item1, da.Item2, sb.Item2);
                  }
               } else if (da.Item2 <= sa.Item2 && sa.Item2 <= db.Item2) {
                  if (da.Item2 <= sb.Item2 && sb.Item2 <= db.Item2) {
                     // containment
                     yield return (sa.Item1, da.Item1, sa.Item2, sb.Item2);
                  } else {
                     // overlap
                     yield return (sa.Item1, da.Item1, sa.Item2, db.Item2);
                  }
               }
            }
         }
//         var eventStream = sourceStream
//            .Select(t => (true, t))
//            .Concat(destStream.Select(t => (false, t)))
//            .OrderBy(t => t.Item2.Item2);
//
//         PolyNode first = null, second = null;
//         double tstart = 0.0;
//         foreach (var (x, (n, t)) in eventStream) {
//            if (first != null && second != null) {
//               yield return (first, second, tstart, t);
//            }
//            if (x) first = first != null ? null : n;
//            else second = second != null ? null : n;
//            if (first != null && second != null) {
//               tstart = t;
//            }
//         }
      }

      public static IEnumerable<(PolyNode, double)> X(IntLineSegment2 seg, LocalGeometryView lgv) {
         var punchedLand = lgv.PunchedLand;
         punchedLand.AssertIsContourlessRootHolePunchResult();

         var allBreakpoints = new SortedList<double, List<(PolyNode, double)>>();
         
         foreach (var polyNode in punchedLand.EnumerateLandNodes()) {
            XVisitLandPolyNode(seg, polyNode, allBreakpoints);
         }

         var res = new List<(PolyNode, double)>();
         foreach (var x in allBreakpoints.Values) {
            res.AddRange(x);
         }
//            allBreakpoints.SelectMany(kvp => kvp.Value.Item2.Select(t => (kvp.Value.Item1, t))).ToList();
         return res;

//         // TODO: Fix this hack. Eventually. Maybe never. Possibly.
//
//
//         Trace.Assert(breakpoints.Count % 2 == 0);
//         return breakpoints.Select(kvp => (kvp.Value, kvp.Key));
         //
         //         var it = breakpoints.GetEnumerator();
         //         while (it.MoveNext()) {
         //            var bp0 = it.Current;
         //            Trace.Assert(it.MoveNext());
         //            var bp1 = it.Current;
         //            yield return (landPolyNodeBoundedByPolyNodeContour[bp0.Value], bp0.Key, bp1.Key);
         //         }
      }

      private static void XVisitLandPolyNode(IntLineSegment2 seg, PolyNode polyNode, SortedList<double, List<(PolyNode, double)>> allBreakpoints) {
         var contourBvh = polyNode.visibilityGraphNodeData.ContourBvh;
         if (contourBvh == null) {
            var localBreakpoints = new List<(PolyNode, double)>();
            H2(seg, polyNode, localBreakpoints);
            H3(seg, polyNode, allBreakpoints, localBreakpoints);

            foreach (var child in polyNode.Childs) {
               localBreakpoints = new List<(PolyNode, double)>();
               H2(seg, child, localBreakpoints);
               H3(seg, child, allBreakpoints, localBreakpoints);
            }
         } else {
            var localBreakpoints = new List<(PolyNode, double)>();
            var intersectingLeaves = contourBvh.FindPotentiallyIntersectingLeaves(seg);
            foreach (var leaf in intersectingLeaves) {
               for (var i = leaf.SegmentsStartIndexInclusive; i < leaf.SegmentsEndIndexExclusive; i++) {
                  ref var contourSegment = ref leaf.Segments[i];
                  H(ref seg, polyNode, ref contourSegment, localBreakpoints);
               }
            }
            H3(seg, polyNode, allBreakpoints, localBreakpoints);
         }
      }

      private static void H3(IntLineSegment2 seg, PolyNode polyNode, SortedList<double, List<(PolyNode, double)>> allBreakpoints, List<(PolyNode, double)> localBreakpoints) {
         if (Clipper.PointInPolygon(seg.First, polyNode.Contour) != PolygonContainmentResult.OutsidePolygon) {
            localBreakpoints.Add((polyNode, 0.0));
         }

         if (Clipper.PointInPolygon(seg.Second, polyNode.Contour) != PolygonContainmentResult.OutsidePolygon) {
            localBreakpoints.Add((polyNode, 1.0));
         }

         Trace.Assert(localBreakpoints.Count % 2 == 0);
         if (localBreakpoints.Count % 2 != 0) throw new Exception("Remove this line of code dear god");

         localBreakpoints.Sort((a, b) => a.Item2.CompareTo(b.Item2));
         for (var it = localBreakpoints.Count - 1; it >= 0; it -= 1) {
            // if no following element, continue
            if (it == localBreakpoints.Count - 1) continue;

            if (localBreakpoints[it + 1].Item2 - localBreakpoints[it].Item2 < 1E-9) {
               localBreakpoints.RemoveAt(it + 1);
               localBreakpoints.RemoveAt(it);
            }
         }

         Trace.Assert(localBreakpoints.Count % 2 == 0);
         if (localBreakpoints.Count % 2 != 0) throw new Exception("Remove this line of code dear god");

         if (localBreakpoints.Count > 0) {
            allBreakpoints.Add(localBreakpoints[0].Item2, localBreakpoints);
         }
      }

      private static void H2(IntLineSegment2 seg, PolyNode polyNode, List<(PolyNode, double)> localBreakpoints) {
         for (var i = 0; i < polyNode.Contour.Count; i++) {
            var contourSegment = new IntLineSegment2(
               i == 0 ? polyNode.Contour.Last() : polyNode.Contour[i - 1],
               polyNode.Contour[i]); // i = 5 {((24, 619), (-15, 619))}, i = 7

            H(ref seg, polyNode, ref contourSegment, localBreakpoints);
         }
      }

      private static void H(ref IntLineSegment2 seg, PolyNode polyNode, ref IntLineSegment2 contourSegment, List<(PolyNode, double)> localBreakpoints) {
         if (GeometryOperations.TryFindSegmentSegmentIntersectionT(ref seg, ref contourSegment, out var t)) {
            var clocknessFirst = GeometryOperations.Clockness(seg.First, seg.Second, contourSegment.First);
            var clocknessSecond = GeometryOperations.Clockness(seg.First, seg.Second, contourSegment.Second);
            if (clocknessFirst == Clockness.Neither) {
               Debug.Assert(clocknessSecond != Clockness.Neither);
               if (clocknessSecond == Clockness.Clockwise) return;
            } else if (clocknessSecond == Clockness.Neither) {
               Debug.Assert(clocknessFirst != Clockness.Neither);
               if (clocknessFirst == Clockness.Clockwise) return;
            }

            localBreakpoints.Add((polyNode, t));
         }
      }

      public static PortalSectorEdgeDescription Build(SectorNodeDescription source, SectorNodeDescription destination, IntLineSegment2 sourceSegment, IntLineSegment2 destinationSegment) {
         return new PortalSectorEdgeDescription {
            Source = source,
            Destination = destination,
            SourceSegment = sourceSegment,
            DestinationSegment = destinationSegment
         };
      }
   }

   //   public class Crossover {
   //      public Sector Source { get; set; }
   //      public Sector Destination { get; set; }
   //      public IntLineSegment2 SourceLocation { get; set; }
   //      public Matrix3x2 AToBTransformation { get; set; }
   //      public Matrix3x2 BToATransformation { get; set; }
   //   }

   public interface ISectorGraphDescriptionStore {
      void AddSectorNodeDescription(SectorNodeDescription sectorNodeDescription);
      void RemoveSectorNodeDescription(SectorNodeDescription sectorNodeDescription);

      void AddSectorEdgeDescription(SectorEdgeDescription sectorEdgeDescription);

      void AddTemporaryHoleDescription(DynamicTerrainHoleDescription holeDescription);
      void RemoveTemporaryHoleDescription(DynamicTerrainHoleDescription holeDescription);
   }

   public class SectorGraphDescriptionStore : ISectorGraphDescriptionStore {
      private readonly HashSet<SectorNodeDescription> nodeDescriptions = new HashSet<SectorNodeDescription>();
      private readonly HashSet<SectorEdgeDescription> edgeDescriptions = new HashSet<SectorEdgeDescription>();
      private readonly HashSet<DynamicTerrainHoleDescription> holeDescriptions = new HashSet<DynamicTerrainHoleDescription>();
      public int Version { get; private set; }

      public IEnumerable<SectorNodeDescription> EnumerateSectorNodeDescriptions() {
         return nodeDescriptions;
      }

      public IEnumerable<SectorEdgeDescription> EnumerateSectorEdgeDescriptions() {
         return edgeDescriptions;
      }

      public IEnumerable<DynamicTerrainHoleDescription> EnumerateDynamicTerrainHoleDescriptions() {
         return holeDescriptions;
      }

      public void AddSectorNodeDescription(SectorNodeDescription sectorNodeDescription) {
         if (nodeDescriptions.Add(sectorNodeDescription)) {
            Version++;

            // foreach (var hole in temporaryHoles.Where(hole => hole.AbsoluteBounds.IntersectsWith(sector.AbsoluteBounds))) {
            //    sectorsByHole.Add(hole, sector);
            //    holesBySector.Add(sector, hole);
            // }
         }
      }

      public void RemoveSectorNodeDescription(SectorNodeDescription sectorNodeDescription) {
         if (nodeDescriptions.Remove(sectorNodeDescription)) {
            Version++;

            // HashSet<DynamicTerrainHole> holes;
            // if (holesBySector.TryGetValue(sector, out holes)) {
            //    holesBySector.Remove(sector);
            // 
            //    foreach (var hole in holes) {
            //       sectorsByHole.Remove(hole, sector);
            //    }
            // }
         }
      }

      public void AddSectorEdgeDescription(SectorEdgeDescription sectorEdgeDescription) {
         if (edgeDescriptions.Add(sectorEdgeDescription)) {
            Version++;
         }
      }

      public void AddTemporaryHoleDescription(DynamicTerrainHoleDescription holeDescription) {
         if (holeDescriptions.Add(holeDescription)) {
            Version++;
         }
         // 
         //    foreach (var sector in sectors.Where(sector => sector.AbsoluteBounds.IntersectsWith(hole.AbsoluteBounds))) {
         //       holesBySector.Add(sector, hole);
         //       sectorsByHole.Add(hole, sector);
         //    }
         // }
      }

      public void RemoveTemporaryHoleDescription(DynamicTerrainHoleDescription holeDescription) {
         if (holeDescriptions.Remove(holeDescription)) {
            Version++;
         }
         // 
         //    HashSet<Sector> sectors;
         //    if (sectorsByHole.TryGetValue(hole, out sectors)) {
         //       sectorsByHole.Remove(hole);
         // 
         //       foreach (var sector in sectors) {
         //          holesBySector.Remove(sector, hole);
         //       }
         //    }
         // }
      }

      public void Clear() {
         nodeDescriptions.Clear();
         edgeDescriptions.Clear();
         holeDescriptions.Clear();
         Version++;
      }
   }

   public class TerrainService : ISectorGraphDescriptionStore, ITerrainSnapshotCompiler {
      private readonly SectorGraphDescriptionStore storage;
      private readonly TerrainSnapshotCompiler snapshotCompiler;

      public TerrainService(SectorGraphDescriptionStore storage, TerrainSnapshotCompiler snapshotCompiler) {
         this.storage = storage;
         this.snapshotCompiler = snapshotCompiler;
      }

      public TerrainSnapshotCompiler SnapshotCompiler => snapshotCompiler;

      public SectorNodeDescription CreateSectorNodeDescription(TerrainStaticMetadata metadata) {
         return new SectorNodeDescription(this, metadata);
      }

      public DynamicTerrainHoleDescription CreateHoleDescription(IHoleStaticMetadata staticMetadata) {
         return new DynamicTerrainHoleDescription(this, staticMetadata);
      }

      public void AddSectorNodeDescription(SectorNodeDescription sectorNodeDescription) {
         storage.AddSectorNodeDescription(sectorNodeDescription);
      }

      public void RemoveSectorNodeDescription(SectorNodeDescription sectorNodeDescription) {
         storage.RemoveSectorNodeDescription(sectorNodeDescription);
      }

      public void AddSectorEdgeDescription(SectorEdgeDescription sectorEdgeDescription) {
         storage.AddSectorEdgeDescription(sectorEdgeDescription);
      }

      public void AddTemporaryHoleDescription(DynamicTerrainHoleDescription holeDescription) {
         storage.AddTemporaryHoleDescription(holeDescription);
      }

      public void RemoveTemporaryHoleDescription(DynamicTerrainHoleDescription holeDescription) {
         storage.RemoveTemporaryHoleDescription(holeDescription);
      }

      public void Clear() {
         storage.Clear();
      }

      public TerrainSnapshot CompileSnapshot() {
         return snapshotCompiler.CompileSnapshot();
      }
   }

   public static class TerrainHoleHelpers {
      public static bool ContainsPoint(this DynamicTerrainHoleDescription dynamicTerrainHoleDescription, double holeDilationRadius, DoubleVector3 point) {
         Console.WriteLine("NI: ContainsPoint");
         return false;
//         // Padding so that when flooring the point, we don't accidentally say a point isn't
//         // in the hole when in reality, it is. 
//         var paddedHoleShapeUnion = PolygonOperations.Offset()
//                                                     .Dilate(holeDilationRadius)
//                                                     .Include(dynamicTerrainHoleDescription.Polygons)
//                                                     .Execute();
//
//         PolyNode node;
//         bool isHole;
//         paddedHoleShapeUnion.PickDeepestPolynodeGivenHoleShapePolytree(point.XY.LossyToIntVector2(), out node, out isHole);
//
//         // we want land inside the hole-shape-union because we want to know if we're in the hole, not a hole of the hole shape.
//         return !isHole;
      }
   }
}