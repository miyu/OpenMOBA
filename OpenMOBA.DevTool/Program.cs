using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using ClipperLib;
using OpenMOBA.Debugging;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool {
   public static class Program {
      public static void Main(string[] args) {
         var gameFactory = new GameFactory();
         gameFactory.GameCreated += (s, game) => { GameDebugger.AttachToWithSoftwareRendering(game); };
         gameFactory.GameCreated += (s, game) => { GameDebugger.AttachToWithHardwareRendering(game); };
         OpenMOBA.Program.Main(gameFactory);
      }
   }

   public class GameDebugger : IGameDebugger {
      private static readonly StrokeStyle PathStroke = new StrokeStyle(Color.Lime, 15.0);
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
         if (frameStatistics.EventsProcessed != 0 || GameTimeService.Ticks % 64 == 0) RenderDebugFrame();
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

         for (var i = 0; i < 100; i++) {
            RunBenchmarkIteration();
            if (false && i == 0)
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
         }
         GC.Collect();
         var sw = new Stopwatch();
         sw.Start();
         for (var i = 0; i < 100; i++) RunBenchmarkIteration();
         Console.WriteLine("100itr: " + sw.ElapsedMilliseconds + "ms");
      }

      private void RenderDebugFrame() {
         var holeDilationRadius = 15.0;
         //Benchmark(holeDilationRadius);

         var terrainSnapshot = TerrainService.CompileSnapshot();
         var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);

         terrainOverlayNetwork.Initialize();

         var debugCanvas = DebugMultiCanvasHost.CreateAndAddCanvas(GameTimeService.Ticks);
         //         var temporaryHolePolygons = terrainSnapshot.TemporaryHoles.SelectMany(th => th.Polygons).ToList();
         debugCanvas.BatchDraw(() => {
            debugCanvas.Transform = Matrix4x4.Identity;
//            DrawTestPathfindingQueries(debugCanvas, holeDilationRadius);

            foreach (var terrainNode in terrainOverlayNetwork.TerrainNodes) {
               var sectorNodeDescription = terrainNode.SectorNodeDescription;
               var localGeometryView = terrainNode.LocalGeometryView;
               var landPolyNode = terrainNode.LandPolyNode;
               var crossoverPointManager = terrainNode.CrossoverPointManager;

               debugCanvas.Transform = sectorNodeDescription.WorldTransform;
//               debugCanvas.DrawTriangulation(localGeometryView.Triangulation, new StrokeStyle(Color.DarkGray));
               debugCanvas.FillTriangulation(localGeometryView.Triangulation, new FillStyle(Color.DarkGray));
               continue;

               //Console.WriteLine("Holes: " + localGeometryView.Job.DynamicHoles.Count);
               foreach (var (k, v) in localGeometryView.Job.DynamicHoles) {
                  debugCanvas.DrawPolygonContours(v.holeIncludedContours, StrokeStyle.RedHairLineSolid);
                  debugCanvas.DrawPolygonContours(v.holeExcludedContours, StrokeStyle.RedHairLineSolid);
               }

               debugCanvas.DrawPoints(landPolyNode.FindAggregateContourCrossoverWaypoints(), StrokeStyle.RedThick25Solid);

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

               foreach (var p in crossoverPointManager.CrossoverPoints) debugCanvas.DrawPoint(p, new StrokeStyle(Color.DarkSlateGray, 10));


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
            Tuple.Create(new DoubleVector3(200, 700, 0), new DoubleVector3(2200, 200, 0))
            //            Tuple.Create(new DoubleVector3(60, 40, 0), new DoubleVector3(930, 300, 0)),
            //            Tuple.Create(new DoubleVector3(675, 175, 0), new DoubleVector3(825, 300, 0)),
            //            Tuple.Create(new DoubleVector3(50, 900, 0), new DoubleVector3(950, 475, 0)),
            //            Tuple.Create(new DoubleVector3(50, 500, 0), new DoubleVector3(80, 720, 0))
         };

         //         var sector1 = Game.TerrainService.BuildSnapshot().SectorSnapshots[1];
         //         var p1 = new IntVector2(500, 300);
         //         var p1World = sector1.LocalToWorld(p1);
         //         sector1.GetGeometryContext(holeDilationRadius).PunchedLand.PickDeepestPolynode(p1, out PolyNode p1PolyNode, out bool p1IsInHole);
         //         debugCanvas.DrawPoint(p1World, new StrokeStyle(p1IsInHole ? Color.Green : Color.Lime, 20));
         //         var pathfindingContext = TerrainService.BuildSnapshot().GetPathfindingContext(holeDilationRadius);
         //         if (pathfindingContext.TryFindSector(sector1.LocalToWorld(p1), out SectorSnapshot sector1SnapshotHopefully)) {
         //            debugCanvas.DrawPoint(sector1SnapshotHopefully.LocalToWorld(new IntVector2(100, 100)), new StrokeStyle(Color.Lime, 20));
         //         }
         //
         //         var sector2 = Game.TerrainService.BuildSnapshot().SectorSnapshots[2];
         //         var p2 = new IntVector2(350, 320);
         //         var p2World = Vector3.Transform(new DoubleVector3(p2.ToDoubleVector2()).ToDotNetVector(), sector2.WorldTransform).ToOpenMobaVector();
         //         sector2.GetGeometryContext(holeDilationRadius).PunchedLand.PickDeepestPolynode(p2, out PolyNode p2PolyNode, out bool p2IsInHole);
         //         debugCanvas.DrawPoint(p2World, new StrokeStyle(p2IsInHole ? Color.DarkRed : Color.Red, 20));
         //         if (pathfindingContext.TryFindSector(sector2.LocalToWorld(p2), out SectorSnapshot sector2SnapshotHopefully)) {
         //            debugCanvas.DrawPoint(sector2SnapshotHopefully.LocalToWorld(new IntVector2(900, 100)), new StrokeStyle(Color.Red, 20));
         //         }
         //
         ////         double pathCostUpperBound;
         ////         if (Game.PathfinderCalculator.TryFindPathCostUpperBound(holeDilationRadius, p1, p1PolyNode, p2, p2PolyNode, out pathCostUpperBound)) {
         ////            debugCanvas.DrawLine(p1World, p2World, PathStroke);
         ////         } else {
         ////            debugCanvas.DrawLine(p1World, p2World, NoPathStroke);
         ////         }
         //         List<DoubleVector3> path;
         //         if (Game.PathfinderCalculator.TryFindPath(holeDilationRadius, new DoubleVector3(50, 50, 0), new DoubleVector3(50, 250, 0), out path)) {
         //            debugCanvas.DrawLine(p1World, p2World, PathStroke);
         //         } else {
         //            debugCanvas.DrawLine(p1World, p2World, NoPathStroke);
         //         }

         foreach (var query in testPathFindingQueries) {
            List<DoubleVector3> pathPoints;
            if (Game.PathfinderCalculator.TryFindPath(holeDilationRadius, query.Item1, query.Item2, out pathPoints)) {
               Console.WriteLine("Yippee " + string.Join(", ", pathPoints));
               debugCanvas?.DrawLineStrip(pathPoints, PathStroke);
            } else {
               Console.WriteLine("Nope");
               debugCanvas?.DrawLine(query.Item1, query.Item2, NoPathStroke);
            }
         }
      }

      public static void AttachToWithSoftwareRendering(Game game) {
         var rotation = 100 * Math.PI / 180.0;
         var scale = 1.0f;
         var displaySize = new Size((int)(1400 * scale), (int)(700 * scale));
         var center = new DoubleVector3(1500, 500, 0);
         var projector = new PerspectiveProjector(
            center + DoubleVector3.FromRadiusAngleAroundXAxis(800, rotation),
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
         AttachTo(game, debugMultiCanvasHost);
      }

      public static void AttachToWithHardwareRendering(Game game) {
         var debugMultiCanvasHost = Canvas3DDebugMultiCanvasHost.CreateAndShowCanvas(new Size(1280, 720));
         AttachTo(game, debugMultiCanvasHost);
      }

      public static void AttachTo(Game game, IDebugMultiCanvasHost debugMultiCanvasHost) {
         var debugger = new GameDebugger(game, debugMultiCanvasHost);
         game.Debuggers.Add(debugger);
      }
   }
}
