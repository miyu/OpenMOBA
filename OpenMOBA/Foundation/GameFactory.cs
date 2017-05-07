using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using OpenMOBA.Utilities;

namespace OpenMOBA.Foundation {
   public interface IGameEventFactory {
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, TerrainHole temporaryHole);
   }

   public class Swarm {
      public List<Entity> Entities { get; set; } = new List<Entity>();
      public DoubleVector3 Destination { get; set; }
   }

   public class GameInstance : IGameEventFactory {
      public DebugProfiler DebugProfiler { get; } = new DebugProfiler();
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
            var poly = Polygon.CreateRectXY(r.Next(0, 800), r.Next(0, 800), r.Next(100, 200), r.Next(100, 200), 0);
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

         var benchmarkDestination = new DoubleVector3(950, 50, 0.0);
         var benchmarkUnitBaseSpeed = 50.0f;
         var swarm = new Swarm { Destination = benchmarkDestination };
         var swarmMeanRadius = 10.0f;
         for (var y = 0; y < 10; y++) {
            for (var x = 0; x < 10; x++) {
               // var swarmlingRadius = 10f;
               var swarmlingRadius = (float)Math.Round(5.0f + 10.0f * (float)r.NextDouble());
               var p = new DoubleVector3(50, 500, 0);
               var offset = new DoubleVector3(x * swarmMeanRadius * 2, y * swarmMeanRadius * 2, 0);
//               var swarmling = CreateTestEntity(p + offset, swarmlingRadius, benchmarkUnitBaseSpeed - 20 + 40 * (float)r.NextDouble());
//               swarmling.MovementComponent.Swarm = swarm;
//               swarm.Entities.Add(swarmling);
            }
         }

         var optimal = CreateTestEntity(new DoubleVector3(50 + 9 * 10*2, 500, 0.0), 10, benchmarkUnitBaseSpeed);
         MovementSystemService.Pathfind(optimal, benchmarkDestination);

         var debugMultiCanvasHost = DebugMultiCanvasHost.CreateAndShowCanvas(MapConfiguration.Size, new Point(100, 100));
//         DebugHandleFrameEnd(debugMultiCanvasHost);
//         GameTimeService.IncrementTicks();

         IntMath.Sqrt(0); // init static

         var sw = new Stopwatch();
         sw.Start();
         while (true) {
            DebugProfiler.EnterTick(GameTimeService.Ticks);

            int eventsProcessed;
            GameEventQueueService.ProcessPendingGameEvents(out eventsProcessed);
            EntityService.ProcessSystems();

            DebugProfiler.LeaveTick();

            //// GameTimeService.Ticks % 1 == 0)
            if (eventsProcessed != 0 || GameTimeService.Ticks % 1 == 0) {
               DebugHandleFrameEnd(debugMultiCanvasHost);
//               return;
            }

            GameTimeService.IncrementTicks();
//            Console.WriteLine("At " + GameTimeService.Ticks + " " + TerrainService.BuildSnapshot().TemporaryHoles.Count);
            //            if (GameTimeService.Ticks > 80) return;
            if (GameTimeService.Ticks > GameTimeService.TicksPerSecond * 20) {
               Console.WriteLine($"Done! {sw.Elapsed.TotalSeconds}");
               break;
            }
         }

         var latch = new CountdownEvent(1);
         new Thread(() => {
            DebugProfiler.DumpToClipboard();
            latch.Signal();
         }) { ApartmentState = ApartmentState.STA }.Start();
         latch.Wait();
      }

      private Entity CreateTestEntity(DoubleVector3 initialPosition, float radius, float movementSpeed) {
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
         var debugCanvas = debugMultiCanvasHost.CreateAndAddCanvas(GameTimeService.Ticks);

         AngularVisibleSegmentStore ComputeLineOfSight(DoubleVector2 position, double radius) {
            var barriers = terrainSnapshot.ComputeVisibilityGraph(radius).Barriers;
            var avss = new AngularVisibleSegmentStore(position);
            var i = -1; // for debugging
            foreach (var barrier in barriers) {
               //                  Console.WriteLine("INSERT " + barrier);
               avss.Insert(barrier);

               if (i-- == 0) {
                  using (var thick = new Pen(Color.LawnGreen, 20f)) {
                     debugCanvas.DrawLineStrip(
                        new[] {
                           barrier.First.XY,
                           barrier.Second.XY
                        }, thick);
                  }
                  break;
               }
            }
            return avss;
         }

         var sw = new Stopwatch();
         sw.Start();
         var testMc = EntityService.EnumerateEntities().First().MovementComponent;
         for (var it = 0; it < 100; it++) {
            ComputeLineOfSight(testMc.Position.XY, testMc.BaseRadius);
         }
         DebugProfiler.AddStatistic("shade.compgeom.lineofsight.compute100", sw.ElapsedMilliseconds);

         var temporaryHolePolygons = terrainSnapshot.TemporaryHoles.SelectMany(th => th.Polygons).ToList();
         var holeDilationRadius = 15.0;
         var visibilityGraph = terrainSnapshot.ComputeVisibilityGraph(holeDilationRadius);
         debugCanvas.Draw(g => {
            void DrawLineOfSight(DoubleVector2 position, double radius) {
               var avss = ComputeLineOfSight(position, radius);

               foreach (var range in avss.Get()) {
                  var oxy = position;
                  var rstart = DoubleVector2.FromRadiusAngle(100, range.ThetaStart);
                  var rend = DoubleVector2.FromRadiusAngle(100, range.ThetaEnd);

                  if (range.Id == AngularVisibleSegmentStore.RANGE_ID_NULL) {
                     continue;
                  }

                  //                  Console.WriteLine($"{oxy}, {range.ThetaStart}, {range.ThetaEnd}");
                  //                  Console.WriteLine(range.Segment.Value);

                  var s = range.Segment;
                  var s1 = s.First.XY.ToDoubleVector2();
                  var s2 = s.Second.XY.ToDoubleVector2();
                  DoubleVector2 visibleStart, visibleEnd;
                  if (!GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rstart, s1, s2, out visibleStart)) {
                     // wtf?
                     continue;
                  }
                  if (!GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rend, s1, s2, out visibleEnd)) {
                     // wtf?
                     continue;
                  }

                  //                  Console.WriteLine($"({visibleStart}, {visibleEnd})");

                  using (var b = new SolidBrush(Color.FromArgb(120, Color.Yellow)))
                     debugCanvas.FillPolygon(
                        new Polygon(new List<IntVector3> {
                           new IntVector3((long)position.X, (long)position.Y, 0),
                           new IntVector3((long)visibleStart.X, (long)visibleStart.Y, 0),
                           new IntVector3((long)visibleEnd.X, (long)visibleEnd.Y, 0)
                        }, false), b);
                  using (var dash = new Pen(Color.FromArgb(30, Color.Black), 1f) { DashPattern = new[] { 10.0f, 10.0f } })
                  using (var thick = new Pen(Color.Black, 5f))
                     try {
                        debugCanvas.DrawLineStrip(
                           new[] {
                              oxy.LossyToIntVector2(),
                              visibleStart.LossyToIntVector2()
                           }, dash);
                        debugCanvas.DrawLineStrip(
                           new[] {
                              oxy.LossyToIntVector2(),
                              visibleEnd.LossyToIntVector2()
                           }, dash);
                        debugCanvas.DrawLineStrip(
                           new[] {
                              visibleStart.LossyToIntVector2(),
                              visibleEnd.LossyToIntVector2()
                           }, thick);
                     } catch { }
               }
            }

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
                  var pathPoints = entity.MovementComponent.PathingBreadcrumbs.Select(p => p.XY.LossyToIntVector2()).ToList();
                  pathPoints.Insert(0, movementComponent.Position.XY.LossyToIntVector2());
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

//            var losRadius = EntityService.EnumerateEntities().First().MovementComponent.BaseRadius;
//            var waypoint = TerrainService.BuildSnapshot().ComputeVisibilityGraph(losRadius).Waypoints[5];
//            debugCanvas.DrawPoint(waypoint.XY, Brushes.Black, 2f);
//            DrawLineOfSight(waypoint.XY.ToDoubleVector2(), losRadius);

            foreach (var entity in EntityService.EnumerateEntities()) {
               var movementComponent = entity.MovementComponent;
               if (movementComponent != null) {
                  debugCanvas.DrawPoint(movementComponent.Position.XY.LossyToIntVector2(), Brushes.Black, movementComponent.BaseRadius);
                  debugCanvas.DrawPoint(movementComponent.Position.XY.LossyToIntVector2(), Brushes.White, movementComponent.BaseRadius - 2);

                  if (movementComponent.Swarm != null && movementComponent.WeightedSumNBodyForces.Norm2D() > GeometryOperations.kEpsilon) {
                     debugCanvas.DrawLineList(
                        new[] {
                           movementComponent.Position.XY.LossyToIntVector2(),
                           (movementComponent.Position.XY + movementComponent.WeightedSumNBodyForces.ToUnit() * movementComponent.BaseRadius).LossyToIntVector2()
                        }, Pens.Gray);
                  }

                  DrawLineOfSight(movementComponent.Position.XY, movementComponent.BaseRadius);

                  if (movementComponent.DebugLines != null) {
                     foreach (var l in movementComponent.DebugLines) {
                        debugCanvas.DrawLineList(new[] { l.Item1.LossyToIntVector2(), l.Item2.LossyToIntVector2() }, Pens.Black);
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
            Polygon.CreateRectXY(100, 100, 300, 300, 0),
            Polygon.CreateRectXY(400, 200, 100, 100, 0),
            Polygon.CreateRectXY(200, -50, 100, 150, 0),
            Polygon.CreateRectXY(600, 600, 300, 300, 0),
            Polygon.CreateRectXY(700, 500, 100, 100, 0),
            Polygon.CreateRectXY(200, 700, 100, 100, 0),
            Polygon.CreateRectXY(600, 100, 300, 50, 0),
            Polygon.CreateRectXY(600, 150, 50, 200, 0),
            Polygon.CreateRectXY(850, 150, 50, 200, 0),
            Polygon.CreateRectXY(600, 350, 300, 50, 0),
            Polygon.CreateRectXY(700, 200, 100, 100, 0)
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
