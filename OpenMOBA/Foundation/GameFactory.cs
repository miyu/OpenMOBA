using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using OpenMOBA.Utilities;

namespace OpenMOBA.Foundation {
   public interface IGameEventFactory {
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
   }

   public class Swarm {
      public List<Entity> Entities { get; set; } = new List<Entity>();
      public DoubleVector2 Destination { get; set; }
   }

   public class GameInstance : IGameEventFactory {
      public GameTimeService GameTimeService { get; set; }
      public GameEventQueueService GameEventQueueService { get; set; }
      public MapConfiguration MapConfiguration { get; set; }
      public TerrainService TerrainService { get; set; }
      public EntityService EntityService { get; set; }
      public PathfinderCalculator PathfinderCalculator { get; set; }
      public MovementSystemService MovementSystemService { get; set; }
      public GameLogicFacade GameLogicFacade { get; set; }

      public void Run() {
         var r = new Random(1);
         for (int i = 0; i < 30; i++) {
            var poly = Polygon.CreateRect(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200));
            var startTicks = r.Next(0, 500);
            var endTicks = r.Next(startTicks + 20, startTicks + 100);
            var terrainHole = new TerrainHole { Polygons = new[] { poly } };
            GameEventQueueService.AddGameEvent(CreateAddTemporaryHoleEvent(new GameTime(startTicks), terrainHole));
            GameEventQueueService.AddGameEvent(CreateRemoveTemporaryHoleEvent(new GameTime(endTicks), terrainHole));
         }

//         var a = CreateTestEntity(new DoubleVector2(60, 40), 15, 80);
//         var b = CreateTestEntity(new DoubleVector2(675, 175), 15, 70);
//         var c = CreateTestEntity(new DoubleVector2(50, 900), 15, 60);
//         var d = CreateTestEntity(new DoubleVector2(50, 500), 15, 50);
//
//         MovementSystemService.Pathfind(a, new DoubleVector2(930, 300));
//         MovementSystemService.Pathfind(b, new DoubleVector2(825, 300));
//         MovementSystemService.Pathfind(c, new DoubleVector2(950, 475));
//         MovementSystemService.Pathfind(d, new DoubleVector2(80, 720));

         var benchmarkDestination = new DoubleVector2(950, 50);
         var benchmarkUnitBaseSpeed = 100.0f;
         var swarm = new Swarm { Destination = benchmarkDestination };
         for (var y = 0; y < 10; y++) {
            for (var x = 0; x < 10; x++) {
               var swarmlingRadius = 10f;
               var p = new DoubleVector2(50, 500);
               var offset = new DoubleVector2(x * swarmlingRadius * 2, y * swarmlingRadius * 2);
               var swarmling = CreateTestEntity(p + offset, swarmlingRadius, benchmarkUnitBaseSpeed);
               swarmling.MovementComponent.Swarm = swarm;
               swarm.Entities.Add(swarmling);
            }
         }

         var optimal = CreateTestEntity(new DoubleVector2(50 + 9 * 10*2, 500), 10, 100f);
         MovementSystemService.Pathfind(optimal, benchmarkDestination);

         var debugMultiCanvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(MapConfiguration.Size, new Point(100, 100));
         DebugHandleFrameEnd(debugMultiCanvasHost);
         GameTimeService.IncrementTicks();

         IntMath.Sqrt(0); // init static

         var sw = new Stopwatch();
         sw.Start();
         while (true) {
            GameEventQueueService.ProcessPendingGameEvents();
            EntityService.ProcessSystems();
            DebugHandleFrameEnd(debugMultiCanvasHost);

            GameTimeService.IncrementTicks();
//            Console.WriteLine("At " + GameTimeService.Ticks + " " + TerrainService.BuildSnapshot().TemporaryHoles.Count);
            //            if (GameTimeService.Ticks > 80) return;
            if (GameTimeService.Ticks > GameTimeService.TicksPerSecond * 80) {
               Console.WriteLine($"Done! {sw.Elapsed.TotalSeconds}");
               return;
            }
         }
      }

      private Entity CreateTestEntity(DoubleVector2 initialPosition, float radius, float movementSpeed) {
         var entity = EntityService.CreateEntity();
         EntityService.AddEntityComponent(entity, new MovementComponent {
            Position = initialPosition,
            BaseRadius = radius,
            BaseSpeed = movementSpeed
         });
         return entity;
      }

