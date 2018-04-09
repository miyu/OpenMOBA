using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.DevTool.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Foundation.Terrain.CompilationResults.Overlay;
using OpenMOBA.Foundation.Terrain.Declarations;
using OpenMOBA.Geometry;
using cInt = System.Int32;
using PqItem = System.ValueTuple<float, float, OpenMOBA.Foundation.Terrain.CompilationResults.Overlay.TerrainOverlayNetworkNode, int, OpenMOBA.Foundation.Terrain.CompilationResults.Overlay.TerrainOverlayNetworkNode, int, OpenMOBA.Foundation.Terrain.CompilationResults.Overlay.TerrainOverlayNetworkEdge>;

namespace OpenMOBA.Foundation {
   public class Entity {
      public EntityComponentsMask ComponentMask { get; set; }
      public EntityComponent[] ComponentsByType { get; } = new EntityComponent[(int)EntityComponentType.Count];
      public MovementComponent MovementComponent => (MovementComponent)ComponentsByType[(int)EntityComponentType.Movement];
   }

   public class EntityService {
      private readonly HashSet<Entity> entities = new HashSet<Entity>();
      private readonly List<EntitySystemService> systems = new List<EntitySystemService>();

      public IEnumerable<Entity> EnumerateEntities() {
         return entities;
      }

      public void AddEntitySystem(EntitySystemService system) {
         systems.Add(system);
      }

      public Entity CreateEntity() {
         var entity = new Entity();
         entities.Add(entity);
         return entity;
      }

      public void AddEntityComponent(Entity entity, EntityComponent component) {
         if (entity.ComponentMask.Contains(ComponentMaskUtils.Build(component.Type))) throw new InvalidOperationException("Entity already has component of type " + component.Type);
         entity.ComponentMask = entity.ComponentMask.Or(component.Type);
         entity.ComponentsByType[(int)component.Type] = component;
         foreach (var system in systems) if (entity.ComponentMask.Contains(system.RequiredComponentsMask)) system.AssociateEntity(entity);
      }

      public void ProcessSystems() {
         foreach (var system in systems) system.Execute();
      }
   }

   public static class ComponentMaskUtils {
      public static EntityComponentsMask Or(this EntityComponentsMask mask, EntityComponentType type) {
         return mask | (EntityComponentsMask)(1 << (int)type);
      }

      public static bool Contains(this EntityComponentsMask mask, EntityComponentsMask other) {
         return (mask & other) == other;
      }

      public static EntityComponentsMask Build(params EntityComponentType[] componentTypes) {
         EntityComponentsMask result = 0;
         foreach (var componentType in componentTypes) result = result.Or(componentType);
         return result;
      }
   }

   /// <summary>
   ///    Note: A value in this enum is treated as an offset into an array.
   ///    Note: This takes advantage of the first enum member having value 0.
   /// </summary>
   public enum EntityComponentType {
      Movement,
      Status,

      Count
   }

   [Flags]
   public enum EntityComponentsMask : uint { }

   public abstract class EntityComponent {
      protected EntityComponent(EntityComponentType type) {
         Type = type;
      }

      public EntityComponentType Type { get; }
   }

   public class MovementComponent : EntityComponent {
      public MovementComponent() : base(EntityComponentType.Movement) { }
      public DoubleVector3 WorldPosition { get; set; }
      public DoubleVector3 LookAt { get; set; } = DoubleVector3.UnitX;
      public float BaseRadius { get; set; }
      public float BaseSpeed { get; set; }

      /// <summary>
      ///    If true, movement will recompute path before updating position
      /// </summary>
      public bool PathingIsInvalidated { get; set; }

      /// <summary>
      ///    The desired destination of the unit. Even if pathfinding fails, this is still set and,
      ///    once terrain changes, pathing may attempt to resume.
      /// </summary>
      public DoubleVector3 PathingDestination { get; set; }

      public MotionRoadmap PathingRoadmap { get; set; } = null;
      public int PathingRoadmapProgressIndex = -1;

      public Swarm Swarm { get; set; }
      public TriangulationIsland SwarmingIsland { get; set; }
      public int SwarmingTriangleIndex { get; set; }

      public List<Tuple<DoubleVector3, DoubleVector3>> DebugLines { get; set; }

      // Values precomputed at entry of movement service
      public int ComputedRadius { get; set; }
      public int ComputedSpeed { get; set; }
//      public DoubleVector2 WeightedSumNBodyForces { get; set; }
//      public double SumWeightsNBodyForces { get; set; }

      // Final computed swarmling velocity
      public DoubleVector2 SwarmlingVelocity { get; set; }
   }

   public abstract class EntitySystemService {
      private readonly HashSet<Entity> associatedEntities = new HashSet<Entity>();

      protected EntitySystemService(EntityService entityService, EntityComponentsMask requiredComponentsMask) {
         EntityService = entityService;
         RequiredComponentsMask = requiredComponentsMask;
      }

      public EntityService EntityService { get; }
      public EntityComponentsMask RequiredComponentsMask { get; }
      public IEnumerable<Entity> AssociatedEntities => associatedEntities;

      public void AssociateEntity(Entity entity) {
         associatedEntities.Add(entity);
      }

      public abstract void Execute();
   }

   public static class IntMath {
      private const int MaxLutIntExclusive = 1024 * 1024;
      private static readonly cInt[] SqrtLut = Enumerable.Range(0, MaxLutIntExclusive).Select(x => (cInt)Math.Sqrt(x)).ToArray();

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static cInt Square(cInt x) {
         return x * x;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static cInt Quad(cInt x) {
         return Square(Square(x));
      }

      public static cInt Sqrt(cInt x) {
         if (x < 0) throw new ArgumentException($"sqrti({x})");
         else if (x < MaxLutIntExclusive) return SqrtLut[x];
         else return (cInt)Math.Sqrt(x);
      }
   }

   public class MotionRoadmap {
      public List<MotionRoadmapAction> Plan = new List<MotionRoadmapAction>();
   }

   public abstract class MotionRoadmapAction { }

