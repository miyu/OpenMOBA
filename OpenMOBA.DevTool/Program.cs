using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using Canvas3D;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Debugging;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;
using SharpDX;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Vector3 = System.Numerics.Vector3;

namespace OpenMOBA.DevTool {
   public static class Program {
      public static void Main(string[] args) {
         var gameFactory = new GameFactory();
         gameFactory.GameCreated += (s, game) => { GameDebugger.AttachToWithSoftwareRendering(game); };
//         gameFactory.GameCreated += (s, game) => { GameDebugger.AttachToWithHardwareRendering(game); };
         OpenMOBA.Program.Main(gameFactory);
      }
   }

   public class GameDebugger : IGameDebugger {
      private static readonly StrokeStyle PathStroke = new StrokeStyle(Color.Lime, 5.0);
      private static readonly StrokeStyle PathStroke2 = new StrokeStyle(Color.DarkGreen, 15.0);
      private static readonly StrokeStyle NoPathStroke = new StrokeStyle(Color.Red, 15.0, new[] { 1.0f, 1.0f });
      private static readonly StrokeStyle HighlightStroke = new StrokeStyle(Color.Red, 3.0);

      public GameDebugger(Game game, IDebugMultiCanvasHost debugMultiCanvasHost) {
         Game = game;
         DebugMultiCanvasHost = debugMultiCanvasHost;
      }

      public Game Game { get; }
      public IDebugMultiCanvasHost DebugMultiCanvasHost { get; }

      private DebugProfiler DebugProfiler => Game.DebugProfiler;
      private GameTimeService GameTimeService => Game.GameTimeService;
      private TerrainService TerrainService => Game.TerrainService;
      private EntityService EntityService => Game.EntityService;

      public void HandleFrameEnd(FrameEndStatistics frameStatistics) {
         if (GameTimeService.Ticks == 0) {
            //            AddSquiggleHole();
         }
         if (frameStatistics.EventsProcessed != 0 || GameTimeService.Ticks % 16 == 0) RenderDebugFrame();
      }

