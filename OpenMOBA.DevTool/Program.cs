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
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Geometry;
using SharpDX;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Vector3 = System.Numerics.Vector3;

namespace OpenMOBA.DevTool {
   public static class Program {
      public static void Main(string[] args) {
         var gameFactory = new GameFactory();
//         gameFactory.GameCreated += (s, game) => { GameDebugger.AttachToWithSoftwareRendering(game); };
         gameFactory.GameCreated += (s, game) => { GameDebugger.AttachToWithHardwareRendering(game); };
         OpenMOBA.Program.Main(gameFactory);
      }
   }

   public class GameDebugger : IGameDebugger {
      private static readonly StrokeStyle PathStroke = new StrokeStyle(Color.Lime, 1.0);
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
         if (frameStatistics.EventsProcessed != 0 || GameTimeService.Ticks % 32 == 0) RenderDebugFrame();
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
         var agentRadius = 0; //28.005;

         var terrainSnapshot = TerrainService.CompileSnapshot();
         var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(agentRadius);

         var debugCanvas = DebugMultiCanvasHost.CreateAndAddCanvas(GameTimeService.Ticks);
         debugCanvas.BatchDraw(() => {
            debugCanvas.Transform = Matrix4x4.Identity;
            //debugCanvas.FillPolygonTriangulation(Polygon2.CreateRect(-3500, -1500, 7000, 3000), new FillStyle(Color.Black));
            RenderHook?.Invoke(this, debugCanvas);
            if (RenderHook != null) return;

            debugCanvas.Transform = Matrix4x4.Identity;
            DrawEntityPaths(debugCanvas);
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
               var fillColor = colors[(index / colors.Length) % colors.Length];
               //debugCanvas.DrawPolyNode(terrainNode.LocalGeometryView.ComputeErodedOuterContour(), StrokeStyle.BlackHairLineSolid, StrokeStyle.CyanThick3Solid);
               //debugCanvas.DrawPolyNode(terrainNode.LocalGeometryView.DilatedHolesUnion, StrokeStyle.RedHairLineSolid, StrokeStyle.LimeThick5Solid);
               debugCanvas.DrawPolyNode(terrainNode.LocalGeometryView.PunchedLand, new StrokeStyle(Color.Gray));
               
               foreach (var ((desc, version), (includedContours, excludedContours)) in terrainNode.LocalGeometryView.Job.DynamicHoles) {
                  debugCanvas.DrawPolygonContours(includedContours, StrokeStyle.RedHairLineSolid);
                  debugCanvas.DrawPolygonContours(excludedContours, StrokeStyle.LimeHairLineSolid);
               }
               continue;
//               if (index == 0)
               //debugCanvas.DrawPolygonContours(terrainNode.LocalGeometryView.ComputeCrossoverLandPolys().ToList(), StrokeStyle.LimeHairLineSolid);
               //debugCanvas.DrawLineList(localGeometryView.Job.CrossoverSegments.ToArray(), StrokeStyle.CyanHairLineSolid);
               //debugCanvas.DrawPoints(terrainNode.CrossoverPointManager.CrossoverPoints, StrokeStyle.RedThick10Solid);

               debugCanvas.Transform = Matrix4x4.Identity;

               foreach (var (i, entity) in Game.EntityService.EnumerateEntities().Enumerate()) {
                  var mc = entity.MovementComponent;
                  var ton = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(2);
                  if (!ton.TryFindTerrainOverlayNode(mc.WorldPosition, out var tonn, out var plocal)) continue;
                  if (i == 2 || i == 1) continue;
                  var color = Color.FromArgb(20, new[] {
                     Color.Red,
                     Color.Magenta,
                     Color.Yellow,
                     Color.Lime,
                     Color.Blue,
                  }[i]);
                  debugCanvas.DrawCrossSectorVisibilityPolygon(tonn, new IntVector2((int)plocal.X, (int)plocal.Y), new FillStyle(color));
               }

               debugCanvas.Transform = sectorNodeDescription.WorldTransform;
            }
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
               for (var i = bvh.StartIndexInclusive; i < bvh.EndIndexExclusive; i++) {
                  debugCanvas.DrawAxisAlignedBoundingBox(bvh.BoundingBoxes[i], new StrokeStyle(Color.Black, 3));
               }
            }
         }