   public class MotionRoadmapWalkAction : MotionRoadmapAction {
      public MotionRoadmapWalkAction(TerrainOverlayNetworkNode node, IntVector2 source, IntVector2 destination) {
         Node = node;
         Source = source;
         Destination = destination;
      }

      public readonly TerrainOverlayNetworkNode Node;
      public readonly IntVector2 Source;
      public readonly IntVector2 Destination;
   }

   public class PathfinderCalculator {
      private readonly StatsCalculator statsCalculator;
      private readonly TerrainService terrainService;

      public PathfinderCalculator(TerrainService terrainService, StatsCalculator statsCalculator) {
         this.terrainService = terrainService;
         this.statsCalculator = statsCalculator;
      }

      public bool IsDestinationReachable(double holeDilationRadius, DoubleVector3 sourceWorld, DoubleVector3 destinationWorld) {
         var snapshot = terrainService.CompileSnapshot();
         var overlayNetwork = snapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);

         return overlayNetwork.TryFindTerrainOverlayNode(sourceWorld.ToDotNetVector(), out var sourceNode) &&
                overlayNetwork.TryFindTerrainOverlayNode(destinationWorld.ToDotNetVector(), out var destinationNode) &&
                IsDestinationReachable(sourceNode, destinationNode);
      }

      public bool IsDestinationReachable(TerrainOverlayNetworkNode sourceNode, TerrainOverlayNetworkNode destinationNode) {
         var visited = new HashSet<TerrainOverlayNetworkNode>();

         bool Visit(TerrainOverlayNetworkNode n) {
            if (!visited.Add(n)) {
               return false;
            }
            if (n == destinationNode) {
               return true;
            }
            foreach (var neighbor in n.OutboundEdgeGroups.Keys) {
               Visit(neighbor);
            }
            return false;
         }

         return Visit(sourceNode);
      }

      public bool TryFindPath(double agentRadius, DoubleVector3 sourceWorld, DoubleVector3 destinationWorld, out MotionRoadmap roadmap, IDebugCanvas debugCanvas = null) {
         roadmap = null;
         var terrainSnapshot = terrainService.CompileSnapshot();
         var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(agentRadius);
         if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(sourceWorld.ToDotNetVector(), out var sourceNode)) return false;
         if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(destinationWorld.ToDotNetVector(), out var destinationNode)) return false;

         var sourceLocal = Vector3.Transform(sourceWorld.ToDotNetVector(), sourceNode.SectorNodeDescription.WorldTransformInv);
         var destinationLocal = Vector3.Transform(destinationWorld.ToDotNetVector(), destinationNode.SectorNodeDescription.WorldTransformInv);

