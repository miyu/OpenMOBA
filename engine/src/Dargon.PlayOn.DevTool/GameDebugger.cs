using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Dviz;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.DevTool {
   public class GameDebugger : GameEventListener {
      public delegate void RenderHookEvent(GameDebugger debugger, IDebugCanvas canvas);

      private static readonly StrokeStyle PathStroke2 = new StrokeStyle(Color.DarkGreen, 15.0);
      private static readonly StrokeStyle NoPathStroke = new StrokeStyle(Color.Red, 15.0, new[] { 1.0f, 1.0f });
      private static readonly StrokeStyle HighlightStroke = new StrokeStyle(Color.Red, 3.0);

      public RenderHookEvent RenderHook;

      public GameDebugger(Game game, IDebugMultiCanvasHost debugMultiCanvasHost) {
         Game = game;
         DebugMultiCanvasHost = debugMultiCanvasHost;
      }

      public Game Game { get; }
      public IDebugMultiCanvasHost DebugMultiCanvasHost { get; }

      private GameTimeManager GameTimeManager => Game.GameTimeManager;
      private TerrainFacade TerrainFacade => Game.TerrainFacade;
      private EntityWorld EntityWorld => Game.EntityWorld;

      public override void HandleLeaveTick(LeaveTickStatistics statistics) {
         if (statistics.EventsProcessed != 0 || GameTimeManager.Ticks % 1 == 0) RenderDebugFrame();
      }

      private void Benchmark(double holeDilationRadius) {
         void RunBenchmarkIteration() {
            TerrainFacade.SnapshotCompiler.InvalidateCaches();
            TerrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);
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

      private void RenderDebugFrame() {
         var agentRadius = (double)0.1f; //10; //28.005;

         var terrainSnapshot = TerrainFacade.CompileSnapshot();
         var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(agentRadius);

         var debugCanvas = DebugMultiCanvasHost.CreateAndAddCanvas(GameTimeManager.Ticks);
         debugCanvas.BatchDraw(() => {
            debugCanvas.Transform = Matrix4x4.Identity;
            //            debugCanvas.FillPolygonTriangulation(Polygon2.CreateRect(-3500, -1500, 7000, 3000), new FillStyle(Color.Black));
            RenderHook?.Invoke(this, debugCanvas);
            if (RenderHook != null) return;

            debugCanvas.Transform = Matrix4x4.Identity;
            debugCanvas.DrawEntityPaths(EntityWorld);
            DrawEntities(debugCanvas);
            //            DrawEntityMotionVectors(debugCanvas);
            //DrawTestPathfindingQueries(debugCanvas, 0.0);

            debugCanvas.Transform = Matrix4x4.Identity;
            //            if (MovementSystemService.RenderMe != null) {
            //               foreach (var pathfinderResultContext in MovementSystemService.RenderMe) {
            //                  for (var i = 0; i < pathfinderResultContext.Destinations.Length; i++) {
            //                     if (pathfinderResultContext.TryComputeRoadmap(i, out var roadmap)) {
            //                        DrawRoadmap(debugCanvas, roadmap);
            //                     }
            //                  }
            //               }
            //            }

            var colors = new[] { Color.White };
            //			   var colors = new[] { Color.Red, Color.Lime, Color.Cyan, Color.Magenta, Color.Yellow, Color.Orange, Color.Blue, Color.Indigo, Color.Violet };
            foreach (var (index, terrainNode) in terrainOverlayNetwork.TerrainNodes.Enumerate()) {
               var sectorNodeDescription = terrainNode.SectorNodeDescription;
               var localGeometryView = terrainNode.LocalGeometryView;
               var landPolyNode = terrainNode.LandPolyNode;
               var crossoverPointManager = terrainNode.CrossoverPointManager;

               debugCanvas.Transform = Matrix4x4.Identity;
               debugCanvas.Transform = sectorNodeDescription.WorldTransform;
               var fillColor = colors[index / colors.Length % colors.Length];
               //debugCanvas.DrawPolyNode(terrainNode.LocalGeometryView.ComputeErodedOuterContour(), StrokeStyle.BlackHairLineSolid, StrokeStyle.CyanThick3Solid);
               //debugCanvas.DrawPolyNode(terrainNode.LocalGeometryView.DilatedHolesUnion, StrokeStyle.RedHairLineSolid, StrokeStyle.LimeThick5Solid);
               // debugCanvas.DrawPolyNode(terrainNode.LocalGeometryView.PunchedLand, new StrokeStyle(Color.Gray));
               debugCanvas.FillTriangulation(terrainNode.LocalGeometryView.Triangulation, new FillStyle(Color.White));
               debugCanvas.DrawTriangulation(terrainNode.LocalGeometryView.Triangulation, new StrokeStyle(Color.Black));

               foreach (var ((desc, version), (includedContours, excludedContours)) in terrainNode.LocalGeometryView.Job.DynamicHoles) {
                  debugCanvas.DrawPolygonContours(includedContours, StrokeStyle.RedHairLineSolid);
                  debugCanvas.DrawPolygonContours(excludedContours, StrokeStyle.LimeHairLineSolid);
               }
               //               if (index == 0)
               debugCanvas.DrawPolygonContours(terrainNode.LocalGeometryView.ComputeCrossoverLandPolys().ToList(), StrokeStyle.LimeHairLineSolid);
               debugCanvas.DrawLineList(localGeometryView.Job.CrossoverSegments.Select(x => x.segment).ToArray(), StrokeStyle.CyanHairLineSolid);
               debugCanvas.DrawPoints(terrainNode.CrossoverPointManager.CrossoverPoints, StrokeStyle.RedThick10Solid);
               continue;

               debugCanvas.Transform = Matrix4x4.Identity;

               foreach (var (i, entity) in Game.EntityWorld.EnumerateEntities().Enumerate()) {
                  var mc = entity.MotionComponent;
                  var ton = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(CDoubleMath.c2);
                  if (!ton.TryFindTerrainOverlayNode(mc.Internals.Pose.WorldPosition, out var tonn, out var plocal)) continue;
                  if (i == 2 || i == 1) continue;
                  var color = Color.FromArgb(20, new[] {
                     Color.Red,
                     Color.Magenta,
                     Color.Yellow,
                     Color.Lime,
                     Color.Blue
                  }[i]);
                  debugCanvas.DrawCrossSectorVisibilityPolygon(tonn, new IntVector2((int)plocal.X, (int)plocal.Y), new FillStyle(color));
               }

               debugCanvas.Transform = sectorNodeDescription.WorldTransform;
            }

            DrawTestVectorWalkQueries(debugCanvas);
         });
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
            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygonAndIsHoles();
         Console.WriteLine("NI: AddSquiggleHole");
         throw new NotImplementedException();
      }

      private void DrawBvhAABB<TValue>(IDebugCanvas debugCanvas, BvhTreeAABB<TValue> bvhRoot) {
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
               for (var i = bvh.StartIndexInclusive; i < bvh.EndIndexExclusive; i++) debugCanvas.DrawAxisAlignedBoundingBox(bvh.BoundingBoxes[i], new StrokeStyle(Color.Black, 3));
            }
         }

         Helper(bvhRoot);
      }

      private void DrawEntities(IDebugCanvas debugCanvas) {
         foreach (var (i, entity) in EntityWorld.EnumerateEntities().Enumerate()) {
            //            if (i == 2 || i == 1) continue;
            var movementComponent = entity.MotionComponent;
            if (movementComponent != null) {
               debugCanvas.Transform = Matrix4x4.Identity;
               debugCanvas.DrawPoint(movementComponent.Internals.Pose.WorldPosition, new StrokeStyle(Color.Black, 2 * (float)movementComponent.BaseStatistics.Radius));
               debugCanvas.DrawPoint(movementComponent.Internals.Pose.WorldPosition, new StrokeStyle(Color.White, 2 * (float)movementComponent.BaseStatistics.Radius - 2));

               //               if (movementComponent.Swarm != null && movementComponent.WeightedSumNBodyForces.Norm2D() > GeometryOperations.kEpsilon) {
               //                  var direction = movementComponent.WeightedSumNBodyForces.ToUnit() * movementComponent.BaseRadius;
               //                  var to = movementComponent.WorldPosition + new DoubleVector3(direction.X, direction.Y, 0.0);
               //                  debugCanvas.DrawLine(movementComponent.WorldPosition, to, new StrokeStyle(Color.Gray));
               //               }

               //if (movementComponent.DebugLines != null)
               //   debugCanvas.DrawLineList(
               //      movementComponent.DebugLines.SelectMany(pair => new[] { pair.Item1, pair.Item2 }).ToList(),
               //      new StrokeStyle(Color.Black));
               continue;
               var terrainOverlayNetwork = TerrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(entity.MotionComponent.BaseStatistics.Radius);
               if (terrainOverlayNetwork.TryFindTerrainOverlayNode(movementComponent.Internals.Pose.WorldPosition, out var node, out var plocal)) {
                  debugCanvas.Transform = node.SectorNodeDescription.WorldTransform;
                  //debugCanvas.DrawTriangulation(node.LocalGeometryView.Triangulation, StrokeStyle.BlackHairLineSolid);
                  if (node.LocalGeometryView.Triangulation.TryIntersect(plocal.X, plocal.Y, out var island, out var triangleIndex)) debugCanvas.DrawTriangle(island.Triangles[triangleIndex], StrokeStyle.RedHairLineSolid);
               }
            }
         }
      }

      private void DrawEntityMotionVectors(IDebugCanvas debugCanvas) {
         debugCanvas.Transform = Matrix4x4.Identity;
         throw new NotImplementedException("Force vectors no longer accessible? Maybe expose from flocking simulator.");
         // foreach (var (i, entity) in EntityWorld.EnumerateEntities().Enumerate()) {
         //    var mc = entity.MotionComponent;
         //    if (mc?.Internals.Swarm != null) {
         //       var local = mc.Internals.Localization.LocalPosition;
         //       var motionVectorUnnormalized = mc.Internals.Steering.CurrentUpdateForceContributions.Aggregate.SumForces;
         //       if (motionVectorUnnormalized.Norm2D() < CDoubleMath.Epsilon) continue;
         //       var v = motionVectorUnnormalized.ToUnit() * mc.Localization.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor * 100;
         //       var goalWorld = mc.Localization.TerrainOverlayNetworkNode.SectorNodeDescription.LocalToWorld(local + v);
         //       debugCanvas.DrawLine(mc.Pose.WorldPosition, goalWorld, StrokeStyle.RedHairLineSolid);
         //    }
         // }
      }

      private void DrawTestPathfindingQueries(IDebugCanvas debugCanvas, double agentRadius) {
         Console.WriteLine("!@#@!#!@#!@#!");
         var pathfinderResultContext = Game.PathfinderCalculator.UniformCostSearch(
            agentRadius,
            new DoubleVector3(-800, 300, 0),
            new[] {
               new DoubleVector3(-1200, 300, 0),
               new DoubleVector3(-1250, 0, 0),
               new DoubleVector3(1250, -80, 0),
               new DoubleVector3(1250, -280, 0)
            },
            true,
            null,
            debugCanvas);

         Console.WriteLine("!@!@#!#@#!@#!@!@!@!@#!#@!#!@#!@#!");

         //         var prc2 = Game.PathfinderCalculator.UniformCostSearch(
         //            agentRadius,
         //            new DoubleVector3(-800, 300, 0),
         //            new[] {
         //               new DoubleVector3(-1220, 330, 0),
         //               new DoubleVector3(-1250, -300, 0),
         //               new DoubleVector3(1290, -80, 0),
         //               new DoubleVector3(1250, -380, 0)
         //            },
         //            true,
         //            pathfinderResultContext);
         Console.WriteLine("!@#@");

         for (var i = 0; i < 4; i++) if (pathfinderResultContext.TryComputeRoadmap(i, out var roadmap)) debugCanvas.DrawRoadmap(roadmap);

         //            if (prc2.TryComputeRoadmap(i, out roadmap)) {
         //               DrawRoadmap(debugCanvas, roadmap);
         //            }
         return;

         var testPathFindingQueries = new[] {
            //            Tuple.Create(new DoubleVector3(-600, 300, 0), new DoubleVector3(950, 950, 0)),
            //            Tuple.Create(new DoubleVector3(900, 750, 0), new DoubleVector3(2100, 800, 0))
            Tuple.Create(new DoubleVector3(1250, -80, 0), new DoubleVector3(-800, 300, 0))
            //            Tuple.Create(new DoubleVector3(-800, 300, 0), new DoubleVector3(1250, -80, 0))
            //            Tuple.Create(new DoubleVector3(200, 700, 0), new DoubleVector3(2200, 200, 0))
            //            Tuple.Create(new DoubleVector3(60, 40, 0), new DoubleVector3(930, 300, 0)),
            //            Tuple.Create(new DoubleVector3(675, 175, 0), new DoubleVector3(825, 300, 0)),
            //            Tuple.Create(new DoubleVector3(50, 900, 0), new DoubleVector3(950, 475, 0)),
            //            Tuple.Create(new DoubleVector3(50, 500, 0), new DoubleVector3(80, 720, 0))
         };

         foreach (var query in testPathFindingQueries) DrawPathfindingQueryResult(debugCanvas, agentRadius, query.Item1, query.Item2);
      }

      private void DrawTestVectorWalkQueries(IDebugCanvas debugCanvas) {
         var computedRadius = 0.1f;
         var ton = Game.TerrainFacade.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(computedRadius);

         var colors = new[] { Color.Red, Color.Orange, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta };

         for (var i = 0; i < 20; i++) {
            //if (i < 15) continue;
            if (i != 19) continue;
            var r = new Random(i);
            var p = 1 * (new DoubleVector3(r.NextDouble() * 2 - 1 - 1, r.NextDouble() * 2 - 1 - 3, 0.06));
            if (!ton.TryFindPreciseLocalization(p, 0, out var loc)) {
               Console.WriteLine(ton.TerrainNodes.First().SectorNodeDescription.LocalToWorld(DoubleVector2.Zero));
               continue;
            }

            // var dir = -DoubleVector3.UnitX;// (new DoubleVector3(r.NextDouble() - 1, r.NextDouble(), r.NextDouble()) * 2.0 - DoubleVector3.One).ToUnit();
            var dir = new DoubleVector3(-0.999880909919739, 0.015432508662343, 1.83135271072388E-05);

            debugCanvas.Transform = Matrix4x4.Identity;
            var stroke = new StrokeStyle(Color.FromArgb(220, colors[i % colors.Length]), 0.05f);
            var delta = 0.0999999f; //0.03f;
            var distanceToMove = Game.GameTimeManager.Ticks * delta;

            // debugCanvas.Transform = loc.TerrainOverlayNetworkNode.SectorNodeDescription.WorldTransform;
            // for (var ti = 0; ti < loc.TriangulationIsland.Triangles.Length; ti++) {
            //    var t = loc.TriangulationIsland.Triangles[ti];
            //    debugCanvas.DrawText(ti.ToString(), t.Centroid.LossyToIntVector2());
            //    debugCanvas.DrawText(ti + "A", ((t.Centroid + t.Points.A) / 2).LossyToIntVector2());
            //    debugCanvas.DrawText(ti + "B", ((t.Centroid + t.Points.B) / 2).LossyToIntVector2());
            //    debugCanvas.DrawText(ti + "C", ((t.Centroid + t.Points.C) / 2).LossyToIntVector2());
            // }

            for (var step = 0; step < Game.GameTimeManager.Ticks; step++) {
               var res = Game.TriangulationWalker3D.WalkTriangulation(loc, dir, delta, debugCanvas, stroke);
               debugCanvas.Transform = loc.TerrainOverlayNetworkNode.SectorNodeDescription.WorldTransform;
               if (step + 1 == Game.GameTimeManager.Ticks) debugCanvas.DrawPoint(loc.LocalPosition, new StrokeStyle(Color.Red, 0.05f));
               // Console.WriteLine("S_A_" + step + " loc " + loc.LocalPosition + " => " + res.Item1.LocalPosition + " " + loc.LocalPosition.To(res.Item1.LocalPosition).Norm2D());
               loc = res.Item1;
               var tr = loc.TriangulationIsland.Triangles[loc.TriangleIndex];
               // Console.WriteLine("TR " + tr + " " + loc.LocalPosition + " " + loc.TriangleIndex);
               // Console.WriteLine("TR " + tr.Points[0].To(loc.LocalPosition).ProjectOntoComponentD(tr.Points[0].To(tr.Points[1])));
               // Console.WriteLine("TR " + tr.Points[0].To(loc.LocalPosition).ProjectOntoComponentD(tr.Points[0].To(tr.Points[2])));
               if (step + 1 == Game.GameTimeManager.Ticks) debugCanvas.DrawPoint(loc.LocalPosition, new StrokeStyle(Color.Lime, 0.04f));

               var worldPosition = loc.TerrainOverlayNetworkNode.SectorNodeDescription.LocalToWorld(loc.LocalPosition);
               var rloc = ton.FindNearestLandPointLocalization(worldPosition, computedRadius);
               // if (rloc.localization.LocalPosition.To(loc.LocalPosition).Norm2D() > 0.001f) {
               // Console.WriteLine("S_B_"+step + " loc " + loc.LocalPosition + " => " + rloc.localization.LocalPosition + " " + loc.LocalPosition.To(rloc.localization.LocalPosition).Norm2D());
               if (loc.LocalPosition.To(rloc.localization.LocalPosition).Norm2D() > 0.001f) Debugger.Break();
               // }
               loc = rloc.localization;
               tr = loc.TriangulationIsland.Triangles[loc.TriangleIndex];
               // Console.WriteLine("TR " + tr + " " + loc.LocalPosition + " " + loc.TriangleIndex);
               // Console.WriteLine("TR " + tr.Points[0].To(loc.LocalPosition).ProjectOntoComponentD(tr.Points[0].To(tr.Points[1])));
               // Console.WriteLine("TR " + tr.Points[0].To(loc.LocalPosition).ProjectOntoComponentD(tr.Points[0].To(tr.Points[2])));
               if (step + 1 == Game.GameTimeManager.Ticks) debugCanvas.DrawPoint(loc.LocalPosition, new StrokeStyle(Color.Gray, 0.03f));
            }
         }

         if (false) {
            if (ton.TryFindPreciseLocalization(new DoubleVector3(-450, -350, 0), 0, out var loc)) {
               Game.TriangulationWalker3D.WalkTriangulation(loc, new DoubleVector3(2, 0.4, 0).ToUnit(), Game.GameTimeManager.Ticks * 100, debugCanvas);
               Game.TriangulationWalker3D.WalkTriangulation(loc, new DoubleVector3(1.7, 0.4, 0).ToUnit(), Game.GameTimeManager.Ticks * 100, debugCanvas);
            }
         }
      }

      private void DrawPathfindingQueryResult(IDebugCanvas debugCanvas, double agentRadius, DoubleVector3 source, DoubleVector3 dest) {
         if (Game.PathfinderCalculator.TryFindPath(agentRadius, source, dest, out var roadmap)) {
            Console.WriteLine("Yippee ");
            debugCanvas.DrawRoadmap(roadmap);
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
         var center = new Vector3(-3, -4, 0);
         //         var center = new DoubleVector3(1500, 1500, 0);
         var projector = new PerspectiveProjector(
            center + Vector3s.FromRadiusAngleAroundXAxis(1, (float)rotation),
            // center + Vector3s.FromRadiusAngleAroundXAxis(5, (float)rotation),
            //				center + DoubleVector3.FromRadiusAngleAroundXAxis(200, rotation),
            center,
            Vector3s.FromRadiusAngleAroundXAxis(1, (float)(rotation - Math.PI / 2)),
            displaySize.Width,
            displaySize.Height);
         //         projector = null;
         //         var debugMultiCanvasHost = new MonoGameCanvasHost();
         var debugMultiCanvasHost = Dargon.Dviz.DebugMultiCanvasHost.CreateAndShowCanvas(
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