      private void AddSquiggleHole() {
         var holeSquiggle = PolylineOperations.ExtrudePolygon(
            new[] {
               new IntVector2(100, 50),
               new IntVector2(100, 100),
               new IntVector2(200, 100),
               new IntVector2(200, 150),
               new IntVector2(200, 200),
               new IntVector2(400, 250),
               new IntVector2(200, 300),
               new IntVector2(400, 315),
               new IntVector2(200, 330),
               new IntVector2(210, 340),
               new IntVector2(220, 350),
               new IntVector2(220, 400),
               new IntVector2(221, 400)
            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygons();
         Console.WriteLine("NI: AddSquiggleHole");
         throw new NotImplementedException();
         //TerrainService.AddTemporaryHoleDescription(new DynamicTerrainHoleDescription { Polygons = holeSquiggle });
      }

      private void Benchmark(double holeDilationRadius) {
         void RunBenchmarkIteration() {
            TerrainService.SnapshotCompiler.InvalidateCaches();
            TerrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);
         }

         for (var i = 0; i < 10; i++) {
            RunBenchmarkIteration();
            if (i == 0)
               PolyNodeCrossoverPointManager.DumpPerformanceCounters();
         }
         GC.Collect();
         var sw = new Stopwatch();
         sw.Start();
         for (var i = 0; i < 10; i++) RunBenchmarkIteration();
         Console.WriteLine("10itr: " + sw.ElapsedMilliseconds + "ms");
      }

      public delegate void RenderHookEvent(GameDebugger debugger, IDebugCanvas canvas);
      public RenderHookEvent RenderHook;

      private void RenderDebugFrame() {
         var holeDilationRadius = 15.0;
         if (GameTimeService.Ticks > 20) {
            Benchmark(holeDilationRadius);
            return;
         }

         var terrainSnapshot = TerrainService.CompileSnapshot();
         var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);
         //terrainOverlayNetwork.Initialize();
         Console.WriteLine("Rendering");

         var debugCanvas = DebugMultiCanvasHost.CreateAndAddCanvas(GameTimeService.Ticks);
         //         var temporaryHolePolygons = terrainSnapshot.TemporaryHoles.SelectMany(th => th.Polygons).ToList();
         debugCanvas.BatchDraw(() => {
            debugCanvas.Transform = Matrix4x4.Identity;
            RenderHook?.Invoke(this, debugCanvas);
            if (RenderHook != null) return;

            debugCanvas.Transform = Matrix4x4.Identity;

            //debugCanvas.DrawLine(new DoubleVector3(0, 0, 0), new DoubleVector3(10000, 0, 0), new StrokeStyle(Color.Red, 50));
            //debugCanvas.DrawLine(new DoubleVector3(0, 0, 0), new DoubleVector3(0, 10000, 0), new StrokeStyle(Color.Lime, 50));
            //debugCanvas.DrawLine(new DoubleVector3(0, 0, 0), new DoubleVector3(0, 0, 10000), new StrokeStyle(Color.Blue, 50));

            //            return;

            //            DrawTestPathfindingQueries(debugCanvas, holeDilationRadius);



            /*
            // FOR BUNNY
            debugCanvas.DrawPoint(new DoubleVector3(-561.450012207031, -1316.31005859375, -116.25), new StrokeStyle(Color.Gray, 850));
            // Paths from Source [808.800476074219, -2133.13989257813, 466.265472412109] to [-496.957489013672, 566.484985351563, 3515.56762695313]
            Console.WriteLine("# TONs: " + terrainOverlayNetwork.TerrainNodes.Count);
            var sourceNode = terrainOverlayNetwork.TerrainNodes.First(n => {
               var w = Vector3.Transform(new Vector3(n.SectorNodeDescription.StaticMetadata.LocalBoundary.Width / 2.0f, n.SectorNodeDescription.StaticMetadata.LocalBoundary.Height / 2.0f, 0), n.SectorNodeDescription.WorldTransform);
               return w.X < -700 && w.Y < -1300 && w.Z > 1100;// && w.Y < -2000 && w.Z > 500;
            });
            var destinationNode = terrainOverlayNetwork.TerrainNodes.First(n => {
               var w = Vector3.Transform(new Vector3(n.SectorNodeDescription.StaticMetadata.LocalBoundary.Width / 2.0f, n.SectorNodeDescription.StaticMetadata.LocalBoundary.Height / 2.0f, 0), n.SectorNodeDescription.WorldTransform);
               return w.X > 350 && w.X < 450 && w.Y < -1200 && w.Z > -800 && w.Z < -600;// && w.Y < 500 && w.Z > 3200 && w.Z < 3700; // -1072 343 3786
            });

            //[491.319305419922, -1594.47033691406, -1430.91870117188]

            var sourcePoint = Vector3.Transform(new Vector3(0, 0, 0), sourceNode.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
            var destinationPoint = Vector3.Transform(new Vector3(0, 0, 0), destinationNode.SectorNodeDescription.WorldTransform).ToOpenMobaVector();

            var sourceLocal = Vector3.Transform(sourcePoint.ToDotNetVector(), sourceNode.SectorNodeDescription.WorldTransformInv);
            var destinationLocal = Vector3.Transform(destinationPoint.ToDotNetVector(), destinationNode.SectorNodeDescription.WorldTransformInv);
            var sourceValid = sourceNode.LandPolyNode.PointInLandPolygonNonrecursive(new IntVector2((int)sourceLocal.X + 50, (int)sourceLocal.Y + 50));
            var destinationValid = destinationNode.LandPolyNode.PointInLandPolygonNonrecursive(new IntVector2((int)destinationLocal.X + 50, (int)destinationLocal.Y + 50));
            Console.WriteLine("Svalid? " + sourceValid + " Dvalid? " + destinationValid);

            sourcePoint = Vector3.Transform(sourceLocal + new Vector3(50, 50, 0), sourceNode.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
            destinationPoint = Vector3.Transform(destinationLocal + new Vector3(50, 50, 0), destinationNode.SectorNodeDescription.WorldTransform).ToOpenMobaVector();

            DrawPathfindingQueryResult(debugCanvas, holeDilationRadius, sourcePoint, destinationPoint);
            Console.WriteLine("Source: " + sourcePoint + " to " + destinationPoint);

            debugCanvas.Transform = Matrix4x4.Identity;
            debugCanvas.DrawPoint(sourcePoint, new StrokeStyle(Color.Lime, 100));
            debugCanvas.DrawPoint(destinationPoint, new StrokeStyle(Color.Red, 100));


            /*
            // for dragon
            Console.WriteLine("# TONs: " + terrainOverlayNetwork.TerrainNodes.Count);
            var sourceNode = terrainOverlayNetwork.TerrainNodes.Where(n => {
               var w = Vector3.Transform(new Vector3(n.SectorNodeDescription.StaticMetadata.LocalBoundary.Width / 2.0f, n.SectorNodeDescription.StaticMetadata.LocalBoundary.Height / 2.0f, 0), n.SectorNodeDescription.WorldTransform);
               //[849.049987792969, 489.600006103516, 1492.59997558594] to[849.049987792969, 489.600006103516, 1492.59997558594
               return w.X < -500 && w.Y < 490 && w.Z > 1490;
            }).Skip(0).First();
            var destinationNode = terrainOverlayNetwork.TerrainNodes.Where(n => {
               var w = Vector3.Transform(new Vector3(n.SectorNodeDescription.StaticMetadata.LocalBoundary.Width / 2.0f, n.SectorNodeDescription.StaticMetadata.LocalBoundary.Height / 2.0f, 0), n.SectorNodeDescription.WorldTransform);
               return w.X > 850 && w.Y < 490 && w.Z > 1490;
            }).Skip(0).First();
            var sourcePoint = Vector3.Transform(new Vector3(0, 0, 0), sourceNode.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
            var destinationPoint = Vector3.Transform(new Vector3(0, 0, 0), destinationNode.SectorNodeDescription.WorldTransform).ToOpenMobaVector();

            var sourceLocal = Vector3.Transform(sourcePoint.ToDotNetVector(), sourceNode.SectorNodeDescription.WorldTransformInv);
            var destinationLocal = Vector3.Transform(destinationPoint.ToDotNetVector(), destinationNode.SectorNodeDescription.WorldTransformInv);
            var sourceValid = sourceNode.LandPolyNode.PointInLandPolygonNonrecursive(new IntVector2((int)sourceLocal.X, (int)sourceLocal.Y));
            var destinationValid = destinationNode.LandPolyNode.PointInLandPolygonNonrecursive(new IntVector2((int)destinationLocal.X, (int)destinationLocal.Y));
            Console.WriteLine("Svalid? " + sourceValid + " Dvalid? " + destinationValid);
            // while(true) 
//				DrawPathfindingQueryResult(debugCanvas, holeDilationRadius, sourcePoint, destinationPoint);
            Console.WriteLine("Source: " + sourcePoint + " to " + destinationPoint);


            debugCanvas.Transform = Matrix4x4.Identity;
//				debugCanvas.DrawPoint(sourcePoint, new StrokeStyle(Color.Lime, 200));
            debugCanvas.DrawPoint(destinationPoint, new StrokeStyle(Color.Red, 200));
            /**/

//			   DrawPathfindingQueryResult(debugCanvas, holeDilationRadius, new DoubleVector3(0, -500, 400), new DoubleVector3(0, 500, 400));

            var boundsBvh = terrainOverlayNetwork.NodeBvh;

            void DrawBvhAABB<TValue>(BvhTreeAABB<TValue> bvhRoot) {
               var q = new Queue<(BvhTreeAABB<TValue>, int)>();
               q.Enqueue((bvhRoot, 0));

               var maxDepth = -1;
               while (q.Count > 0) {
                  var (node, depth) = q.Dequeue();
                  if (node.First == null) {
                     maxDepth = Math.Max(depth, maxDepth);
                  } else {
                     q.Enqueue((node.First, depth + 1));
                     q.Enqueue((node.Second, depth + 1));
                  }
               }

               void Helper(BvhTreeAABB<TValue> bvh, int depth = 0) {
                  if (bvh.First != null) {
                     var r = maxDepth == 0 ? 0 : (int)(255 * depth / (float)maxDepth);
                     var g = 255 - r;

                     debugCanvas.DrawAxisAlignedBoundingBox(bvh.Bounds, new StrokeStyle(Color.FromArgb(r, g, 0), 3));

                     Helper(bvh.First, depth + 1);
                     Helper(bvh.Second, depth + 1);
                  } else {
                     for (var i = bvh.StartIndexInclusive; i < bvh.EndIndexExclusive; i++) {
                        debugCanvas.DrawAxisAlignedBoundingBox(bvh.BoundingBoxes[i], new StrokeStyle(Color.Black, 3));
                     }
                  }
               }

               Helper(bvhRoot);
            }

            //				DrawBvhAABB(boundsBvh);
            //			   foreach (var node in boundsBvh.FindPotentiallyIntersectingLeaves(sourcePoint)) {
            //			      debugCanvas.DrawAxisAlignedBoundingBox(node.Bounds, new StrokeStyle(Color.Red, 3));
            //			      for (var i = node.StartIndexInclusive; i < node.EndIndexExclusive; i++) {
            //			         var tonn = node.Values[i];
            //			         debugCanvas.Transform = tonn.SectorNodeDescription.WorldTransform;
            //                  debugCanvas.FillTriangulation(tonn.LocalGeometryView.Triangulation, new FillStyle(Color.White));
            ////                  debugCanvas.DrawAxisAlignedBoundingBox(node.BoundingBoxes[i], new StrokeStyle(Color.Black, 3));
            //			      }
            //			   }


//			   debugCanvas.Transform = Matrix4x4.Identity;
//			   debugCanvas.DrawPoint(new DoubleVector3(0, 0, 500), new StrokeStyle(Color.Red, 45));


            var colors = new[] { Color.White };
//			   var colors = new[] { Color.Red, Color.Lime, Color.Cyan, Color.Magenta, Color.Yellow, Color.Orange, Color.Blue, Color.Indigo, Color.Violet };
            foreach (var (index, terrainNode) in terrainOverlayNetwork.TerrainNodes.Enumerate()) {
               var sectorNodeDescription = terrainNode.SectorNodeDescription;
               var localGeometryView = terrainNode.LocalGeometryView;
               var landPolyNode = terrainNode.LandPolyNode;
               var crossoverPointManager = terrainNode.CrossoverPointManager;

//               if (index % colors.Length != 2) {
//                  continue;
//               }
//               if (index != 2 + 1 * colors.Length) {
//                  continue;
//               }
//               Console.WriteLine(index);

               debugCanvas.Transform = Matrix4x4.Identity;
//               var dc = ((Canvas3DDebugMultiCanvasHost.Canvas3DDebugCanvas)debugCanvas);
//               debugCanvas.DrawAxisAlignedBoundingBox(sectorNodeDescription.WorldBounds, new StrokeStyle(Color.Black, 3));

//               dc.Scene.AddRenderable(
//                  dc.GraphicsFacade.Presets.UnitCube,
//                  MatrixCM.Translation(
//                     (float)sectorNodeDescription.WorldBounds.Center.X,
//                     (float)sectorNodeDescription.WorldBounds.Center.Y,
//                     (float)sectorNodeDescription.WorldBounds.Center.Z) * 
//                     MatrixCM.Scaling(
//                        (float)sectorNodeDescription.WorldBounds.Extents.X,
//                        (float)sectorNodeDescription.WorldBounds.Extents.Y,
//                        (float)sectorNodeDescription.WorldBounds.Extents.Z),
//                  new MaterialDescription { Properties = { Metallic = 0, Roughness = 1}},
//                  SharpDX.Color.Black);


//               debugCanvas.DrawPoint(Vector3.Transform(new Vector3(0, 0, 0), sectorNodeDescription.WorldTransform).ToOpenMobaVector(), new StrokeStyle(Color.Black, 100));
               

               debugCanvas.Transform = sectorNodeDescription.WorldTransform;
               //debugCanvas.DrawPoint(new DoubleVector3(0, 0, 0), new StrokeStyle(Color.Black, 100));
               var fillColor = colors[(index / colors.Length) % colors.Length];
//               if (localGeometryView.Job.DynamicHoles.Count > 0) {
//                  fillColor = Color.Red;
//               }
//               debugCanvas.FillTriangulation(new Triangulator().TriangulateLandNode(landPolyNode), new FillStyle(fillColor));
					debugCanvas.DrawTriangulation(localGeometryView.Triangulation, new StrokeStyle(Color.DarkGray));
               debugCanvas.DrawLineList(localGeometryView.Job.CrossoverSegments.ToArray(), StrokeStyle.RedHairLineSolid);

//               foreach (var c in visibilityGraphNodeData.EdgeDescriptions) {
//                  debugCanvas.DrawLine(c.SourceSegment.First, c.SourceSegment.Second, new StrokeStyle(Color.Red, 5));
//               }

               continue;
//               debugCanvas.DrawPolyTree(localGeometryView.PunchedLand);

               //Console.WriteLine("Holes: " + localGeometryView.Job.DynamicHoles.Count);
//					foreach (var (k, v) in localGeometryView.Job.DynamicHoles) {
//						debugCanvas.DrawPolygonContours(v.holeIncludedContours, StrokeStyle.RedHairLineSolid);
//						debugCanvas.DrawPolygonContours(v.holeExcludedContours, StrokeStyle.RedHairLineSolid);
//                  Console.WriteLine("!");
//					}

               
//               debugCanvas.DrawLine(new DoubleVector3(0, 600, 0), new DoubleVector3(0, 800, 0), new StrokeStyle(Color.Magenta, 1));
//               try {
//                  var xres = PortalSectorEdgeDescription.X(new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)), localGeometryView).ToArray();
//                  Console.WriteLine("X size: " + xres.Length);
//                  var ps = xres.Map(t => t.Item2).Map(new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)).PointAt);
//                  debugCanvas.DrawPoints(ps, new StrokeStyle(Color.Lime, 5));
//               } catch(Exception e) {
//                  MessageBox.Show($"!!! {e}");
//                  debugCanvas.FillTriangle(new DoubleVector3(20, 20, 0), new DoubleVector3(500, 20, 0), new DoubleVector3(20, 500, 0), new FillStyle(Color.Red));
//               }

               //               debugCanvas.DrawLine(new IntVector2(144, -7), new IntVector2(145, 2), new StrokeStyle(Color.Orange));
               //               var x = terrainNode.LandPolyNode.FindContourAndChildHoleBarriers();
               //               var y = terrainNode.CrossoverPointManager.CrossoverPoints;
               //
               //               debugCanvas.DrawLineList(x, new StrokeStyle(Color.Red));
               //
               //
               //               var nodeAndChildrenContours = new[] { terrainNode.LandPolyNode.Contour }
               //                  .Concat(terrainNode.LandPolyNode.Childs.Select(c => c.Contour)).ToArray()
               //                  .Map(c => c.Concat(new[] { c.First() }).ToArray());
               //               var dilatedNodeAndChildrenPolytree = PolygonOperations.Offset()
               //                                                                     .Include(nodeAndChildrenContours)
               //                                                                     .Dilate(15)
               //                                                                     .Erode(10)
               //                                                                     .Execute();
               //               debugCanvas.DrawPolyTree(dilatedNodeAndChildrenPolytree, new StrokeStyle(Color.Lime), new StrokeStyle(Color.Cyan));

               //               var seg = new IntLineSegment2(new IntVector2(-54, 6), new IntVector2(145, 0));
               //               foreach (var barrier in x) {
               //                  if (barrier.Intersects(ref seg)) {
               //                     throw new Exception("!!");
               //                  }
               //               }

               //               Console.WriteLine(x + "" + y);

               //               debugCanvas.DrawPoints(landPolyNode.FindAggregateContourCrossoverWaypoints(), StrokeStyle.RedThick25Solid);
               //               debugCanvas.DrawVisibilityGraph(landPolyNode.ComputeVisibilityGraph());


               //               if (landPolyNode.FindAggregateContourCrossoverWaypoints().Length > 16) {
               //                  debugCanvas.DrawPoint(landPolyNode.FindAggregateContourCrossoverWaypoints()[7], new StrokeStyle(Color.Lime, 50));
               //                  debugCanvas.DrawPoint(landPolyNode.FindAggregateContourCrossoverWaypoints()[16], new StrokeStyle(Color.Lime, 50));
               //               }

               //               debugCanvas.DrawLine(new IntVector2(800, 500), new IntVector2(1000, 215), new StrokeStyle(Color.Magenta, 5));
               //               if (terrainNode != terrainOverlayNetwork.TerrainNodes.Last()) continue;

               //               debugCanvas.DrawLine(new IntVector2(400, 550), new IntVector2(0, 615), new StrokeStyle(Color.Magenta, 1));
               //               Console.WriteLine("!@#@!#!@#@!@");
               //               Console.WriteLine("!!!!!!!AAAAAA");
               //               Console.WriteLine(!terrainNode.LandPolyNode.FindContourAndChildHoleBarriersBvh().TryIntersect(new IntLineSegment2(new IntVector2(400, 550), new IntVector2(0, 615)), out var qqqq) + " " + qqqq);
               //               Console.WriteLine("!!!!!!!BBBB");
               //               Console.WriteLine(terrainNode.LandPolyNode.SegmentInLandPolygonNonrecursive(new IntVector2(400, 550), new IntVector2(0, 615)));
               //               Console.WriteLine("!!!!!!!CCCC");
               //               var (_, _, _, sourceOptimalLinkToCrossovers) = terrainNode.CrossoverPointManager.FindOptimalLinksToCrossovers(new IntVector2(400, 550));
               //               Console.WriteLine("!!!!!!!!");
               //               Console.WriteLine(sourceOptimalLinkToCrossovers[0].PriorIndex);
               //               Console.WriteLine("!!!!!!!AAAAAA");
               //               Console.WriteLine(!terrainNode.LandPolyNode.FindContourAndChildHoleBarriersBvh().TryIntersect(new IntLineSegment2(new IntVector2(400, 550), new IntVector2(0, 615)), out var zzz) + " " + zzz);
               //               Console.WriteLine("!!!!!!!BBBB");
               //               Console.WriteLine(terrainNode.LandPolyNode.SegmentInLandPolygonNonrecursive(new IntVector2(400, 550), new IntVector2(0, 615)));
               //               Console.WriteLine("!!!!!!!CCCC");
               //               debugCanvas.DrawBvh(terrainNode.LandPolyNode.FindContourAndChildHoleBarriersBvh());

               //if (!sectorNodeDescription.EnableDebugHighlight) continue;

               //var outboundEdges = terrainNode.OutboundEdgeGroups.SelectMany(kvp => kvp.Value).ToList();
               //if (outboundEdges.Count >= 4) {
               //   var g1 = outboundEdges.First();
               //   var g2 = outboundEdges.Skip(3).First();
               //   var e1 = g1.Edges[g1.Edges.Length * 3 / 4];
               //   var e2 = g2.Edges[g2.Edges.Length * 1 / 4];
               //   var linkEnter = crossoverPointManager.OptimalLinkToOtherCrossoversByCrossoverPointIndex[e1.SourceCrossoverIndex][e2.SourceCrossoverIndex];
               //   var linkExit = crossoverPointManager.OptimalLinkToOtherCrossoversByCrossoverPointIndex[e2.SourceCrossoverIndex][e1.SourceCrossoverIndex];
               //   if (linkEnter.PriorIndex == PathLink.DirectPathIndex) {
               //      debugCanvas.DrawLine(crossoverPointManager.CrossoverPoints[e1.SourceCrossoverIndex], crossoverPointManager.CrossoverPoints[e2.SourceCrossoverIndex], PathStroke2);
               //   } else {
               //      debugCanvas.DrawLine(crossoverPointManager.CrossoverPoints[e1.SourceCrossoverIndex], crossoverPointManager.Waypoints[linkEnter.PriorIndex], PathStroke);
               //      debugCanvas.DrawLine(crossoverPointManager.Waypoints[linkExit.PriorIndex], crossoverPointManager.CrossoverPoints[e2.SourceCrossoverIndex], PathStroke);
               //   }
               //}
               //foreach (var g in outboundEdges) foreach (var e in g.Edges) debugCanvas.DrawPoint(crossoverPointManager.CrossoverPoints[e.SourceCrossoverIndex], new StrokeStyle(Color.White, 3));

               //var bvh = terrainNode.LandPolyNode.visibilityGraphNodeData.ContourBvh;
               //if (bvh != null) debugCanvas.DrawBvh(bvh);

               //               var ssws = landPolyNode.ComputeSegmentSeeingWaypoints(new DoubleLineSegment2(new DoubleVector2(0, 200), new DoubleVector2(0, 400)));
               //               var ssws = landPolyNode.ComputeSegmentSeeingWaypoints(new DoubleLineSegment2(new DoubleVector2(0, 190), new DoubleVector2(0, 410)));
               //               var ssws = landPolyNode.ComputeSegmentSeeingWaypoints(new DoubleLineSegment2(new DoubleVector2(0, 600), new DoubleVector2(0, 800)));
               //               var ssws = landPolyNode.ComputeSegmentSeeingWaypoints(new DoubleLineSegment2(new DoubleVector2(1000, 190), new DoubleVector2(1000, 410)));
               //               var ssws = landPolyNode.ComputeSegmentSeeingWaypoints(new DoubleLineSegment2(new DoubleVector2(1000, 590), new DoubleVector2(1000, 810)));
               //               Console.WriteLine("!!!" + ssws.Length);
               //               foreach (var ssw in ssws) {
               //                  var ind = landPolyNode.FindAggregateContourCrossoverWaypoints()[ssw];
               //                  debugCanvas.DrawPoint(ind, new StrokeStyle(Color.DarkSlateGray, 10));
               //               }

               //               foreach (var p in crossoverPointManager.CrossoverPoints) debugCanvas.DrawPoint(p, new StrokeStyle(Color.DarkSlateGray, 10));


               //               terrainNode.CrossoverPointManager.CrossoverPoints

               continue;

               var punchedLand = localGeometryView.PunchedLand;
               var s = new Stack<PolyNode>();
               punchedLand.Childs.ForEach(s.Push);
               while (s.Any()) {
                  var landNode = s.Pop();
                  foreach (var subLandNode in landNode.Childs.SelectMany(child => child.Childs)) s.Push(subLandNode);

                  var visibilityGraph = landNode.ComputeVisibilityGraph();
                  debugCanvas.DrawVisibilityGraph(visibilityGraph);

//                  var visibilityGraphNodeData = landNode.visibilityGraphNodeData;
//                  if (visibilityGraphNodeData.EdgeDescriptions == null) {
//                     continue;
//                  }
//                  
//                  foreach (var c in visibilityGraphNodeData.EdgeDescriptions) {
//                     debugCanvas.DrawLine(c.SourceSegment.First, c.SourceSegment.Second, new StrokeStyle(Color.Red, 5));
//                  }

//                  debugCanvas.DrawVisibilityPolygon(landNode.ComputeWaypointVisibilityPolygons()[25]);

                  //                  var colors = new[] { Color.Lime, Color.Orange, Color.Cyan, Color.Magenta, Color.Yellow, Color.Pink };
                  //                  for (int crossoverIndex = 0; crossoverIndex < visibilityGraphNodeData.ErodedCrossoverSegments.Count; crossoverIndex++) {
                  //                     var erodedCrossoverSegment = visibilityGraphNodeData.ErodedCrossoverSegments[crossoverIndex];
                  //                     var destinations = visibilityGraphNodeData.ErodedCrossoverSegments.Select((seg, i) => (seg, i != crossoverIndex)).Where(t => t.Item2)
                  //                                                               .SelectMany(t => t.Item1.Points)
                  //                                                               .Select(visibilityGraph.IndicesByWaypoint.Get)
                  //                                                               .ToArray();
                  //                     var dijkstras = visibilityGraph.Dijkstras(erodedCrossoverSegment.Points, destinations);
                  //                     for (var i = 0; i < visibilityGraph.Waypoints.Length; i++) {
                  //                        //                     if (double.IsNaN(dijkstras[i].TotalCost)) {
                  //                        //                        continue;
                  //                        //                     }
                  ////                        debugCanvas.DrawText(((int)dijkstras[i].TotalCost).ToString(), visibilityGraph.Waypoints[i]);
                  ////                        debugCanvas.DrawLine(visibilityGraph.Waypoints[i], visibilityGraph.Waypoints[dijkstras[i].PriorIndex], new StrokeStyle(colors[crossoverIndex], 5));
                  //                     }
                  //
                  //                     foreach (var vg in landNode.ComputeWaypointVisibilityPolygons()) {
                  //                        //                     debugCanvas.DrawLineOfSight(vg);
                  //                     }
                  //
                  //                     var crossoverSeeingWaypoints = landNode.ComputeSegmentSeeingWaypoints(visibilityGraphNodeData.EdgeDescriptions[crossoverIndex]);
                  //                     Console.WriteLine(crossoverSeeingWaypoints.Length);
                  ////                     landNode.ComputeWaypointVisibilityPolygons()[19].Insert(new IntLineSegment2(new IntVector2(-3000, 3000), new IntVector2(-3000, -3000)));
                  //                     foreach (var waypointIndex in crossoverSeeingWaypoints) {
                  ////                        debugCanvas.DrawLineOfSight(landNode.ComputeWaypointVisibilityPolygons()[waypointIndex]);
                  //                        //                        debugCanvas.FillPolygon(new[] { visibilityGraph.Waypoints[waypointIndex], crossover.First, crossover.Second }, new FillStyle(Color.FromArgb(150, colors[crossoverIndex])));
                  //                        //                        debugCanvas.DrawLine(visibilityGraph.Waypoints[waypointIndex], crossover.First, new StrokeStyle(colors[crossoverIndex], 5));
                  //                        //                        debugCanvas.DrawLine(visibilityGraph.Waypoints[waypointIndex], crossover.Second, new StrokeStyle(colors[crossoverIndex], 5));
                  //                     }
                  //
                  //                     var crossover = visibilityGraphNodeData.SectorSnapshot.SourceSegmentEdgeDescriptions[crossoverIndex];
                  ////                     var localToRemote = crossover.LocalToRemote;
                  ////                     var remoteGeometryContext = erodedView.GetGeometryContext(crossover.Remote);
                  ////                     remoteGeometryContext.PunchedLand.PickDeepestPolynode(crossover.RemoteSegment.ComputeMidpoint(), out PolyNode remotePolyNode, out bool isCrossoverEndpointInHole);
                  ////                     Trace.Assert(!isCrossoverEndpointInHole);
                  //
                  ////                     var remoteBarriers = remotePolyNode.FindContourAndChildHoleBarriers();
                  ////                     foreach (var barrier in remoteBarriers) {
                  ////                        var first = Vector2.Transform(barrier.First.ToDotNetVector(), crossover.RemoteToLocal).ToOpenMobaVector();
                  ////                        var second = Vector2.Transform(barrier.Second.ToDotNetVector(), crossover.RemoteToLocal).ToOpenMobaVector();
                  ////                        var midpoint = (first + second) / 2;
                  ////                        debugCanvas.DrawLine(first, second, new StrokeStyle(Color.Black));
                  ////                     }
                  //                  }
               }
            }

            //            foreach (var sectorSnapshot in terrainSnapshot.SectorSnapshots) {
            //               debugCanvas.Transform = sectorSnapshot.WorldTransform;
            //
            //               var s = new Stack<PolyNode>();
            //               sectorSnapshot.ComputePunchedLand(holeDilationRadius).Childs.ForEach(s.Push);
            //               while (s.Any()) {
            //                  var landNode = s.Pop();
            //                  foreach (var subLandNode in landNode.Childs.SelectMany(child => child.Childs)) {
            //                     s.Push(subLandNode);
            //                  }
            //
            //                  if (landNode.visibilityGraphNodeData.CrossoverSnapshots == null) {
            //                     continue;
            //                  }
            //                  foreach (var crossover in landNode.visibilityGraphNodeData.CrossoverSnapshots) {
            //                     debugCanvas.DrawLine(crossover.LocalSegment.First, crossover.LocalSegment.Second, new StrokeStyle(Color.Red, 5));
            //                  }
            //               }
            //            }
            //            DrawEntities(debugCanvas);
         });
      }