         return TryFindPath(
            sourceNode, 
            sourceLocal.ToOpenMobaVector().LossyToIntVector3().XY, 
            destinationNode, 
            destinationLocal.ToOpenMobaVector().LossyToIntVector3().XY,
            out roadmap,
            debugCanvas);
      }

      public bool TryFindPath(TerrainOverlayNetworkNode sourceNode, IntVector2 sourcePoint, TerrainOverlayNetworkNode destinationNode, IntVector2 destinationPoint, out MotionRoadmap result, IDebugCanvas debugCanvas = null) {
         var roadmap = new MotionRoadmap();

         if (debugCanvas != null) {
            debugCanvas.Transform = Matrix4x4.Identity;
         }

         void DrawLine(TerrainOverlayNetworkNode p1Node, IntVector2 p1Local, TerrainOverlayNetworkNode p2Node, IntVector2 p2Local, StrokeStyle strokeStyle) {
            if (debugCanvas == null) return;
            debugCanvas.DrawLine(
               Vector3.Transform(new Vector3(p1Local.X, p1Local.Y, 0), p1Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector(),
               Vector3.Transform(new Vector3(p2Local.X, p2Local.Y, 0), p2Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector(),
               strokeStyle);
         }

         void X(TerrainOverlayNetworkNode node, int sourceWaypoint, int destinationWaypoint) {
            var cpm = node.CrossoverPointManager;
            var waypointToWaypointLut = cpm.WaypointToWaypointLut;
            var sourcePath = new List<PathLink>();
            var destPath = new List<PathLink>();

            // must query with [a][b] where a > b
            var sourceFinger = sourceWaypoint;
            var destinationFinger = destinationWaypoint;
            while (sourceFinger != destinationFinger) {
               if (sourceFinger < destinationFinger) {
                  var link = waypointToWaypointLut[destinationFinger][sourceFinger];
                  destPath.Add(link);
                  destinationFinger = link.PriorIndex;
               } else {
                  var link = waypointToWaypointLut[sourceFinger][destinationFinger];
                  sourcePath.Add(link);
                  sourceFinger = link.PriorIndex;
               }
            }

            // extend roadmap
            var prior = sourceWaypoint;
            for (var i = 0; i < sourcePath.Count; i++) {
               var next = sourcePath[i].PriorIndex;
               roadmap.Plan.Add(new MotionRoadmapWalkAction(node, cpm.Waypoints[prior], cpm.Waypoints[next]));
               prior = next;
            }

            // skip last item since is link to last of source plan
            for (var i = destPath.Count - 2; i >= 0; i--) {
               var next = destPath[i].PriorIndex;
               roadmap.Plan.Add(new MotionRoadmapWalkAction(node, cpm.Waypoints[prior], cpm.Waypoints[next]));
               prior = next;
            }

            if (prior != destinationWaypoint) {
               roadmap.Plan.Add(new MotionRoadmapWalkAction(node, cpm.Waypoints[prior], cpm.Waypoints[destinationWaypoint]));
            }
         }

         if (sourceNode == destinationNode) {
            if (sourceNode.LandPolyNode.SegmentInLandPolygonNonrecursive(sourcePoint, destinationPoint)) {
               roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourcePoint, destinationPoint));
               result = roadmap; 
               return true;
            }

            var sourceVisibleWaypointLinks = sourceNode.CrossoverPointManager.FindVisibleWaypointLinks(sourcePoint, null, out var sourceVisibleWaypointLinksLength, out var sourceOptimalLinkToWaypoints);
            var destinationVisibleWaypointLinks = sourceNode.CrossoverPointManager.FindVisibleWaypointLinks(destinationPoint, null, out var destinationVisibleWaypointLinksLength, out var destinationOptimalLinkToWaypoints);

            var bestFirstWaypoint = -1;
            var bestFirstWaypointCost = double.PositiveInfinity;
            for (var i = 0 ; i < sourceVisibleWaypointLinksLength; i++) {
               var link = sourceVisibleWaypointLinks[i];
               var firstWaypoint = link.PriorIndex;
               var cost = link.TotalCost + destinationOptimalLinkToWaypoints[firstWaypoint].TotalCost;
               if (cost < bestFirstWaypointCost) {
                  bestFirstWaypoint = firstWaypoint;
                  bestFirstWaypointCost = cost;
               }
            }

            roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourcePoint, sourceNode.CrossoverPointManager.Waypoints[bestFirstWaypoint]));
            X(sourceNode, bestFirstWaypoint, destinationOptimalLinkToWaypoints[bestFirstWaypoint].PriorIndex);
            roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourceNode.CrossoverPointManager.Waypoints[destinationOptimalLinkToWaypoints[bestFirstWaypoint].PriorIndex], destinationPoint));
            result = roadmap;
            return true;
         }

         const int SOURCE_POINT_CPI = -100;
         const int DESTINATION_POINT_CPI = -200;
         // todo: special-case if src is dst node

         Console.WriteLine("Src had " + sourceNode.CrossoverPointManager.CrossoverPoints.Count + " : " + string.Join(", ", sourceNode.CrossoverPointManager.CrossoverPoints));
         var (_, _, _, sourceOptimalLinkToCrossovers) = sourceNode.CrossoverPointManager.FindOptimalLinksToCrossovers(sourcePoint);
         var (_, _, _, destinationOptimalLinkToCrossovers) = destinationNode.CrossoverPointManager.FindOptimalLinksToCrossovers(destinationPoint);


         var q = new PriorityQueue<PqItem>((a, b) => a.Item1.CompareTo(b.Item1));
         var priorityUpperBounds = new Dictionary<(TerrainOverlayNetworkNode, int), float>();
         var predecessor = new Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge)>(); // visited

         foreach (var kvp in sourceNode.OutboundEdgeGroups) {
            foreach (var g in kvp.Value) {
               foreach (var edge in g.Edges) {
                  var cpiLink = sourceOptimalLinkToCrossovers[edge.SourceCrossoverIndex];
                  var worldCpiLinkCost = cpiLink.TotalCost * sourceNode.SectorNodeDescription.LocalToWorldScalingFactor;
                  priorityUpperBounds[(sourceNode, edge.SourceCrossoverIndex)] = worldCpiLinkCost;
                  q.Enqueue((worldCpiLinkCost, worldCpiLinkCost, sourceNode, SOURCE_POINT_CPI, sourceNode, edge.SourceCrossoverIndex, null));
                  Console.WriteLine("Init link: " + cpiLink.TotalCost + " " + edge.SourceCrossoverIndex + " of " + sourceNode.CrossoverPointManager.CrossoverPoints.Count);
               }
            }
         }

         int enqueueCount = 0;
         int dequeueCount = 0;
         bool terminalEnqueued = false;

         var destinationWorld = Vector3.Transform(new Vector3(destinationPoint.X, destinationPoint.Y, 0), destinationNode.SectorNodeDescription.WorldTransform);

         float ComputeHeuristic(TerrainOverlayNetworkNode n, int cpi) {
            var cp = n.CrossoverPointManager.CrossoverPoints[cpi];
            var cpw = Vector3.Transform(new Vector3(cp.X, cp.Y, 0), n.SectorNodeDescription.WorldTransform);
            return Vector3.Distance(destinationWorld, cpw) * 1.0f;
         }

         void DrawPqItem(PqItem item, StrokeStyle strokeStyle) {
            var (_, ncost, nsrcnode, nsrccpi, ndstnode, ndstcpi, nedge) = item;
            DrawLine(
               nsrcnode,
               nsrccpi == SOURCE_POINT_CPI ? sourcePoint : nsrcnode.CrossoverPointManager.CrossoverPoints[nsrccpi],
               ndstnode,
               ndstcpi == DESTINATION_POINT_CPI ? destinationPoint : ndstnode.CrossoverPointManager.CrossoverPoints[ndstcpi],
               strokeStyle);
         }

         while (!q.IsEmpty) {
            var item = q.Dequeue();
            dequeueCount++;

            var (_, ncost, nsrcnode, nsrccpi, ndstnode, ndstcpi, nedge) = item;
            DrawPqItem(item, StrokeStyle.RedHairLineSolid);

            //            Console.WriteLine($"Deq {ncost} {nsrcnode} {nsrccpi} {ndstnode} {ndstcpi} {nedge}");

            if (predecessor.ContainsKey((ndstnode, ndstcpi))) {
               continue;
            }
            predecessor[(ndstnode, ndstcpi)] = (nsrcnode, nsrccpi, nedge);

            if (ndstcpi == DESTINATION_POINT_CPI) {
               Console.WriteLine("Success! Dequeues: " + dequeueCount + " and enqueues: " + enqueueCount);

               // build high-level plan of path
               var path = new List<(TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge)>();
               var cur = (ndstnode, ndstcpi, (TerrainOverlayNetworkEdge)null);
               while (predecessor.TryGetValue((cur.Item1, cur.Item2), out var pred)) {
                  path.Add(cur);
                  var (psrcnode, psrccpi, pedge) = pred;
                  cur = pred; 
               }
               path.Add(cur);
               path.Reverse();

               foreach (var x in path) {
                  Console.WriteLine("PATH: " + x);
                  if (x.Item2 >= 0) {
                     var cp = x.Item1.CrossoverPointManager.CrossoverPoints[x.Item2];
                     Console.WriteLine("   " + cp + " => " + Vector3.Transform(new Vector3(cp.X, cp.Y, 0), x.Item1.SectorNodeDescription.WorldTransform));
                  } else if (x.Item2 == DESTINATION_POINT_CPI) {
                     Console.WriteLine("   " + destinationPoint + " => " + Vector3.Transform(new Vector3(destinationPoint.X, destinationPoint.Y, 0), x.Item1.SectorNodeDescription.WorldTransform));
                  }
               }

               // convert path to a motion plan. three cases for motion: moving from start to crossover, crossover to crossover, or crossover to end.
               // last one not processed, since we process pairwise.
               for (var i = 0; i < path.Count - 1; i++) {
                  if (i == 0) {
                     // moving from start to crossover
                     var nextCpi = path[1].Item2;
                     var firstLink = sourceOptimalLinkToCrossovers[nextCpi];
                     if (firstLink.PriorIndex == PathLink.DirectPathIndex) {
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourcePoint, sourceNode.CrossoverPointManager.CrossoverPoints[nextCpi]));
                     } else {
                        var lastLink = sourceNode.CrossoverPointManager.OptimalLinkToWaypointsByCrossoverPointIndex[nextCpi][firstLink.PriorIndex];
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourcePoint, sourceNode.CrossoverPointManager.Waypoints[firstLink.PriorIndex]));
                        X(sourceNode, firstLink.PriorIndex, lastLink.PriorIndex);
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourceNode.CrossoverPointManager.Waypoints[lastLink.PriorIndex], sourceNode.CrossoverPointManager.CrossoverPoints[nextCpi]));
                     }

                     // TODO: take cpi edge
                  } else if (i + 2 < path.Count) {
                     // moving from crossover to crossover
                     var (a, b) = (path[i], path[i + 1]);

                     if (a.Item1 != b.Item1) {
                        // todo: handle edge action in roadmap
                        continue;
                     }

                     var firstLink = a.Item1.CrossoverPointManager.OptimalLinkToOtherCrossoversByCrossoverPointIndex[a.Item2][b.Item2];
                     if (firstLink.PriorIndex == PathLink.DirectPathIndex) {
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(a.Item1, a.Item1.CrossoverPointManager.CrossoverPoints[a.Item2], a.Item1.CrossoverPointManager.CrossoverPoints[b.Item2]));
                     } else {
                        var lastLink = b.Item1.CrossoverPointManager.OptimalLinkToOtherCrossoversByCrossoverPointIndex[b.Item2][a.Item2];
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(a.Item1, a.Item1.CrossoverPointManager.CrossoverPoints[a.Item2], a.Item1.CrossoverPointManager.Waypoints[firstLink.PriorIndex]));
                        X(a.Item1, firstLink.PriorIndex, lastLink.PriorIndex);
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(a.Item1, a.Item1.CrossoverPointManager.Waypoints[lastLink.PriorIndex], a.Item1.CrossoverPointManager.CrossoverPoints[b.Item2]));
                     }
                  } else {
                     // moving from crossover to destination
                     var sourceCpi = path[i].Item2;
                     var lastLink = destinationOptimalLinkToCrossovers[sourceCpi];
                     if (lastLink.PriorIndex == PathLink.DirectPathIndex) {
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(destinationNode, destinationNode.CrossoverPointManager.CrossoverPoints[sourceCpi], destinationPoint));
                     } else {
                        var firstLink = destinationNode.CrossoverPointManager.OptimalLinkToWaypointsByCrossoverPointIndex[sourceCpi][lastLink.PriorIndex];
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(destinationNode, destinationNode.CrossoverPointManager.CrossoverPoints[sourceCpi], destinationNode.CrossoverPointManager.Waypoints[firstLink.PriorIndex]));
                        X(destinationNode, firstLink.PriorIndex, lastLink.PriorIndex);
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(destinationNode, destinationNode.CrossoverPointManager.Waypoints[lastLink.PriorIndex], destinationPoint));
                     }
                  }
               }

               Console.WriteLine("Number of nodes visited: " + predecessor.Count);
               Console.WriteLine("Upper bounds: " + priorityUpperBounds.Count);

               result = roadmap;
               return true;
            }

            // expansion to cp of other node => expand to other cps
            if (nsrcnode != ndstnode) {
               var linksToOtherCpis = ndstnode.CrossoverPointManager.OptimalLinkToOtherCrossoversByCrossoverPointIndex[ndstcpi];
               for (var cpi = 0; cpi < linksToOtherCpis.Count; cpi++) {
                  if (cpi == ndstcpi) continue;

                  var link = linksToOtherCpis[cpi];
                  var scost = ncost + link.TotalCost * ndstnode.SectorNodeDescription.LocalToWorldScalingFactor;
                  var sprior = scost + ComputeHeuristic(ndstnode, cpi);
                  Trace.Assert(link.TotalCost >= 0);

                  if (priorityUpperBounds.TryGetValue((ndstnode, cpi), out float scostub) && scostub <= scost) {
                     continue;
                  }
                  priorityUpperBounds[(ndstnode, cpi)] = scost;

                  PqItem nitem = (sprior, scost, ndstnode, ndstcpi, ndstnode, cpi, null);
                  q.Enqueue(nitem);
                  enqueueCount++;
                  DrawPqItem(nitem, StrokeStyle.LimeHairLineSolid);
               }
            }

            // expansion to cp of same node => expand to neighbor edges
            // (technically should do this either way if CPI has multiple meanings...?)
            if (nsrcnode == ndstnode) {
//               Console.WriteLine("OEG?");
               foreach (var kvp in ndstnode.OutboundEdgeGroups) {
                  foreach (var g in kvp.Value) {
                     foreach (var edge in g.Edges) {
                        if (edge.SourceCrossoverIndex == ndstcpi) {
//                           Console.WriteLine("OEG: " + edge + " to " + (ndstnode != g.Destination));
                           var scost = ncost + edge.Cost; // no need for scaling factor
                           var sprior = scost + ComputeHeuristic(g.Destination, edge.DestinationCrossoverIndex);
                           Trace.Assert(edge.Cost >= 0);

                           if (priorityUpperBounds.TryGetValue((g.Destination, edge.DestinationCrossoverIndex), out float scostub) && scostub <= scost) {
                              continue;
                           }
                           priorityUpperBounds[(g.Destination, edge.DestinationCrossoverIndex)] = scost;
                           PqItem nitem = (sprior, scost, ndstnode, ndstcpi, g.Destination, edge.DestinationCrossoverIndex, edge);
                           q.Enqueue(nitem);
                           enqueueCount++;
                           DrawPqItem(nitem, StrokeStyle.MagentaHairLineSolid);
                        }
                     }
                  }
               }
            }

            // expansion to terminal if current node is destination node
            if (ndstnode == destinationNode) {
               var link = destinationOptimalLinkToCrossovers[ndstcpi];
               Trace.Assert(link.TotalCost >= 0);

               var scost = ncost + link.TotalCost * destinationNode.SectorNodeDescription.LocalToWorldScalingFactor;
               var sprior = scost + 0;
               PqItem nitem = (sprior, scost, ndstnode, ndstcpi, destinationNode, DESTINATION_POINT_CPI, null);
               q.Enqueue(nitem);
               enqueueCount++;
               DrawPqItem(nitem, StrokeStyle.CyanHairLineSolid);

               if (!terminalEnqueued) {
                  terminalEnqueued = true;
                  Console.WriteLine("Terminal Enqueued: Dequeues such far: " + dequeueCount + " and enqueues: " + enqueueCount);
               }
            }
         }

         Console.WriteLine("Failure! Dequeues: " + dequeueCount + " and enqueues: " + enqueueCount);
         result = null;
         return false;
      }

      private void X(TerrainOverlayNetworkNode sourceNode, int priorIndex1, int priorIndex2, MotionRoadmap roadmap) {
         throw new NotImplementedException();
      }
   }

   public class MovementSystemService : EntitySystemService {
      public enum WalkResult {
         PushInward,
         CanPushInward,
         Progress,
         CanEdgeFollow,
         Completion
      }

      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Movement);
      private readonly GameTimeService gameTimeService;
      private readonly PathfinderCalculator pathfinderCalculator;
      private readonly StatsCalculator statsCalculator;
      private readonly TerrainService terrainService;

      public MovementSystemService(
         EntityService entityService,
         GameTimeService gameTimeService,
         StatsCalculator statsCalculator,
         TerrainService terrainService,
         PathfinderCalculator pathfinderCalculator
      ) : base(entityService, kComponentMask) {
         this.gameTimeService = gameTimeService;
         this.statsCalculator = statsCalculator;
         this.terrainService = terrainService;
         this.pathfinderCalculator = pathfinderCalculator;
      }

      public void Pathfind(Entity entity, DoubleVector3 destination) {
         var movementComponent = entity.MovementComponent;
         movementComponent.PathingDestination = destination;

         var holeDilationRadius = statsCalculator.ComputeCharacterRadius(entity) + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius;
         pathfinderCalculator.TryFindPath(holeDilationRadius, movementComponent.WorldPosition, destination, out var roadmap);
         movementComponent.PathingRoadmap = roadmap;
         movementComponent.PathingIsInvalidated = false;
         movementComponent.PathingRoadmapProgressIndex = 0;
      }

      public void HandleHoleAdded(DynamicTerrainHoleDescription holeDescription) {
         InvalidatePaths();

         foreach (var entity in AssociatedEntities) {
            var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
            var paddedHoleDilationRadius = characterRadius + InternalTerrainCompilationConstants.AdditionalHoleDilationRadius + InternalTerrainCompilationConstants.TriangleEdgeBufferRadius;
            if (holeDescription.ContainsPoint(paddedHoleDilationRadius, entity.MovementComponent.WorldPosition)) FixEntityInHole(entity);
         }
      }

      private void FixEntityInHole(Entity entity) {
         var computedRadius = statsCalculator.ComputeCharacterRadius(entity);
         var movementComponent = entity.MovementComponent;
         movementComponent.WorldPosition = PushToLand(movementComponent.WorldPosition, computedRadius);
      }

      private DoubleVector3 PushToLand(DoubleVector3 vect, double computedRadius) {
         return vect;
         throw new NotImplementedException();
         //         var paddedHoleDilationRadius = computedRadius + TerrainConstants.AdditionalHoleDilationRadius + TerrainConstants.TriangleEdgeBufferRadius;
         //         DoubleVector3 nearestLandPoint;
         //         if (!terrainService.BuildSnapshot().FindNearestLandPointAndIsInHole(paddedHoleDilationRadius, vect, out nearestLandPoint)) {
         //            throw new InvalidOperationException("In new hole but not terrain snapshot hole.");
         //         }
         //         return nearestLandPoint;
      }

      /// <summary>
      ///    Invalidates all pathing entities' paths, flagging them for recomputation.
      /// </summary>
      public void InvalidatePaths() {
         foreach (var entity in AssociatedEntities) entity.MovementComponent.PathingIsInvalidated = true;
      }

      public bool NN(TriangulationIsland island, DoubleVector3 destination, out Dictionary<int, int> d) {
         int rootTriangleIndex;
         if (!island.TryIntersect(destination.X, destination.Y, out rootTriangleIndex)) {
            d = null;
            return false;
         }

         d = new Dictionary<int, int>();
         var s = new PriorityQueue<Tuple<int, int, double>>((a, b) => a.Item3.CompareTo(b.Item3));
         s.Enqueue(Tuple.Create(rootTriangleIndex, -1, 0.0));
         while (s.Any()) {
            var t = s.Dequeue();
            var ti = t.Item1;
            if (d.ContainsKey(ti)) continue;
            var pi = t.Item2;
            var prevDist = t.Item3;
            d[ti] = pi;
            for (var i = 0; i < 3; i++) {
               var nti = island.Triangles[ti].NeighborOppositePointIndices[i];
               if (nti != Triangle3.NO_NEIGHBOR_INDEX) {
                  var addDist = (island.Triangles[nti].Centroid - island.Triangles[ti].Centroid).Norm2D();
                  s.Enqueue(Tuple.Create(nti, ti, prevDist + addDist));
               }
            }
         }
         return true;
      }

      public override void Execute() {
         foreach (var entity in AssociatedEntities) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent.PathingIsInvalidated) Pathfind(entity, movementComponent.PathingDestination);

            if (movementComponent.Swarm == null) ExecutePathNonswarmer(entity, movementComponent);
            else ExecutePathSwarmer(entity, movementComponent);
         }
      }

      private void ExecutePathNonswarmer(Entity entity, MovementComponent movementComponent) {
         if (movementComponent.PathingRoadmap == null) return;

         var movementSpeed = statsCalculator.ComputeMovementSpeed(entity);
         var worldDistanceRemaining = movementSpeed * gameTimeService.SecondsPerTick;
         var plan = movementComponent.PathingRoadmap.Plan;

         while (worldDistanceRemaining > 0 && movementComponent.PathingRoadmapProgressIndex < plan.Count) {
            var action = plan[movementComponent.PathingRoadmapProgressIndex];
            switch (action) {
               case MotionRoadmapWalkAction wa:
                  var currentSectorLocalPositionDotNet = Vector3.Transform(movementComponent.WorldPosition.ToDotNetVector(), wa.Node.SectorNodeDescription.WorldTransformInv).ToOpenMobaVector();
                  var currentSectorLocalPosition = new DoubleVector2(currentSectorLocalPositionDotNet.X, currentSectorLocalPositionDotNet.Y);
                  Trace.Assert(Math.Abs(currentSectorLocalPositionDotNet.Z) < 1E-3);

                  // vect from position to next pathing breadcrumb (in local space)
                  // todo: set lookat
                  var pb = currentSectorLocalPosition.To(wa.Destination.ToDoubleVector2());

                  // |pb| - distance to next pathing breadcrumb
                  var localDistance = pb.Norm2D();
                  var worldDistance = localDistance * wa.Node.SectorNodeDescription.LocalToWorldScalingFactor;

                  DoubleVector2 nextSectorLocalPosition;
                  if (worldDistance <= float.Epsilon || worldDistance <= worldDistanceRemaining) {
                     nextSectorLocalPosition = wa.Destination.ToDoubleVector2();
                     movementComponent.PathingRoadmapProgressIndex++;
                     worldDistanceRemaining -= worldDistance;
                  } else {
                     nextSectorLocalPosition = currentSectorLocalPosition + pb * worldDistanceRemaining / worldDistance;
                     worldDistanceRemaining = 0;
                  }

                  movementComponent.WorldPosition = Vector3.Transform(
                     new Vector3(nextSectorLocalPosition.ToDotNetVector(), 0), 
                     wa.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
      }

      private void ExecutePathSwarmer(Entity entity, MovementComponent movementComponent) {
         throw new NotImplementedException();
         //         var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
         //         var triangulation = terrainService.BuildSnapshot().ComputeTriangulation(characterRadius);
         //
         //         // p = position of entity to move (updated incrementally)
         //         var p = movementComponent.Position;
         //
         //         // Find triangle we're currently sitting on.
         //         TriangulationIsland island;
         //         int triangleIndex;
         //         if (!triangulation.TryIntersect(p.X, p.Y, out island, out triangleIndex)) {
         //            Console.WriteLine("Warning: Entity not on land.");
         //            FixEntityInHole(entity);
         //
         //            p = movementComponent.Position;
         //            if (!triangulation.TryIntersect(p.X, p.Y, out island, out triangleIndex)) {
         //               Console.WriteLine("Warning: fixing entity not on land failed?");
         //               return;
         //            }
         //         }
         //
         //         // Figure out how much further entity can move this tick
         //         var preferredDirectionUnit = movementComponent.SwarmlingVelocity.ToUnit();
         //         var distanceRemaining = movementComponent.SwarmlingVelocity.Norm2D() * gameTimeService.SecondsPerTick;
         //
         //         movementComponent.Position = CPU(distanceRemaining, p, preferredDirectionUnit, island, triangleIndex);
      }

      private DoubleVector3 CPU(double distanceRemaining, DoubleVector3 p, DoubleVector2 preferredDirectionUnit, TriangulationIsland island, int triangleIndex) {
         var allowPushIntoTriangle = true;
         while (distanceRemaining > GeometryOperations.kEpsilon) {
            DoubleVector3 np;
            int nti;
            var walkResult = WalkTriangle(p, preferredDirectionUnit, distanceRemaining, island, triangleIndex, allowPushIntoTriangle, true, out np, out nti);
            switch (walkResult) {
               case WalkResult.Completion:
                  return np;
               case WalkResult.Progress:
                  distanceRemaining -= (p - np).Norm2D();
                  p = np;
                  triangleIndex = nti;
                  allowPushIntoTriangle = true;
                  continue;
               case WalkResult.PushInward:
                  distanceRemaining -= (p - np).Norm2D();
                  p = np;
                  triangleIndex = nti;
                  allowPushIntoTriangle = false;
                  break;
               case WalkResult.CanPushInward:
                  Console.WriteLine("Warning: Push inward didn't result in being in triangle?");
                  return np;
               case WalkResult.CanEdgeFollow:
                  throw new Exception("Impossible CanEdgeFollow state");
               default:
                  throw new Exception("Impossible state " + walkResult);
            }
         }
         return p;
      }

      // removes normal component of point relative to triangle.
      // NOTE: This can change the point's XY!
//      private DoubleVector3 ProjectToTrianglePlane(DoubleVector3 p, ref Triangle3 triangle) {
//         return p - p.To(triangle.Points[0]).ProjectOnto(triangle.Normal);
//      }

      // Computes Z of p on triangle plane.
//      private DoubleVector3 ZIfyPointOnTrianglePlane(DoubleVector2 p, ref Triangle3 triangle) {
//         // Let p = point we're finding with same x, z
//         //     q = another point on triangle
//         //     n = triangle normal
//         // dot(p-q, normal) = 0
//         // normal.X * (p.X - q.X) + normal.Y * (p.Y - q.Y) + normal.Z * (p.Z - q.Z) = 0
//         // normal.Z * (p.Z - q.Z) = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z - normal.Z * q.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z) / normal.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)) / normal.Z + q.Z
//         var q1 = triangle.Points[0];
//         var q2 = triangle.Points[1];
//         var q = (p - q1.XY).SquaredNorm2D() > (p - q2.XY).SquaredNorm2D() ? q1 : q2;
//         var n = triangle.Normal;
//         var z = (n.X * (q.X - p.X) + n.Y * (q.Y - p.Y)) / n.Z + q.Z;
//         return new DoubleVector3(p.X, p.Y, z);
//      }

      // Computes Z of v formed by triangle plane basis.
//      private DoubleVector3 ZIfyVectorOnTriangleBasis(DoubleVector2 v, ref Triangle3 triangle) {
//         // This is equivalent to ZIfyPointOnTrianglePlane if triangle has 0,0,0 for a point.
//         // Let p = point we're finding with same x, z
//         //     q = 0,0,0
//         //     n = triangle normal
//         // dot(p-q, normal) = 0
//         // normal.X * (p.X - q.X) + normal.Y * (p.Y - q.Y) + normal.Z * (p.Z - q.Z) = 0
//         // normal.Z * (p.Z - q.Z) = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z - normal.Z * q.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)
//         // normal.Z * p.Z = normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y) + normal.Z * q.Z) / normal.Z
//         // p.Z = (normal.X * (q.X - p.X) + normal.Y * (q.Y - p.Y)) / normal.Z + q.Z
//         var p = v;
//         var q = DoubleVector3.Zero;
//         var n = triangle.Normal;
//         var z = (n.X * (q.X - p.X) + n.Y * (q.Y - p.Y)) / n.Z + q.Z;
//         return new DoubleVector3(p.X, p.Y, z);
//      }

      private WalkResult WalkTriangle(
         DoubleVector3 position,
         DoubleVector2 preferredDirectionUnit,
         double distanceRemaining,
         TriangulationIsland island,
         int triangleIndex,
         bool allowPushIntoTriangle,
         bool allowEdgeFollow,
         out DoubleVector3 nextPosition,
         out int nextTriangleIndex
      ) {
         Debug.Assert(GeometryOperations.IsReal(position));
         Debug.Assert(GeometryOperations.IsReal(preferredDirectionUnit));
         Debug.Assert(GeometryOperations.IsReal(distanceRemaining));
         nextPosition = position;
         nextTriangleIndex = triangleIndex;
         return WalkResult.Completion;
//         // Make this a ref in C# 7.0 for minor perf gains
//         var triangle = island.Triangles[triangleIndex];
//         ;
//
//         // NOTE: Position is assumed to be on the triangle plane already.
//         // Either way, enforce: Holding p.XY constant, reset Z to whatever's on triangle plane.
//         var npos = ZIfyPointOnTrianglePlane(position.XY, ref triangle);
//         if ((position - npos).SquaredNorm2D() > 0.05) Console.WriteLine("!! clamp z to triangle " + (position - npos).Norm2D());
//         position = npos;
//
//         // Find the edge of our container triangle that we're walking towards 
//         int opposingVertexIndex;
//         if (!GeometryOperations.TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(position.XY, preferredDirectionUnit, ref triangle, out opposingVertexIndex)) {
//            // Resolve if we're not inside the triangle.
//            if (!allowPushIntoTriangle) {
//               Console.WriteLine("Warning: Pushed into triangle, but immediately not in triangle?");
//               nextPosition = position;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.CanPushInward;
//            }
//            Console.WriteLine("Fix?");
//
//            // If this fails, we're confused as to whether we're in the triangle or not, because we're on an
//            // edge and floating point arithmetic error makes us confused. Simply push us slightly into the triangle
//            // by pulling us towards its centroid
//            // (A previous variant pulled based on perp of nearest edge, however the results are probably pretty similar)
//            var offsetToCentroid = position.To(triangle.Centroid);
//            if (offsetToCentroid.Norm2D() < TerrainConstants.TriangleEdgeBufferRadius) {
//               Console.WriteLine("Warning: Triangle width less than edge buffer radius!");
//               nextPosition = triangle.Centroid;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.PushInward;
//            } else {
//               nextPosition = position + offsetToCentroid.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.PushInward;
//            }
//         }
//
//         // Let d = remaining "preferred" motion.
//         var d = ZIfyVectorOnTriangleBasis(preferredDirectionUnit, ref triangle).ToUnit() * distanceRemaining;
//
//         // Project p-e0 onto perp(e0-e1) to find shortest vector from position to edge.
//         // Intuitively an edge direction and the direction's perp form a vector
//         // space. A point within the triangle's offset from a vertex (which has two edges)
//         // is the sum of vector to point on nearest edge and vector from that point to the 
//         // vertex. These vectors are orthogonal, so intuitively if we project onto the perp
//         // we'll isolate the perp component.
//         var e0 = triangle.Points[(opposingVertexIndex + 1) % 3];
//         var e1 = triangle.Points[(opposingVertexIndex + 2) % 3];
//         var e01 = e0.To(e1);
//         var e01Perp = e01.Cross(triangle.Normal); // points outside of current triangle, perp to edge we're crossing, on triangle plane.
//         Trace.Assert(triangle.Centroid.To(e0).ProjectOntoComponentD(e01Perp) > 0);
//
//         var pe0 = position.To(e0);
//         var pToEdge = pe0.ProjectOnto(e01Perp); // perp to plane normal.
//
//         // If we're sitting right on the edge, push us into the triangle before doing any work
//         // Otherwise, it can be ambiguous as to what edge we're passing through on exit.
//         // Don't delete this or we'll crash.
//         if (pToEdge.Norm2D() < GeometryOperations.kEpsilon) {
//            nextPosition = position - e01Perp.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//            nextTriangleIndex = triangleIndex;
//            return WalkResult.Progress; // is this the best result?
//         }
//
//         // Project d onto pToEdge to see if we're moving beyond edge boundary
//         var pToEdgeComponentRemaining = d.ProjectOntoComponentD(pToEdge);
//         Debug.Assert(GeometryOperations.IsReal(pToEdgeComponentRemaining));
//
//         if (pToEdgeComponentRemaining < 1) {
//            // Motion finishes within triangle.
//            // TODO: Handle when this gets us very close to triangle edge e.g. cR = 0.99999.
//            // (We don't want to fall close to the triangle edge but no longer in the triangle
//            // due to floating point error)
//            nextPosition = position + d;
//            nextTriangleIndex = triangleIndex;
//            return WalkResult.Completion;
//         }
//
//         // Proposed motion would finish outside the triangle
//         var neighborTriangleIndex = triangle.NeighborOppositePointIndices[opposingVertexIndex];
//         var dToEdge = d / pToEdgeComponentRemaining;
//         Debug.Assert(GeometryOperations.IsReal(dToEdge));
//
//         if (neighborTriangleIndex != Triangle3.NO_NEIGHBOR_INDEX) {
//            // Move towards and past the edge between us and the other triangle.
//            // There's a potential bug here where the other triangle is a sliver.
//            // The edge buffer radius could potentially move us past TWO of its edges, out of it.
//            // In practice, this bug happens OFTEN and is counteracted by the in-hole hack-fix.
//            var dToAndPastEdge = dToEdge + dToEdge.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//            nextPosition = position + dToAndPastEdge;
//            nextTriangleIndex = neighborTriangleIndex;
//            return WalkResult.Progress;
//         } else {
//            // We're running into an edge! First, place us as close to the edge as possible.
//            var dToNearEdge = dToEdge - dToEdge.ToUnit() * TerrainConstants.TriangleEdgeBufferRadius;
//            var pNearEdge = position + dToNearEdge;
//
//            // We have this guard so if we're edge following, we don't start an inner loop that's also
//            // edge following... which would probably lead to a stack overflow
//            if (!allowEdgeFollow) {
//               Console.WriteLine("Warning: Could edge follow, but was instructed not to?");
//               nextPosition = pNearEdge;
//               nextTriangleIndex = triangleIndex;
//               return WalkResult.CanEdgeFollow;
//            }
//
//            // We want to follow the edge, potentially past it if possible.
//            // Figure out which edge vertex we're walking towards
//            var walkToEdgeVertex1 = d.ProjectOntoComponentD(e01) > 0;
//            var vertexToWalkTowards = walkToEdgeVertex1 ? e1 : e0;
//            var directionToWalkAlongEdge = walkToEdgeVertex1 ? e01 : -1.0 * e01;
//            var directionToWalkAlongEdgeUnit = directionToWalkAlongEdge.ToUnit();
//
//            // start tracking p/drem independently.
//            var p = pNearEdge;
//            var ti = triangleIndex;
//            var drem = dToNearEdge.Norm2D();
//            var allowPushInward = true;
//            while (drem > GeometryOperations.kEpsilon) {
//               DoubleVector3 np;
//               int nti;
//               var wres = WalkTriangle(
//                  pNearEdge,
//                  directionToWalkAlongEdgeUnit.XY,
//                  distanceRemaining - dToNearEdge.Norm2D(),
//                  island,
//                  ti,
//                  allowPushInward,
//                  false,
//                  out np,
//                  out nti
//               );
//               switch (wres) {
//                  case WalkResult.Completion:
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Completion;
//                  case WalkResult.CanEdgeFollow:
//                     // This is an error, so we just finish
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Completion;
//                  case WalkResult.Progress:
//                     // Woohoo! Walking along edge brought us into another triangle
//                     Trace.Assert(ti != nti);
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Progress;
//                  case WalkResult.PushInward:
//                     p = np; // HAHA
//                     ti = nti;
//                     allowPushInward = false;
//                     continue;
//                  case WalkResult.CanPushInward:
//                     nextPosition = np;
//                     nextTriangleIndex = nti;
//                     return WalkResult.Completion;
//               }
//            }
//
//            nextPosition = p;
//            nextTriangleIndex = ti;
//            return WalkResult.Completion;
//
//            //            // Which edge would we be crossing if we walked along e01 past the vertex?
//            //            // If we're walking along e01 past e1, then we're hitting e12 (across 0, keep 1)
//            //            // If we're walking along e01 past e0, then we're hitting e20 (across 1, keep 0)
//            //            // we'll denote the new edge eab
//            //            var e2 = triangle.Points[opposingVertexIndex];
//            //            var ea = walkToEdgeVertex1 ? e1 : e2;
//            //            var eb = walkToEdgeVertex1 ? e2 : e0;
//            //
//            //            var vertexIndexOpposingEab =
//            //               walkToEdgeVertex1
//            //                  ? (opposingVertexIndex + 1) % 3
//            //                  : (opposingVertexIndex + 2) % 3;
//            //
//            //            var otherNeighborTriangleIndex = triangle.NeighborOppositePointIndices[vertexIndexOpposingEab];
//            //            if (otherNeighborTriangleIndex == Triangle.NO_NEIGHBOR_INDEX) {
//            //               // No neighbor exists, so we're walking towards a corner.
//            //               return WalkTriangle(
//            //                  pNearEdge,
//            //                  directionToWalkAlongEdge,
//            //                  distanceRemaining - dToNearEdge.Norm2D(),
//            //                  island,
//            //                  triangleIndex,
//            //                  true,
//            //                  false);
//            //            }
//            //            // Neighbor exists, so walk until we get into its triangle...
//            //            return WalkTriangle(
//            //               pNearEdge,
//            //               directionToWalkAlongEdge,
//            //               distanceRemaining - dToNearEdge.Norm2D(),
//            //               island,
//            //               triangleIndex,
//            //               true,
//            //               false);
//         }
      }
   }

   public class StatusComponent : EntityComponent {
      public StatusComponent() : base(EntityComponentType.Status) { }
   }

   public class StatsCalculator {
      public double ComputeCharacterRadius(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) return 0;
         return movementComponent.BaseRadius;
      }

      public double ComputeMovementSpeed(Entity entity) {
         var movementComponent = entity.MovementComponent;
         if (movementComponent == null) return 0;
         return movementComponent.BaseSpeed;
      }
   }

   public class StatusSystemService : EntitySystemService {
      private static readonly EntityComponentsMask kComponentMask = ComponentMaskUtils.Build(EntityComponentType.Status);

      public StatusSystemService(EntityService entityService) : base(entityService, kComponentMask) { }

      public override void Execute() { }
   }
}

