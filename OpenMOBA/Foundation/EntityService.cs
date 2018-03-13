using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Foundation.Terrain.Visibility;
using OpenMOBA.Geometry;
using cInt = System.Int32;

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
      public DoubleVector3 Position { get; set; }
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

      // poor datastructure use, but irrelevant for perf 
      public List<DoubleVector3> PathingBreadcrumbs { get; set; } = new List<DoubleVector3>();

      public Swarm Swarm { get; set; }
      public TriangulationIsland SwarmingIsland { get; set; }
      public int SwarmingTriangleIndex { get; set; }

      public List<Tuple<DoubleVector3, DoubleVector3>> DebugLines { get; set; }

      // Values precomputed at entry of movement service
      public IntVector2 DiscretizedPosition { get; set; }

      public int ComputedRadius { get; set; }
      public int ComputedSpeed { get; set; }
      public DoubleVector2 WeightedSumNBodyForces { get; set; }
      public double SumWeightsNBodyForces { get; set; }

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

      public bool TryFindPath(double holeDilationRadius, DoubleVector3 sourceWorld, DoubleVector3 destinationWorld, out MotionRoadmap roadmap) {
         roadmap = null;
         var terrainSnapshot = terrainService.CompileSnapshot();
         var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);
         if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(sourceWorld.ToDotNetVector(), out var sourceNode)) return false;
         if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(destinationWorld.ToDotNetVector(), out var destinationNode)) return false;

         var sourceLocal = Vector3.Transform(sourceWorld.ToDotNetVector(), sourceNode.SectorNodeDescription.WorldTransformInv);
         var destinationLocal = Vector3.Transform(destinationWorld.ToDotNetVector(), destinationNode.SectorNodeDescription.WorldTransformInv);

         return TryFindPath(
            sourceNode, 
            sourceLocal.ToOpenMobaVector().LossyToIntVector3().XY, 
            destinationNode, 
            destinationLocal.ToOpenMobaVector().LossyToIntVector3().XY,
            out roadmap);
      }

      public bool TryFindPath(TerrainOverlayNetworkNode sourceNode, IntVector2 sourcePoint, TerrainOverlayNetworkNode destinationNode, IntVector2 destinationPoint, out MotionRoadmap result) {
         var roadmap = new MotionRoadmap();

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


         var q = new PriorityQueue<(float, float, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge)>((a, b) => a.Item1.CompareTo(b.Item1));
         var priorityUpperBounds = new Dictionary<(TerrainOverlayNetworkNode, int), float>();
         var predecessor = new Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge)>(); // visited

         foreach (var kvp in sourceNode.OutboundEdgeGroups) {
            foreach (var g in kvp.Value) {
               foreach (var edge in g.Edges) {
                  var cpiLink = sourceOptimalLinkToCrossovers[edge.SourceCrossoverIndex];
                  priorityUpperBounds[(sourceNode, edge.SourceCrossoverIndex)] = cpiLink.TotalCost * sourceNode.SectorNodeDescription.LocalToWorldScalingFactor;
                  q.Enqueue((cpiLink.TotalCost, cpiLink.TotalCost, sourceNode, SOURCE_POINT_CPI, sourceNode, edge.SourceCrossoverIndex, null));
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

         while (!q.IsEmpty) {
            var (_, ncost, nsrcnode, nsrccpi, ndstnode, ndstcpi, nedge) = q.Dequeue();
            dequeueCount++;
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

                  q.Enqueue((sprior, scost, ndstnode, ndstcpi, ndstnode, cpi, null));
                  enqueueCount++;
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
                           q.Enqueue((sprior, scost, ndstnode, ndstcpi, g.Destination, edge.DestinationCrossoverIndex, edge));
                           enqueueCount++;
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
               q.Enqueue((sprior, scost, ndstnode, ndstcpi, destinationNode, DESTINATION_POINT_CPI, null));
               enqueueCount++;

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
         throw new NotImplementedException();
         var movementComponent = entity.MovementComponent;

         var holeDilationRadius = statsCalculator.ComputeCharacterRadius(entity) + TerrainConstants.AdditionalHoleDilationRadius;
//         List<DoubleVector3> pathPoints;
//         if (!pathfinderCalculator.TryFindPath(holeDilationRadius, movementComponent.Position, destination, out pathPoints)) movementComponent.PathingBreadcrumbs.Clear();
//         else movementComponent.PathingBreadcrumbs = pathPoints;
         movementComponent.PathingIsInvalidated = false;
         movementComponent.PathingDestination = destination;
      }

      public void HandleHoleAdded(DynamicTerrainHoleDescription holeDescription) {
         InvalidatePaths();

         foreach (var entity in AssociatedEntities) {
            var characterRadius = statsCalculator.ComputeCharacterRadius(entity);
            var paddedHoleDilationRadius = characterRadius + TerrainConstants.AdditionalHoleDilationRadius + TerrainConstants.TriangleEdgeBufferRadius;
            if (holeDescription.ContainsPoint(paddedHoleDilationRadius, entity.MovementComponent.Position)) FixEntityInHole(entity);
         }
      }

      private void FixEntityInHole(Entity entity) {
         var computedRadius = statsCalculator.ComputeCharacterRadius(entity);
         var movementComponent = entity.MovementComponent;
         movementComponent.Position = PushToLand(movementComponent.Position, computedRadius);
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
         return;
         //         goto the_new_code;
         //         goto derp;
         //         foreach (var swarm in AssociatedEntities.Where(e => e.MovementComponent.Swarm != null).Select(e => e.MovementComponent.Swarm).Distinct().ToArray()) {
         //            var destination = swarm.Destination;
         //            foreach (var swarmling in swarm.Entities) {
         //               // seek to point
         //               var seekUnit = (destination - swarmling.MovementComponent.Position).ToUnit();
         //               var vs = new List<Tuple<double, DoubleVector2>>();
         //               vs.Add(Tuple.Create(100.0, seekUnit));
         //
         //               foreach (var other in swarm.Entities) {
         //                  if (other == swarmling) continue;
         //                  var selfToOther = other.MovementComponent.Position - swarmling.MovementComponent.Position;
         //                  var selfToOtherMagnitude = selfToOther.Norm2D();
         //                  var regroupWeight = Math.Max(10000.0, selfToOtherMagnitude * selfToOtherMagnitude) / 10000.0;
         //                  var separateWeight = 0.0;
         //                  var mul = selfToOtherMagnitude < 20 ? 5 : 0.1;
         //                  var separateFactor = 1.0 / (selfToOtherMagnitude * selfToOtherMagnitude * selfToOtherMagnitude + 1);
         //                  separateWeight = 1280000 * mul * separateFactor;
         //                  var wtot = (0.01 * regroupWeight - separateWeight) * 0.5;
         //                  if (wtot > 0) {
         //                     vs.Add(Tuple.Create(wtot, selfToOther.ToUnit()));
         //                  } else {
         //                     vs.Add(Tuple.Create(-wtot, -1.0 * selfToOther.ToUnit()));
         //                  }
         //                  //                  vs.Add(Tuple.Create(regroupWeight - separateWeight, selfToOther.ToUnit()));
         //                  //                  vs.Add(Tuple.Create(regroupWeight, selfToOther.ToUnit()));
         //                  //                  vs.Add(Tuple.Create(separateWeight, -1.0 * selfToOther.ToUnit()));
         //               }
         //
         //               var wsumvs = vs.Aggregate(new DoubleVector2(), (cur, it) => cur + it.Item1 * it.Item2);
         //               var wsumvsw = vs.Sum(it => it.Item1);
         //               var wavs = wsumvs / wsumvsw;
         //               swarmling.MovementComponent.WeightedSumNBodyForces = wavs;
         //            }
         //         }
         //         goto derp;

         //      the_new_code:
         //         var entities = AssociatedEntities.ToArray();
         //
         //         // Precompute computed entity stats
         //         for (var i = 0; i < entities.Length; i++) {
         //            var e = entities[i];
         //            e.MovementComponent.DiscretizedPosition = e.MovementComponent.Position.XY.LossyToIntVector2();
         //            e.MovementComponent.ComputedRadius = (int)Math.Ceiling(statsCalculator.ComputeCharacterRadius(e));
         //            e.MovementComponent.ComputedSpeed = (int)Math.Ceiling(statsCalculator.ComputeMovementSpeed(e));
         //            e.MovementComponent.WeightedSumNBodyForces = DoubleVector2.Zero;
         //            e.MovementComponent.SumWeightsNBodyForces = 0;
         //         }
         //
         //         // Only operate on movement components and swarms onward.
         //         var movementComponents = entities.Select(e => e.MovementComponent).ToArray();
         //         var swarms = entities.Select(e => e.MovementComponent.Swarm)
         //                              .Where(s => s != null)
         //                              .Distinct()
         //                              .ToArray();
         //
         //         // for each entity, update triangle
         //         for (var i = 0; i < movementComponents.Length; i++) {
         //            var a = movementComponents[i];
         //            if (a.Swarm == null) continue;
         //
         //            var terrainSnapshot = terrainService.BuildSnapshot();
         //            var triangulation = terrainSnapshot.ComputeTriangulation(a.ComputedRadius);
         //
         //            TriangulationIsland island;
         //            int triangleIndex;
         //            if (!triangulation.TryIntersect(a.Position.X, a.Position.Y, out island, out triangleIndex)) {
         //               Console.WriteLine("Warning: Entity not on land.");
         //               a.Position = PushToLand(a.Position, a.ComputedRadius);
         //
         //               if (!triangulation.TryIntersect(a.Position.X, a.Position.Y, out island, out triangleIndex)) {
         //                  Console.WriteLine("Warning: fixing entity not on land failed?");
         //                  continue;
         //               }
         //            }
         //
         //            a.SwarmingIsland = island;
         //            a.SwarmingTriangleIndex = triangleIndex;
         //         }
         //
         //         // for each (int, triangleIndex, island, dest) tuple, compute optimal direction (or 0, 0)
         //         var vectorField = new Dictionary<Tuple<int, int, TriangulationIsland, DoubleVector3>, DoubleVector3>();
         //         foreach (var swarm in swarms) {
         //            var dest = swarm.Destination;
         //            foreach (var entity in swarm.Entities) {
         //               var mc = entity.MovementComponent;
         //               var k = Tuple.Create(mc.ComputedRadius, mc.SwarmingTriangleIndex, mc.SwarmingIsland, dest);
         //               if (vectorField.ContainsKey(k)) {
         //                  continue;
         //               }
         //
         //               var triangleCentroid = mc.SwarmingIsland.Triangles[mc.SwarmingTriangleIndex].Centroid;
         //               var dilation = mc.ComputedRadius + TerrainConstants.AdditionalHoleDilationRadius;
         //               List<DoubleVector3> path;
         //               if (!pathfinderCalculator.TryFindPath(dilation, triangleCentroid, dest, out path) || path.Count < 2) {
         //                  vectorField[k] = DoubleVector3.Zero;
         //               } else {
         //                  vectorField[k] = (path[1] - path[0]).ToUnit();
         //               }
         //            }
         //         }
         //
         //         // for each (island, dest) compute spanning dijkstras of tree centroids to dest triangle
         //         var ds = new Dictionary<Tuple<TriangulationIsland, DoubleVector3>, Dictionary<int, int>>();
         //         foreach (var swarm in swarms) {
         //            for (var i = 0; i < swarm.Entities.Count; i++) {
         //               var entity = swarm.Entities[i];
         //               Dictionary<int, int> d;
         //               var key = Tuple.Create(entity.MovementComponent.SwarmingIsland, swarm.Destination);
         //               if (ds.ContainsKey(key)) {
         //                  continue;
         //               }
         //
         //               NN(entity.MovementComponent.SwarmingIsland, swarm.Destination, out d);
         //               ds[key] = d;
         //            }
         //         }
         //
         //         // for each entity pairing, compute separation force vector which prevents overlap
         //         // and "regroup" force vector, which causes clustering within swarms.
         //         // Logic contained within should be scale invariant!
         //         for (var i = 0; i < movementComponents.Length - 1; i++) {
         //            var a = movementComponents[i];
         //            var aRadius = a.ComputedRadius;
         //            for (var j = i + 1; j < movementComponents.Length; j++) {
         //               var b = movementComponents[j];
         //               var aToB = b.DiscretizedPosition - a.DiscretizedPosition;
         //
         //               var radiusSum = aRadius + b.ComputedRadius;
         //               var radiusSumSquared = radiusSum * radiusSum;
         //               var centerDistanceSquared = aToB.SquaredNorm2();
         //
         //               // Must either be overlapping or in the same swarm for us to compute
         //               // (In the future rather than "in same swarm" probably want "allied".
         //               var isOverlapping = centerDistanceSquared < radiusSumSquared;
         //
         //               double w; // where 1 means equal in weight to isolated-unit pather
         //               IntVector2 aForce;
         //               if (isOverlapping) {
         //                  // Case: Overlapping, may or may not be in same swarm.
         //                  // Let D = radius sum
         //                  // Let d = center distance 
         //                  // Separate Force Weight: (k * (D - d) / D)^2
         //                  // Intuitively D-d represents overlapness.
         //                  // k impacts how quickly overlapping overwhelms seeking.
         //                  // k = 1: When fully overlapping
         //                  // k = 2: When half overlapped.
         //                  const int k = 16;
         //                  var centerDistance = IntMath.Sqrt(centerDistanceSquared);
         //                  w = IntMath.Square(k * (radiusSum - centerDistance)) / (double)radiusSumSquared;
         //                  Debug.Assert(GeometryOperations.IsReal(w));
         //
         //                  // And the force vector (outer code will tounit this)
         //                  aForce = aToB.SquaredNorm2() == 0
         //                     ? new IntVector2(2, 1)
         //                     : -1 * aToB;
         //               } else if (a.Swarm == b.Swarm && a.Swarm != null) {
         //                  // Case: Nonoverlapping, in same swarm. Push swarmlings near but nonoverlapping
         //                  // TODO: Alignment force.
         //                  const int groupingTolerance = 8;
         //                  var spacingBetweenBoundaries = IntMath.Sqrt(centerDistanceSquared) - radiusSum;
         //                  var maxAttractionDistance = radiusSum * groupingTolerance;
         //
         //                  if (spacingBetweenBoundaries > maxAttractionDistance)
         //                     continue;
         //
         //                  // regroup = ((D - d) / D)^4
         //                  w = 0.001 * (double)Math.Pow(spacingBetweenBoundaries - maxAttractionDistance, 4.0) / Math.Pow(maxAttractionDistance, 4.0);
         //                  Debug.Assert(GeometryOperations.IsReal(w));
         //
         //                  aForce = aToB;
         //               } else {
         //                  // todo: experiment with continue vs zero-weight for no failed branch prediction
         //                  // (this is pretty pipeliney code)
         //                  continue;
         //               }
         //
         //
         //               var wf = w * aForce.ToDoubleVector2().ToUnit();
         //               Debug.Assert(GeometryOperations.IsReal(wf));
         //               Debug.Assert(GeometryOperations.IsReal(w));
         //
         //               a.WeightedSumNBodyForces += wf;
         //               a.SumWeightsNBodyForces += w;
         //
         //               b.WeightedSumNBodyForces -= wf;
         //               b.SumWeightsNBodyForces += w;
         //            }
         //
         //            if (a.Swarm == null) continue;
         //
         //
         //            var seekAggregate = DoubleVector2.Zero;
         //            var seekWeightAggregate = 0.0;
         //
         //            var d = ds[Tuple.Create(a.SwarmingIsland, a.Swarm.Destination)];
         //            int nti;
         //            if (d != null && d.TryGetValue(a.SwarmingTriangleIndex, out nti) && nti != Triangle3.NO_NEIGHBOR_INDEX) {
         //               var triangleCentroidDijkstrasOptimalSeekUnit = (a.SwarmingIsland.Triangles[nti].Centroid - a.SwarmingIsland.Triangles[a.SwarmingTriangleIndex].Centroid).XY.ToUnit();
         //               const double mul = 0.3;
         //               seekAggregate += mul * triangleCentroidDijkstrasOptimalSeekUnit;
         //               seekWeightAggregate += mul;
         //            }
         //               
         //            var key = Tuple.Create(a.ComputedRadius, a.SwarmingTriangleIndex, a.SwarmingIsland, a.Swarm.Destination);
         //            var triangleCentroidOptimalSeekUnit = vectorField[key];
         //            seekAggregate += triangleCentroidOptimalSeekUnit.XY;
         //            seekWeightAggregate += 1.0;
         //
         //            // var directionalSeekUnit = (a.Swarm.Destination - a.Position).ToUnit();
         //            // seekAggregate += directionalSeekUnit;
         //            // seekWeightAggregate += 1.0;
         //
         //            var seekUnit = seekWeightAggregate < GeometryOperations.kEpsilon || seekAggregate.SquaredNorm2D() < GeometryOperations.kEpsilon ? DoubleVector2.Zero : seekAggregate.ToUnit();
         //
         //            const double seekWeight = 1.0;
         //            a.WeightedSumNBodyForces += seekWeight * seekUnit;
         //            a.SumWeightsNBodyForces += seekWeight;
         //            a.SwarmlingVelocity = (a.WeightedSumNBodyForces / a.SumWeightsNBodyForces) * a.ComputedSpeed;
         //            Debug.Assert(GeometryOperations.IsReal(a.SwarmlingVelocity));
         //         }
         //
         //
         //
         //         // foreach swarmling, compute vector to dest and vector recommended by triangulation
         ////         foreach (var swarm in swarms) {
         ////            var swarmDestination = swarm.Destination;
         ////            foreach (var e in swarm.Entities) {
         ////               var movementComponent = e.MovementComponent;
         ////            }
         ////         }
         //
         ////         foreach (var entity in AssociatedEntities) {
         ////            var movementComponent = entity.MovementComponent;
         ////            if (movementComponent.Swarm != null) {
         ////               if (movementComponent.PathingIsInvalidated) {
         ////                  ExecutePathSwarmer(entity, movementComponent);
         ////               }
         ////            }
         ////         }
         //
         //derp:
         foreach (var entity in AssociatedEntities) {
            var movementComponent = entity.MovementComponent;
            if (movementComponent.PathingIsInvalidated) Pathfind(entity, movementComponent.PathingDestination);

            if (movementComponent.Swarm == null) ExecutePathNonswarmer(entity, movementComponent);
            else ExecutePathSwarmer(entity, movementComponent);
         }
      }

      private void ExecutePathNonswarmer(Entity entity, MovementComponent movementComponent) {
         if (!movementComponent.PathingBreadcrumbs.Any()) return;

         var movementSpeed = statsCalculator.ComputeMovementSpeed(entity);
         var distanceRemaining = movementSpeed * gameTimeService.SecondsPerTick;
         while (distanceRemaining > 0 && movementComponent.PathingBreadcrumbs.Any()) {
            // vect from position to next pathing breadcrumb
            var pb = movementComponent.PathingBreadcrumbs[0] - movementComponent.Position;
            movementComponent.LookAt = pb;

            // |pb| - distance to next pathing breadcrumb
            var d = pb.Norm2D();

            if (Math.Abs(d) <= float.Epsilon || d <= distanceRemaining) {
               movementComponent.Position = movementComponent.PathingBreadcrumbs[0];
               movementComponent.PathingBreadcrumbs.RemoveAt(0);
               distanceRemaining -= d;
            } else {
               movementComponent.Position += pb * distanceRemaining / d;
               distanceRemaining = 0;
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