      private void DrawEntityPaths(IDebugCanvas debugCanvas) {
         foreach (var entity in EntityService.EnumerateEntities()) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent == null) continue;
            var pathPoints = new[] { movementComponent.Position }.Concat(entity.MovementComponent.PathingBreadcrumbs).ToList();
            debugCanvas.DrawLineStrip(pathPoints, PathStroke);
         }
      }

      private void DrawEntities(IDebugCanvas debugCanvas) {
         foreach (var entity in EntityService.EnumerateEntities()) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent != null) {
               debugCanvas.DrawPoint(movementComponent.Position, new StrokeStyle(Color.Black, 2 * movementComponent.BaseRadius));
               debugCanvas.DrawPoint(movementComponent.Position, new StrokeStyle(Color.White, 2 * movementComponent.BaseRadius - 2));

               if (movementComponent.Swarm != null && movementComponent.WeightedSumNBodyForces.Norm2D() > GeometryOperations.kEpsilon) {
                  var direction = movementComponent.WeightedSumNBodyForces.ToUnit() * movementComponent.BaseRadius;
                  var to = movementComponent.Position + new DoubleVector3(direction.X, direction.Y, 0.0);
                  debugCanvas.DrawLine(movementComponent.Position, to, new StrokeStyle(Color.Gray));
               }

               if (movementComponent.DebugLines != null)
                  debugCanvas.DrawLineList(
                     movementComponent.DebugLines.SelectMany(pair => new[] { pair.Item1, pair.Item2 }).ToList(),
                     new StrokeStyle(Color.Black));
            }
         }
      }

      //      private void DrawHighlightedEntityTriangles(SectorSnapshot sectorSnapshot, DebugCanvas debugCanvas) {
      //         foreach (var entity in EntityService.EnumerateEntities()) {
      //            var movementComponent = entity.MovementComponent;
      //            if (movementComponent != null) {
      //               var triangulation = sectorSnapshot.GetGeometryContext(movementComponent.BaseRadius).Triangulation;
      //               TriangulationIsland island;
      //               int triangleIndex;
      //               if (triangulation.TryIntersect(movementComponent.Position.X, movementComponent.Position.Y, out island, out triangleIndex)) {
      //                  debugCanvas.DrawTriangle(island.Triangles[triangleIndex], HighlightStroke);
      //               }
      //            }
      //         }
      //      }

      private void DrawTestPathfindingQueries(IDebugCanvas debugCanvas, double holeDilationRadius) {
         var testPathFindingQueries = new[] {
            //            Tuple.Create(new DoubleVector3(-600, 300, 0), new DoubleVector3(950, 950, 0)),
//            Tuple.Create(new DoubleVector3(900, 750, 0), new DoubleVector3(2100, 800, 0))
            Tuple.Create(new DoubleVector3(-1200, -200, 0), new DoubleVector3(1000, 0, 0))
//            Tuple.Create(new DoubleVector3(200, 700, 0), new DoubleVector3(2200, 200, 0))
            //            Tuple.Create(new DoubleVector3(60, 40, 0), new DoubleVector3(930, 300, 0)),
            //            Tuple.Create(new DoubleVector3(675, 175, 0), new DoubleVector3(825, 300, 0)),
            //            Tuple.Create(new DoubleVector3(50, 900, 0), new DoubleVector3(950, 475, 0)),
            //            Tuple.Create(new DoubleVector3(50, 500, 0), new DoubleVector3(80, 720, 0))
         };

         foreach (var query in testPathFindingQueries) {
            DrawPathfindingQueryResult(debugCanvas, holeDilationRadius, query.Item1, query.Item2);
         }
      }

      private void DrawPathfindingQueryResult(IDebugCanvas debugCanvas, double holeDilationRadius, DoubleVector3 source, DoubleVector3 dest) {
         if (Game.PathfinderCalculator.TryFindPath(holeDilationRadius, source, dest, out var roadmap)) {
            Console.WriteLine("Yippee ");
            foreach (var action in roadmap.Plan) {
               switch (action) {
                  case MotionRoadmapWalkAction walk:
                     debugCanvas.Transform = Matrix4x4.Identity;
                     var s = Vector3.Transform(new Vector3(walk.Source.X, walk.Source.Y, 0), walk.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                     var t = Vector3.Transform(new Vector3(walk.Destination.X, walk.Destination.Y, 0), walk.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                     Console.WriteLine("S: " + s + "\t AND T: " + t);
                     for (var i = 0; i < 100; i++) {
                        debugCanvas.DrawPoint((s * (100 - i) + t * i) / 100, new StrokeStyle(Color.Cyan, 50));
                     }
//                     debugCanvas.DrawLine(s, t, PathStroke);
                     break;
               }
            }
         } else {
            Console.WriteLine("Nope");
            debugCanvas.Transform = Matrix4x4.Identity;
            debugCanvas.DrawLine(source, dest, NoPathStroke);
         }
      }

      public static GameDebugger AttachToWithSoftwareRendering(Game game) {
         var rotation = 95 * Math.PI / 180.0;
         var scale = 1.0f;
         var displaySize = new Size((int)(1400 * scale), (int)(700 * scale));
			var center = new DoubleVector3(0, 0, 0);
//         var center = new DoubleVector3(1500, 1500, 0);
         var projector = new PerspectiveProjector(
            center + DoubleVector3.FromRadiusAngleAroundXAxis(1000, rotation),
//				center + DoubleVector3.FromRadiusAngleAroundXAxis(200, rotation),
            center,
            DoubleVector3.FromRadiusAngleAroundXAxis(1, rotation - Math.PI / 2),
            displaySize.Width,
            displaySize.Height);
         //         projector = null;
         //         var debugMultiCanvasHost = new MonoGameCanvasHost();
         var debugMultiCanvasHost = Debugging.DebugMultiCanvasHost.CreateAndShowCanvas(
            displaySize,
            new Point(100, 100),
            projector);
         return AttachTo(game, debugMultiCanvasHost);
      }

      public static GameDebugger AttachToWithHardwareRendering(Game game) {
         var debugMultiCanvasHost = Canvas3DDebugMultiCanvasHost.CreateAndShowCanvas(new Size(1280, 720));
         return AttachTo(game, debugMultiCanvasHost);
      }

      public static GameDebugger AttachTo(Game game, IDebugMultiCanvasHost debugMultiCanvasHost) {
         var debugger = new GameDebugger(game, debugMultiCanvasHost);
         game.Debuggers.Add(debugger);
         return debugger;

      }
   }
}