      private void DebugHandleFrameEnd(DebugMultiCanvasHost debugMultiCanvasHost) {
         var terrainSnapshot = TerrainService.BuildSnapshot();
         var temporaryHolePolygons = terrainSnapshot.TemporaryHoles.SelectMany(th => th.Polygons).ToList();
         var holeDilationRadius = 15.0;
         var visibilityGraph = terrainSnapshot.ComputeVisibilityGraph(holeDilationRadius);
         var debugCanvas = debugMultiCanvasHost.CreateAndAddCanvas(GameTimeService.Ticks);

         debugCanvas.Draw(g => {
            debugCanvas.DrawPolygons(temporaryHolePolygons, Color.Red);
            debugCanvas.DrawVisibilityGraph(visibilityGraph);
            //         var testPathFindingQueries = new[] {
            //            Tuple.Create(new IntVector2(60, 40), new IntVector2(930, 300)),
            //            Tuple.Create(new IntVector2(675, 175), new IntVector2(825, 300)),
            //            Tuple.Create(new IntVector2(50, 900), new IntVector2(950, 475)),
            //            Tuple.Create(new IntVector2(50, 500), new IntVector2(80, 720))
            //         };

            using (var pen = new Pen(Color.Lime, 2)) {
               foreach (var entity in EntityService.EnumerateEntities()) {
                  var movementComponent = entity.MovementComponent;
                  if (movementComponent == null) continue;
                  var pathPoints = entity.MovementComponent.PathingBreadcrumbs.Select(p => p.LossyToIntVector2()).ToList();
                  pathPoints.Insert(0, movementComponent.Position.LossyToIntVector2());
                  debugCanvas.DrawLineStrip(pathPoints, pen);
               }
            }

            debugCanvas.DrawPolyTree(terrainSnapshot.ComputePunchedLand(0));
            debugCanvas.DrawPolyTree(terrainSnapshot.ComputePunchedLand(holeDilationRadius));
            var hdrTriangulation = terrainSnapshot.ComputeTriangulation(holeDilationRadius);
            debugCanvas.DrawTriangulation(hdrTriangulation, Pens.DarkGray);

//            // draw triangulation quadtree
//            foreach (var island in hdrTriangulation.Islands) {
//               var s = new Stack<Tuple<int, QuadTree<int>.Node>>();
//               s.Push(Tuple.Create(0, island.TriangleIndexQuadTree.Root));
//               while (s.Any()) {
//                  var tuple = s.Pop();
//                  var depth = tuple.Item1;
//                  var node = tuple.Item2;
//                  debugCanvas.DrawRectangle(node.Rect);
//                  if (node.TopLeft != null) {
//                     s.Push(Tuple.Create(depth + 1, node.TopLeft));
//                     s.Push(Tuple.Create(depth + 1, node.TopRight));
//                     s.Push(Tuple.Create(depth + 1, node.BottomLeft));
//                     s.Push(Tuple.Create(depth + 1, node.BottomRight));
//                  }
//               }
//            }

            using (var highlightPen = new Pen(Brushes.Red, 3.0f)) {
               foreach (var entity in EntityService.EnumerateEntities()) {
                  var movementComponent = entity.MovementComponent;
                  if (movementComponent != null) {
                     var triangulation = terrainSnapshot.ComputeTriangulation(movementComponent.BaseRadius);
                     TriangulationIsland island;
                     int triangleIndex;
                     if (triangulation.TryIntersect(movementComponent.Position.X, movementComponent.Position.Y, out island, out triangleIndex)) {
                        debugCanvas.DrawTriangle(island.Triangles[triangleIndex], highlightPen);
                     }
                  }
               }
            }

            foreach (var entity in EntityService.EnumerateEntities()) {
               var movementComponent = entity.MovementComponent;
               if (movementComponent != null) {
                  debugCanvas.DrawPoint(movementComponent.Position.LossyToIntVector2(), Brushes.Black, movementComponent.BaseRadius);
                  debugCanvas.DrawPoint(movementComponent.Position.LossyToIntVector2(), Brushes.White, movementComponent.BaseRadius - 2);

                  if (movementComponent.Swarm != null && movementComponent.WeightedSumNBodyForces.Norm2D() > GeometryOperations.kEpsilon) {
                     debugCanvas.DrawLineList(
                        new[] {
                           movementComponent.Position.LossyToIntVector2(),
                           (movementComponent.Position + movementComponent.WeightedSumNBodyForces.ToUnit() * movementComponent.BaseRadius).LossyToIntVector2()
                        }, Pens.Gray);
                  }

                  if (movementComponent.DebugLines != null) {
                     foreach (var l in movementComponent.DebugLines) {
                        debugCanvas.DrawLineList(new [] { l.Item1.LossyToIntVector2(), l.Item2.LossyToIntVector2() }, Pens.Black);
                     }
                  }
               }
            }

            //         for (int x = -50; x < 1100; x += 100) {
            //            for (int y = -50; y < 1100; y += 100) {
            //               var query = new IntVector2(x, y);
            //               IntVector2 nearestLandPoint;
            //               var isInHole = terrainSnapshot.FindNearestLandPointAndIsInHole(holeDilationRadius, query, out nearestLandPoint);
            //               debugCanvas.DrawPoint(query, isInHole ? Brushes.Red : Brushes.Lime, 3.0f);
            //               if (isInHole) {
            //                  debugCanvas.DrawLineStrip(
            //                     new [] { query, nearestLandPoint },
            //                     Pens.Magenta);
            //               }
            //            }
            //         }
         });
      }

