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

      public List<Tuple<DoubleVector3, DoubleVector3>> DebugLines { get; set; }

      // Values precomputed at entry of movement service
      public int ComputedRadius { get; set; }
      public int ComputedSpeed { get; set; }
      public DoubleVector2 WeightedSumNBodyForces { get; set; }
      public double SumWeightsNBodyForces { get; set; }
      public TerrainOverlayNetwork TerrainOverlayNetwork { get; set; }
      public TerrainOverlayNetworkNode TerrainOverlayNetworkNode { get; set; }
      public DoubleVector2 LocalPosition { get; set; }
      public TriangulationIsland SwarmingIsland { get; set; }
      public int SwarmingTriangleIndex { get; set; }

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
            for (var i = 0; i < sourceVisibleWaypointLinksLength; i++) {
               var link = sourceVisibleWaypointLinks[i];
               var firstWaypoint = link.PriorIndex;
               var cost = link.TotalCost + destinationOptimalLinkToWaypoints[firstWaypoint].TotalCost;
               if (cost < bestFirstWaypointCost) {
                  bestFirstWaypoint = firstWaypoint;
                  bestFirstWaypointCost = cost;
               }
            }

            roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourcePoint, sourceNode.CrossoverPointManager.Waypoints[bestFirstWaypoint]));
            AddInterTerrainOverlayNetworkNodeWaypointToWaypointRoadmapActions(roadmap, sourceNode, bestFirstWaypoint, destinationOptimalLinkToWaypoints[bestFirstWaypoint].PriorIndex);
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

               Console.WriteLine("Number of nodes visited: " + predecessor.Count);
               Console.WriteLine("Upper bounds: " + priorityUpperBounds.Count);

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
                        AddInterTerrainOverlayNetworkNodeWaypointToWaypointRoadmapActions(roadmap, sourceNode, firstLink.PriorIndex, lastLink.PriorIndex);
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
                        AddInterTerrainOverlayNetworkNodeWaypointToWaypointRoadmapActions(roadmap, a.Item1, firstLink.PriorIndex, lastLink.PriorIndex);
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
                        AddInterTerrainOverlayNetworkNodeWaypointToWaypointRoadmapActions(roadmap, destinationNode, firstLink.PriorIndex, lastLink.PriorIndex);
                        roadmap.Plan.Add(new MotionRoadmapWalkAction(destinationNode, destinationNode.CrossoverPointManager.Waypoints[lastLink.PriorIndex], destinationPoint));
                     }
                  }
               }

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

      void AddInterTerrainOverlayNetworkNodeWaypointToWaypointRoadmapActions(MotionRoadmap roadmap, TerrainOverlayNetworkNode node, int sourceWaypoint, int destinationWaypoint) {
         var cpm = node.CrossoverPointManager;
         var waypointToWaypointLut = cpm.WaypointToWaypointLut;
         var sourcePath = new List<PathLink>();
         var destPath = new List<PathLink>();

         // must query with [a][b] where a > b
         // recall waypointToWaypointLut is [destination][source]
         var sourceFinger = sourceWaypoint;
         var destinationFinger = destinationWaypoint;
         while (sourceFinger != destinationFinger) {
            if (sourceFinger > destinationFinger) {
               var link = waypointToWaypointLut[sourceFinger][destinationFinger];
               destPath.Add(link);
               if (link.PriorIndex == destinationFinger) break;
               destinationFinger = link.PriorIndex;
            } else {
               var link = waypointToWaypointLut[destinationFinger][sourceFinger];
               sourcePath.Add(link);
               if (link.PriorIndex == sourceFinger) break;
               sourceFinger = link.PriorIndex;
            }
            // todo: this sometimes happens with the floyd-warshall implementation but not dijkstras
            //               Trace.Assert(sourceFinger != destinationFinger);
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

