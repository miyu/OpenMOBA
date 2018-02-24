using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;
using OpenMOBA;
using OpenMOBA.DataStructures;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;

namespace PolyNodeCrossoverPointManagerBenchmark {
   public static class Program {
      private static readonly Size bounds = new Size(720, 720);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(
         bounds, 
         new Point(50, 50), 
         new OrthographicXYProjector(0.7));

      public static void Main(string[] args) {
         var sectorMetadataPresets = SectorMetadataPresets.HashCircle2;
         var terrainStaticMetadata = new TerrainStaticMetadata {
            LocalBoundary = sectorMetadataPresets.LocalBoundary,
            LocalIncludedContours = sectorMetadataPresets.LocalIncludedContours,
            LocalExcludedContours = sectorMetadataPresets.LocalExcludedContours
         };

         var (localGeometryView, landPolyNode, crossoverPointManager) = BenchmarkAddCrossoverPoints(terrainStaticMetadata);
         var canvas = host.CreateAndAddCanvas(0);
         //canvas.DrawPolyTree((PolyTree)landPolyNode.Parent);
         canvas.DrawPoints(crossoverPointManager.CrossoverPoints, StrokeStyle.RedThick5Solid);
         canvas.DrawVisibilityGraph(landPolyNode.ComputeVisibilityGraph());
         canvas.DrawLineList(landPolyNode.FindContourAndChildHoleBarriers(), StrokeStyle.BlackHairLineSolid);

         // var a = landPolyNode.FindAggregateContourCrossoverWaypoints()[6];
         // var b = landPolyNode.FindAggregateContourCrossoverWaypoints()[13];
         // var q = new IntLineSegment2(a, b);
         // canvas.DrawPoint(a, StrokeStyle.RedThick5Solid);
         // canvas.DrawPoint(b, StrokeStyle.RedThick5Solid);
         // var bvh = landPolyNode.FindContourAndChildHoleBarriersBvh();
         // canvas.DrawBvh(bvh);
         // foreach (var (i, val) in bvh.BoundingBoxes.Enumerate()) {
         //    if (val.Intersects(q)) Console.WriteLine(i + " " + val);
         // }
         // var intersects = bvh.Intersects(q);
         // canvas.DrawLine(a, b, intersects ? StrokeStyle.RedHairLineSolid : StrokeStyle.LimeHairLineSolid);

         Console.WriteLine(
            PolyNodeCrossoverPointManager.AddMany_ConvexHullsComputed + " " + 
            PolyNodeCrossoverPointManager.CrossoverPointsAdded + " " +
            PolyNodeCrossoverPointManager.FindOptimalLinksToCrossoversInvocationCount + " " + 
            PolyNodeCrossoverPointManager.FindOptimalLinksToCrossovers_CandidateWaypointVisibilityCheck + " " + 
            PolyNodeCrossoverPointManager.FindOptimalLinksToCrossovers_CostToWaypointCount + " " + 
            PolyNodeCrossoverPointManager.ProcessCpiInvocationCount + " " + 
            PolyNodeCrossoverPointManager.ProcessCpiInvocation_CandidateBarrierIntersectCount + " " + 
            PolyNodeCrossoverPointManager.ProcessCpiInvocation_DirectCount + " " + 
            PolyNodeCrossoverPointManager.ProcessCpiInvocation_IndirectCount);

         while (true) {
            const int ntrials = 100;
            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < ntrials; i++) {
               BenchmarkAddCrossoverPoints(terrainStaticMetadata);
            }
            Console.WriteLine($"{ntrials} trials in {sw.ElapsedMilliseconds} ms");
         }
      }

      private static (LocalGeometryView, PolyNode, PolyNodeCrossoverPointManager) BenchmarkAddCrossoverPoints(TerrainStaticMetadata terrainStaticMetadata) {
         var crossoverSegments = new IntLineSegment2[] {
            new IntLineSegment2(new IntVector2(200, 0), new IntVector2(400, 0)),
            new IntLineSegment2(new IntVector2(600, 0), new IntVector2(800, 0)),
            new IntLineSegment2(new IntVector2(200, 1000), new IntVector2(400, 1000)),
            new IntLineSegment2(new IntVector2(600, 1000), new IntVector2(800, 1000)),
            
            new IntLineSegment2(new IntVector2(0, 200), new IntVector2(0, 400)),
            new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)),
            new IntLineSegment2(new IntVector2(1000, 200), new IntVector2(1000, 400)),
            new IntLineSegment2(new IntVector2(1000, 600), new IntVector2(1000, 800))
         };

         var actorRadius = 10.0;
         var localGeometryJob = new LocalGeometryJob(terrainStaticMetadata, crossoverSegments.ToHashSet());
         var localGeometryViewManager = new LocalGeometryViewManager(localGeometryJob);
         var localGeometryView = localGeometryViewManager.GetErodedView(actorRadius);
         var landPolyNode = localGeometryView.PunchedLand.Childs.First();

         // Precompute polynode geometry structures to isolate in profiling results.
         landPolyNode.FindAggregateContourCrossoverWaypoints();
         landPolyNode.ComputeWaypointVisibilityPolygons();
         landPolyNode.ComputeVisibilityGraph();
         landPolyNode.FindContourAndChildHoleBarriers();
         landPolyNode.FindContourAndChildHoleBarriersBvh();

         // Then build CPM, which uses cached results from above.
         var crossoverPointManager = new PolyNodeCrossoverPointManager(landPolyNode);
         // return (localGeometryView, landPolyNode, crossoverPointManager);

         var spacing = 50;
         foreach (var crossoverSegment in crossoverSegments) {
            var cs = new DoubleLineSegment2(crossoverSegment.First.ToDoubleVector2(), crossoverSegment.Second.ToDoubleVector2());
            var firstToSecond = cs.First.To(cs.Second);
            var shrink = firstToSecond * (actorRadius / firstToSecond.Norm2D());
            cs = new DoubleLineSegment2(cs.First + shrink, cs.Second - shrink);
            AddCrossoverPoints(crossoverPointManager, cs, spacing);
         }
         
         //Console.WriteLine(crossoverPointManager.CrossoverPoints.Count + " " + crossoverPointManager.CrossoverPoints.Count / 8);

         return (localGeometryView, landPolyNode, crossoverPointManager);
      }

      private static void AddCrossoverPoints(PolyNodeCrossoverPointManager cpm, DoubleLineSegment2 segment, int spacing) {
         var npoints = (int)Math.Ceiling(segment.First.To(segment.Second).Norm2D() / spacing) + 1;
         var points = Util.Generate(npoints, i => segment.PointAt(i / (double)(npoints - 1)).LossyToIntVector2());
         cpm.AddMany(segment, points);
      }
   }
}