      public GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole terrainHole) {
         return new AddTemporaryHoleGameEvent(time, GameLogicFacade, terrainHole);
      }

      public GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole terrainHole) {
         return new RemoveTemporaryHoleGameEvent(time, GameLogicFacade, terrainHole);
      }
   }

   public class GameLogicFacade {
      private readonly TerrainService terrainService;
      private readonly MovementSystemService movementSystemService;

      public GameLogicFacade(TerrainService terrainService, MovementSystemService movementSystemService) {
         this.terrainService = terrainService;
         this.movementSystemService = movementSystemService;
      }

      public void AddTemporaryHole(TerrainHole hole) {
         terrainService.AddTemporaryHole(hole);
         // todo: can optimize to only invalidate paths intersecting hole.
         movementSystemService.HandleHoleAdded(hole);
      }

      public void RemoveTemporaryHole(TerrainHole hole) {
         terrainService.RemoveTemporaryHole(hole);
         movementSystemService.InvalidatePaths();
      }
   }

   public class GameInstanceFactory {
      public GameInstance Create() {
         var gameTimeService = new GameTimeService(30);
         var gameLoop = new GameEventQueueService(gameTimeService);
         var mapConfiguration = CreateDefaultMapConfiguration();
         var terrainService = new TerrainService(mapConfiguration, gameTimeService);
         var entityService = new EntityService();
         var statsCalculator = new StatsCalculator();
         var pathfinderCalculator = new PathfinderCalculator(terrainService, statsCalculator);
         var movementSystemService = new MovementSystemService(entityService, gameTimeService, statsCalculator, terrainService, pathfinderCalculator);
         entityService.AddEntitySystem(movementSystemService);
         var gameLogicFacade = new GameLogicFacade(terrainService, movementSystemService);
         return new GameInstance {
            GameTimeService = gameTimeService,
            GameEventQueueService = gameLoop,
            MapConfiguration = mapConfiguration,
            TerrainService = terrainService,
            EntityService = entityService,
            PathfinderCalculator = pathfinderCalculator,
            MovementSystemService = movementSystemService,
            GameLogicFacade = gameLogicFacade
         };
      }

      private static MapConfiguration CreateDefaultMapConfiguration() {
         var holes = new[] {
            Polygon.CreateRect(100, 100, 300, 300),
            Polygon.CreateRect(400, 200, 100, 100),
            Polygon.CreateRect(200, -50, 100, 150),
            Polygon.CreateRect(600, 600, 300, 300),
            Polygon.CreateRect(700, 500, 100, 100),
            Polygon.CreateRect(200, 700, 100, 100),
            Polygon.CreateRect(600, 100, 300, 50),
            Polygon.CreateRect(600, 150, 50, 200),
            Polygon.CreateRect(850, 150, 50, 200),
            Polygon.CreateRect(600, 350, 300, 50),
            Polygon.CreateRect(700, 200, 100, 100)
         };

//         var holeSquiggle = PolylineOperations.ExtrudePolygon(
//            new[] {
//               new IntVector2(100, 50),
//               new IntVector2(100, 100),
//               new IntVector2(200, 100),
//               new IntVector2(200, 150),
//               new IntVector2(200, 200),
//               new IntVector2(400, 250),
//               new IntVector2(200, 300),
//               new IntVector2(400, 315),
//               new IntVector2(200, 330),
//               new IntVector2(210, 340),
//               new IntVector2(220, 350),
//               new IntVector2(220, 400),
//               new IntVector2(221, 400)
//            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygons();

         return new MapConfiguration {
            Size = new Size(1000, 1000),
            StaticHolePolygons = holes.ToList()
         };
      }
   }
}