         Helper(bvhRoot);
      }

      private void DrawEntities(IDebugCanvas debugCanvas) {
         foreach (var (i, entity) in EntityService.EnumerateEntities().Enumerate()) {
//            if (i == 2 || i == 1) continue;
            var movementComponent = entity.MovementComponent;
            if (movementComponent != null) {
               debugCanvas.Transform = Matrix4x4.Identity;
               debugCanvas.DrawPoint(movementComponent.WorldPosition, new StrokeStyle(Color.Black, 2 * movementComponent.BaseRadius));
               debugCanvas.DrawPoint(movementComponent.WorldPosition, new StrokeStyle(Color.White, 2 * movementComponent.BaseRadius - 2));

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
               var terrainOverlayNetwork = TerrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(entity.MovementComponent.BaseRadius);
               if (terrainOverlayNetwork.TryFindTerrainOverlayNode(movementComponent.WorldPosition, out var node, out var plocal)) {
                  debugCanvas.Transform = node.SectorNodeDescription.WorldTransform;
                  //debugCanvas.DrawTriangulation(node.LocalGeometryView.Triangulation, StrokeStyle.BlackHairLineSolid);
                  if (node.LocalGeometryView.Triangulation.TryIntersect(plocal.X, plocal.Y, out var island, out var triangleIndex)) {
                     debugCanvas.DrawTriangle(island.Triangles[triangleIndex], StrokeStyle.RedHairLineSolid);
                  }
               }
            }
         }
      }

      private void DrawEntityPaths(IDebugCanvas debugCanvas) {
         foreach (var (i, entity) in EntityService.EnumerateEntities().Enumerate()) {
            //            if (i == 2 || i == 1) continue;
            var movementComponent = entity.MovementComponent;
            if (movementComponent?.PathingRoadmap == null || movementComponent?.Swarm != null) continue;
            DrawRoadmap(debugCanvas, movementComponent.PathingRoadmap, movementComponent);
         }
      }

      private void DrawEntityMotionVectors(IDebugCanvas debugCanvas) {
         debugCanvas.Transform = Matrix4x4.Identity;
         foreach (var (i, entity) in EntityService.EnumerateEntities().Enumerate()) {
            var mc = entity.MovementComponent;
            if (mc?.Swarm != null) {
               var local = mc.LocalPosition;
               var motionVectorUnnormalized = mc.WeightedSumNBodyForces;
               if (motionVectorUnnormalized.Norm2D() < 1E-9) continue;
               var v = motionVectorUnnormalized.ToUnit() * mc.TerrainOverlayNetworkNode.SectorNodeDescription.WorldToLocalScalingFactor * 100;
               var goalWorld = mc.TerrainOverlayNetworkNode.SectorNodeDescription.LocalToWorld(local + v);
               debugCanvas.DrawLine(mc.WorldPosition, goalWorld, StrokeStyle.RedHairLineSolid);
            }
         }
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

         for (var i = 0; i < 4; i++) {
            if (pathfinderResultContext.TryComputeRoadmap(i, out var roadmap)) {
               DrawRoadmap(debugCanvas, roadmap);
            }

//            if (prc2.TryComputeRoadmap(i, out roadmap)) {
//               DrawRoadmap(debugCanvas, roadmap);
//            }
         }
         return;

         var testPathFindingQueries = new[] {
            //            Tuple.Create(new DoubleVector3(-600, 300, 0), new DoubleVector3(950, 950, 0)),
//            Tuple.Create(new DoubleVector3(900, 750, 0), new DoubleVector3(2100, 800, 0))
            Tuple.Create(new DoubleVector3(1250, -80, 0), new DoubleVector3(-800, 300, 0)),
//            Tuple.Create(new DoubleVector3(-800, 300, 0), new DoubleVector3(1250, -80, 0))
//            Tuple.Create(new DoubleVector3(200, 700, 0), new DoubleVector3(2200, 200, 0))
            //            Tuple.Create(new DoubleVector3(60, 40, 0), new DoubleVector3(930, 300, 0)),
            //            Tuple.Create(new DoubleVector3(675, 175, 0), new DoubleVector3(825, 300, 0)),
            //            Tuple.Create(new DoubleVector3(50, 900, 0), new DoubleVector3(950, 475, 0)),
            //            Tuple.Create(new DoubleVector3(50, 500, 0), new DoubleVector3(80, 720, 0))
         };

         foreach (var query in testPathFindingQueries) {
            DrawPathfindingQueryResult(debugCanvas, agentRadius, query.Item1, query.Item2);
         }
      }

      private void DrawPathfindingQueryResult(IDebugCanvas debugCanvas, double agentRadius, DoubleVector3 source, DoubleVector3 dest) {
         if (Game.PathfinderCalculator.TryFindPath(agentRadius, source, dest, out var roadmap)) {
            Console.WriteLine("Yippee ");
            DrawRoadmap(debugCanvas, roadmap);
         } else {
            Console.WriteLine("Nope");
            debugCanvas.Transform = Matrix4x4.Identity;
            debugCanvas.DrawLine(source, dest, NoPathStroke);
         }
      }

      private static void DrawRoadmap(IDebugCanvas debugCanvas, MotionRoadmap roadmap, MovementComponent movementComponent = null) {
         var skip = movementComponent?.PathingRoadmapProgressIndex ?? 0;
         foreach (var (i, action) in roadmap.Plan.Skip(skip).Enumerate()) {
            switch (action) {
               case MotionRoadmapWalkAction walk:
                  debugCanvas.Transform = Matrix4x4.Identity;
                  var s = i == 0 && movementComponent != null
                     ? movementComponent.WorldPosition
                     : Vector3.Transform(new Vector3(walk.Source.X, walk.Source.Y, 0), walk.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                  var t = Vector3.Transform(new Vector3(walk.Destination.X, walk.Destination.Y, 0), walk.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
//                     Console.WriteLine("S: " + s + "\t AND T: " + t);
//                     for (var i = 0; i < 100; i++) {
//                        debugCanvas.DrawPoint((s * (100 - i) + t * i) / 100, new StrokeStyle(Color.Cyan, 50));
//                     }
                  debugCanvas.DrawLine(s, t, PathStroke);
                  break;
            }
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

