using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Geometry;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain.Pathfinding {
   public class PathfinderCalculator {
      private readonly StatsCalculator statsCalculator;
      private readonly TerrainFacade terrainFacade;

      public PathfinderCalculator(TerrainFacade terrainFacade, StatsCalculator statsCalculator) {
         this.terrainFacade = terrainFacade;
         this.statsCalculator = statsCalculator;
      }

      public bool IsDestinationReachable(cDouble holeDilationRadius, DoubleVector3 sourceWorld, DoubleVector3 destinationWorld) {
         var snapshot = terrainFacade.CompileSnapshot();
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

      public bool TryFindPath(cDouble agentRadius, DoubleVector3 sourceWorld, DoubleVector3 destinationWorld, out MotionRoadmap roadmap, IDebugCanvas debugCanvas = null) {
         roadmap = null;
         var terrainSnapshot = terrainFacade.CompileSnapshot();
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
         if (debugCanvas != null) {
            debugCanvas.Transform = Matrix4x4.Identity;
         }

         if (sourceNode == destinationNode) {
            var roadmap = new MotionRoadmap();
            if (sourcePoint == destinationPoint || sourceNode.LandPolyNode.SegmentInLandPolygonNonrecursive(sourcePoint, destinationPoint)) {
               roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourcePoint, destinationPoint));
               result = roadmap;
               return true;
            }

            var sourceVisibleWaypointLinks = sourceNode.CrossoverPointManager.FindVisibleWaypointLinks(sourcePoint, null, out var sourceVisibleWaypointLinksLength, out var sourceOptimalLinkToWaypoints);
            var destinationVisibleWaypointLinks = sourceNode.CrossoverPointManager.FindVisibleWaypointLinks(destinationPoint, null, out var destinationVisibleWaypointLinksLength, out var destinationOptimalLinkToWaypoints);

            var bestFirstWaypoint = -1;
            var bestFirstWaypointCost = cDouble.MaxValue;
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

//         Console.WriteLine("Src had " + sourceNode.CrossoverPointManager.CrossoverPoints.Count + " : " + string.Join(", ", sourceNode.CrossoverPointManager.CrossoverPoints));
         var (_, _, _, sourceOptimalLinkToCrossovers) = sourceNode.CrossoverPointManager.FindOptimalLinksToCrossovers(sourcePoint);
         var (_, _, _, destinationOptimalLinkToCrossovers) = destinationNode.CrossoverPointManager.FindOptimalLinksToCrossovers(destinationPoint);

         var q = new PriorityQueue<ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge>>((a, b) => a.Item1.CompareTo(b.Item1));
         var priorityUpperBounds = new Dictionary<(TerrainOverlayNetworkNode, int), cDouble>();
         var predecessor = new Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge, cDouble)>(); // visited

         foreach (var kvp in sourceNode.OutboundEdgeGroups) {
            foreach (var g in kvp.Value) {
               foreach (var edge in g.Edges) {
                  var cpiLink = sourceOptimalLinkToCrossovers[edge.SourceCrossoverIndex];
                  var worldCpiLinkCost = cpiLink.TotalCost * sourceNode.SectorNodeDescription.LocalToWorldScalingFactor;
                  priorityUpperBounds[(sourceNode, edge.SourceCrossoverIndex)] = worldCpiLinkCost;
                  q.Enqueue((worldCpiLinkCost, worldCpiLinkCost, sourceNode, SOURCE_POINT_CPI, sourceNode, edge.SourceCrossoverIndex, null));
                  //Console.WriteLine("Init link: " + cpiLink.TotalCost + " " + edge.SourceCrossoverIndex + " of " + sourceNode.CrossoverPointManager.CrossoverPoints.Count);
               }
            }
         }

         int enqueueCount = 0;
         int dequeueCount = 0;
         bool terminalEnqueued = false;

         var destinationWorld = Vector3.Transform(new Vector3(destinationPoint.X, destinationPoint.Y, 0), destinationNode.SectorNodeDescription.WorldTransform);

         // TODO: Determinism
         cDouble ComputeHeuristic(TerrainOverlayNetworkNode n, int cpi) {
            var cp = n.CrossoverPointManager.CrossoverPoints[cpi];
            var cpw = Vector3.Transform(new Vector3(cp.X, cp.Y, 0), n.SectorNodeDescription.WorldTransform);
            return (cDouble)Vector3.Distance(destinationWorld, cpw) * (cDouble)1;
         }

         void DrawPqItem(ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> item, StrokeStyle strokeStyle) {
            var (_, ncost, nsrcnode, nsrccpi, ndstnode, ndstcpi, nedge) = item;
            DebugDrawLine(
               debugCanvas,
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
            predecessor[(ndstnode, ndstcpi)] = (nsrcnode, nsrccpi, nedge, ncost);

            if (ndstcpi == DESTINATION_POINT_CPI) {
               Console.WriteLine("Success! Dequeues: " + dequeueCount + " and enqueues: " + enqueueCount);

               Console.WriteLine("Number of nodes visited: " + predecessor.Count);
               Console.WriteLine("Upper bounds: " + priorityUpperBounds.Count);

               Trace.Assert(ndstnode == destinationNode);
               Trace.Assert(ndstcpi == DESTINATION_POINT_CPI);

               if (!TryBacktrack(
                  sourceNode, sourcePoint,
                  destinationNode, destinationPoint,
                  predecessor,
                  DESTINATION_POINT_CPI,
                  sourceOptimalLinkToCrossovers,
                  destinationOptimalLinkToCrossovers,
                  out result)) {
                  throw new InvalidStateException();
               }
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
                  Trace.Assert(link.TotalCost >= CDoubleMath.c0);

                  if (priorityUpperBounds.TryGetValue((ndstnode, cpi), out cDouble scostub) && scostub <= scost) {
                     continue;
                  }
                  priorityUpperBounds[(ndstnode, cpi)] = scost;

                  ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> nitem = (sprior, scost, ndstnode, ndstcpi, ndstnode, cpi, null);
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
                           var scost = ncost + (cDouble)edge.Cost; // no need for scaling factor
                           var sprior = scost + ComputeHeuristic(g.Destination, edge.DestinationCrossoverIndex);
                           Trace.Assert(edge.Cost >= 0);

                           if (priorityUpperBounds.TryGetValue((g.Destination, edge.DestinationCrossoverIndex), out var scostub) && scostub <= scost) {
                              continue;
                           }
                           priorityUpperBounds[(g.Destination, edge.DestinationCrossoverIndex)] = scost;
                           ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> nitem = (sprior, scost, ndstnode, ndstcpi, g.Destination, edge.DestinationCrossoverIndex, edge);
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
               Trace.Assert(link.TotalCost >= CDoubleMath.c0);

               var scost = ncost + link.TotalCost * destinationNode.SectorNodeDescription.LocalToWorldScalingFactor;
               var sprior = scost + CDoubleMath.c0;
               ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> nitem = (sprior, scost, ndstnode, ndstcpi, destinationNode, DESTINATION_POINT_CPI, null);
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

      // Optimized for one source many destinations
      // (In multi-unit pathfind we do inverse search, where source is the goal, destinations are where entities are,
      // edges followed backward, so this function leverages cache to compute sourceOptimalLinkToWaypoints, and
      // destinations only need compute visible waypoint links.)
      public static bool TryBacktrack(
         TerrainOverlayNetworkNode sourceNode, IntVector2 sourcePoint,
         TerrainOverlayNetworkNode destinationNode, IntVector2 destinationPoint,
         Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge, cDouble)> predecessor,
         int DESTINATION_POINT_CPI,
         ExposedArrayList<PathLink> sourceOptimalLinkToCrossovers,
         ExposedArrayList<PathLink> destinationOptimalLinkToCrossovers,
         out MotionRoadmap roadmap
      ) {
         if (!predecessor.ContainsKey((destinationNode, DESTINATION_POINT_CPI))) {
            roadmap = null;
            return false;
         }

         // build high-level plan of path
         var path = new List<(TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge, cDouble)>();
         var cur = (destinationNode, DESTINATION_POINT_CPI, (TerrainOverlayNetworkEdge)null, CDoubleMath.c0);
         while (predecessor.TryGetValue((cur.Item1, cur.Item2), out var pred)) {
            path.Add(cur);
            var (psrcnode, psrccpi, pedge, psrcCpiTotalCost) = pred;
            cur = pred;
         }
         Trace.Assert(path.Count > 0);
         path.Add(cur);
         path.Reverse();

         // convert path to a motion plan. three cases for motion: moving from start to crossover, crossover to crossover, or crossover to end.
         // last one not processed, since we process pairwise.
         roadmap = new MotionRoadmap();
         for (var i = 0; i < path.Count - 1; i++) {
            if (i == 0) {
               // moving from start to crossover
               var nextCpi = path[1].Item2;

               // special case: move directly from start to goal
               if (nextCpi == DESTINATION_POINT_CPI) {
                  if (sourceNode.LandPolyNode.SegmentInLandPolygonNonrecursive(sourcePoint, destinationPoint)) {
                     roadmap.Plan.Add(new MotionRoadmapWalkAction(sourceNode, sourcePoint, destinationPoint));
                     continue;
                  }

                  var sourceVisibleWaypointLinks = sourceNode.CrossoverPointManager.FindVisibleWaypointLinks(sourcePoint, null, out var sourceVisibleWaypointLinksLength, out var sourceOptimalLinkToWaypoints);
                  var destinationVisibleWaypointLinks = sourceNode.CrossoverPointManager.FindVisibleWaypointLinks(destinationPoint, null, out var destinationVisibleWaypointLinksLength, out var destinationOptimalLinkToWaypoints);

                  if (sourceVisibleWaypointLinksLength == 0 || destinationVisibleWaypointLinksLength == 0) {
                     roadmap = null;
                     return false;
                  }

                  var bestFirstWaypoint = -1;
                  var bestFirstWaypointCost = cDouble.MaxValue;
                  for (var j = 0; j < sourceVisibleWaypointLinksLength; j++) {
                     var link = sourceVisibleWaypointLinks[j];
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
                  continue;
               }

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
         return true;
      }

      public PathfinderResultContext UniformCostSearch(cDouble agentRadius, DoubleVector3 sourceWorld, DoubleVector3[] destinationWorlds, bool followEdgesReversed, PathfinderResultContext pathfinderResultContext = null, IDebugCanvas debugCanvas = null) {
         var terrainSnapshot = terrainFacade.CompileSnapshot();
         var terrainOverlayNetwork = terrainSnapshot.OverlayNetworkManager.CompileTerrainOverlayNetwork(agentRadius);
         if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(sourceWorld.ToDotNetVector(), out var sourceNode, out var pSourceLocal)) return null;

         var destinationTuples = new (TerrainOverlayNetworkNode, IntVector2)[destinationWorlds.Length];
         for (var destinationIndex = 0; destinationIndex < destinationWorlds.Length; destinationIndex++) {
            var destinationWorld = destinationWorlds[destinationIndex];
            if (!terrainOverlayNetwork.TryFindTerrainOverlayNode(destinationWorld.ToDotNetVector(), out var destinationNode, out var pDestinationLocal)) return null;
            destinationTuples[destinationIndex] = (destinationNode, new IntVector2((int)pDestinationLocal.X, (int)pDestinationLocal.Y));
         }

         return UniformCostSearch(
            (sourceNode, new IntVector2((int)pSourceLocal.X, (int)pSourceLocal.Y)),
            destinationTuples,
            followEdgesReversed,
            pathfinderResultContext,
            debugCanvas);
      }

      public PathfinderResultContext UniformCostSearch((TerrainOverlayNetworkNode, IntVector2) source, (TerrainOverlayNetworkNode, IntVector2)[] destinations, bool followEdgesReversed, PathfinderResultContext pathfinderResultContext = null, IDebugCanvas debugCanvas = null) {
         var (sourceNode, sourcePoint) = source;
         if (pathfinderResultContext != null && !source.Equals(pathfinderResultContext.Source)) {
            throw new InvalidOperationException("Source differed pls");
         }

         const int SOURCE_POINT_CPI = int.MinValue;
         int ComputeDestinationIndexCpi(int destinationIndex) => -1 - destinationIndex;
         int ComputeDestinationIndexFromCpi(int cpi) => -(cpi + 1);

         MultiValueDictionary<TerrainOverlayNetworkNode, TerrainOverlayNetworkEdgeGroup> PickTraversedEdgeGroups(TerrainOverlayNetworkNode node) => followEdgesReversed ? node.InboundEdgeGroups : node.OutboundEdgeGroups;
         int PickFromCpi__(TerrainOverlayNetworkEdge edge) => followEdgesReversed ? edge.DestinationCrossoverIndex : edge.SourceCrossoverIndex;
         int PickToCpi__(TerrainOverlayNetworkEdge edge) => followEdgesReversed ? edge.SourceCrossoverIndex : edge.DestinationCrossoverIndex;
         TerrainOverlayNetworkNode PickEdgeGroupFrom(TerrainOverlayNetworkEdgeGroup edgeGroup) => followEdgesReversed ? edgeGroup.Destination : edgeGroup.Source;
         TerrainOverlayNetworkNode PickEdgeGroupTo(TerrainOverlayNetworkEdgeGroup edgeGroup) => followEdgesReversed ? edgeGroup.Source : edgeGroup.Destination;

         if (debugCanvas != null) {
            debugCanvas.Transform = Matrix4x4.Identity;
         }

         // predecessor, but in the sense that we're 
         var predecessor = new Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge, cDouble)>(); // visited

         var isDestinationVisited = new bool[destinations.Length];
         var isDestinationDirectPath = new bool[destinations.Length];
         var destinationsRemaining = destinations.Length;

         // Handle finding path breadcrumbs where source and destination nodes are equal.
         for (var destinationIndex = 0; destinationIndex < destinations.Length; destinationIndex++) {
            var (destinationNode, destinationPoint) = destinations[destinationIndex];

            // only do local path if source/dest in same node.
            if (sourceNode != destinationNode) continue;
            isDestinationVisited[destinationIndex] = true;
            destinationsRemaining--;

            predecessor[(destinationNode, ComputeDestinationIndexCpi(destinationIndex))] = (sourceNode, SOURCE_POINT_CPI, null, sourcePoint.To(destinationPoint).Norm2F());

            // try direct path
            if (sourceNode.LandPolyNode.SegmentInLandPolygonNonrecursive(sourcePoint, destinationPoint)) {
               isDestinationDirectPath[destinationIndex] = true;
            }
         }

//         Console.WriteLine("Src had " + sourceNode.CrossoverPointManager.CrossoverPoints.Count + " : " + string.Join(", ", sourceNode.CrossoverPointManager.CrossoverPoints));
         var (_, _, _, sourceOptimalLinkToCrossovers) = sourceNode.CrossoverPointManager.FindOptimalLinksToCrossovers(sourcePoint);

         var destinationOptimalLinkToCrossoversByDestinationIndex = new ExposedArrayList<PathLink>[destinations.Length];
         var destinationWorldByDestinationIndex = new Vector3[destinations.Length];
         for (var destinationIndex = 0; destinationIndex < destinations.Length; destinationIndex++) {
            if (isDestinationDirectPath[destinationIndex]) continue;

            var (destinationNode, destinationPoint) = destinations[destinationIndex];
            var (_, _, _, destinationOptimalLinkToCrossovers) = destinationNode.CrossoverPointManager.FindOptimalLinksToCrossovers(destinationPoint);
            destinationOptimalLinkToCrossoversByDestinationIndex[destinationIndex] = destinationOptimalLinkToCrossovers;
            destinationWorldByDestinationIndex[destinationIndex] = Vector3.Transform(new Vector3(destinationPoint.X, destinationPoint.Y, 0), destinationNode.SectorNodeDescription.WorldTransform);
         }

         var q = new PriorityQueue<ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge>>((a, b) => a.Item1.CompareTo(b.Item1));
         var priorityUpperBounds = new Dictionary<(TerrainOverlayNetworkNode, int), cDouble>();

         // populate q with initial expansion from start node (even if reusing prior work)
         foreach (var kvp in PickTraversedEdgeGroups(sourceNode)) {
            foreach (var g in kvp.Value) {
               foreach (var edge in g.Edges) {
                  var cpiLink = sourceOptimalLinkToCrossovers[PickFromCpi__(edge)];
                  var worldCpiLinkCost = cpiLink.TotalCost * sourceNode.SectorNodeDescription.LocalToWorldScalingFactor;
                  priorityUpperBounds[(sourceNode, PickFromCpi__(edge))] = worldCpiLinkCost;
                  q.Enqueue((worldCpiLinkCost, worldCpiLinkCost, sourceNode, SOURCE_POINT_CPI, sourceNode, PickFromCpi__(edge), null));
//                  Console.WriteLine("Init link: " + cpiLink.TotalCost + " " + PickFromCpi__(edge) + " of " + sourceNode.CrossoverPointManager.CrossoverPoints.Count);
               }
            }
         }

         // populate q with our fringe (if reusing prior work)
         if (pathfinderResultContext != null) {
            foreach (var (cur, predInfo) in pathfinderResultContext.Predecessors) {
               TerrainOverlayNetworkEdge FindMatchingEdge() {
                  foreach (var edgeGroup in predInfo.Item1.OutboundEdgeGroups[cur.Item1]) {
                     foreach (var candidateEdge in edgeGroup.Edges) {
                        if (candidateEdge.SourceCrossoverIndex == predInfo.Item2 && candidateEdge.DestinationCrossoverIndex == cur.Item2) {
                           return candidateEdge;
                        }
                     }
                  }
                  throw new NotImplementedException();
               }

               // our keys will be prior-visited (and therefore cost-weighted) nodes.
               // look at all neighbors, then if unvisited, add to our fringe.
               priorityUpperBounds[cur] = predInfo.Item4 * (cDouble)1.001f;
            }
         }

         int enqueueCount = 0;
         int dequeueCount = 0;
         bool terminalEnqueued = false;

         // TODO: Determinism
         cDouble ComputeHeuristic(TerrainOverlayNetworkNode n, int cpi) {
            return (cDouble)0;
//            var cp = n.CrossoverPointManager.CrossoverPoints[cpi];
//            var cpw = Vector3.Transform(new Vector3(cp.X, cp.Y, 0), n.SectorNodeDescription.WorldTransform);
//            return Vector3.Distance(destinationWorld, cpw) * 1.0f;
         }

         void DrawPqItem(ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> item, StrokeStyle strokeStyle) {
            var (_, ncost, nsrcnode, nsrccpi, ndstnode, ndstcpi, nedge) = item;
            DebugDrawLine(
               debugCanvas,
               nsrcnode,
               nsrccpi == SOURCE_POINT_CPI ? sourcePoint : nsrcnode.CrossoverPointManager.CrossoverPoints[nsrccpi],
               ndstnode,
               ndstcpi < 0 ? destinations[ComputeDestinationIndexFromCpi(ndstcpi)].Item2 : ndstnode.CrossoverPointManager.CrossoverPoints[ndstcpi],
               strokeStyle);
         }

         while (!q.IsEmpty && destinationsRemaining > 0) {
            var item = q.Dequeue();
            dequeueCount++;

            var (_, ncost, nsrcnode, nsrccpi, ndstnode, ndstcpi, nedge) = item;
            DrawPqItem(item, StrokeStyle.RedHairLineSolid);

            //            Console.WriteLine($"Deq {ncost} {nsrcnode} {nsrccpi} {ndstnode} {ndstcpi} {nedge}");

            if (predecessor.ContainsKey((ndstnode, ndstcpi))) {
               continue;
            }
            predecessor[(ndstnode, ndstcpi)] = (nsrcnode, nsrccpi, nedge, ncost);

            if (ndstcpi != SOURCE_POINT_CPI && ndstcpi < 0) {
               // visit destination!
               var destinationIndex = ComputeDestinationIndexFromCpi(ndstcpi);
               Trace.Assert(!isDestinationVisited[destinationIndex]);
               isDestinationVisited[destinationIndex] = true;
               destinationsRemaining--;

               if (destinationsRemaining == 0) {
                  return new PathfinderResultContext(source, destinations, predecessor, sourceOptimalLinkToCrossovers, destinationOptimalLinkToCrossoversByDestinationIndex);
               }
               continue;
            }

            // expansion to cp of other node => expand to other cps
            if (nsrcnode != ndstnode) {
               var linksToOtherCpis = ndstnode.CrossoverPointManager.OptimalLinkToOtherCrossoversByCrossoverPointIndex[ndstcpi];
               for (var cpi = 0; cpi < linksToOtherCpis.Count; cpi++) {
                  if (cpi == ndstcpi) continue;

                  var link = linksToOtherCpis[cpi];
                  var scost = ncost + link.TotalCost * ndstnode.SectorNodeDescription.LocalToWorldScalingFactor;
                  var sprior = scost + ComputeHeuristic(ndstnode, cpi);
                  Trace.Assert(link.TotalCost >= CDoubleMath.c0);

                  if (priorityUpperBounds.TryGetValue((ndstnode, cpi), out var scostub) && scostub <= scost) {
                     continue;
                  }
                  priorityUpperBounds[(ndstnode, cpi)] = scost;

                  ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> nitem = (sprior, scost, ndstnode, ndstcpi, ndstnode, cpi, null);
                  q.Enqueue(nitem);
                  enqueueCount++;
                  DrawPqItem(nitem, StrokeStyle.LimeHairLineSolid);
               }
            }

            // expansion to cp of same node => expand to neighbor edges
            // (technically should do this either way if CPI has multiple meanings...?)
            if (nsrcnode == ndstnode) {
               //               Console.WriteLine("OEG?");
               foreach (var kvp in PickTraversedEdgeGroups(ndstnode)) {
                  foreach (var g in kvp.Value) {
                     foreach (var edge in g.Edges) {
                        if (PickFromCpi__(edge) == ndstcpi) {
                           //                           Console.WriteLine("OEG: " + edge + " to " + (ndstnode != PickEdgeGroupTo(g)));
                           var scost = ncost + (cDouble)edge.Cost; // no need for scaling factor
                           var sprior = scost + ComputeHeuristic(PickEdgeGroupTo(g), PickToCpi__(edge));
                           Trace.Assert(edge.Cost >= 0);

                           if (priorityUpperBounds.TryGetValue((PickEdgeGroupTo(g), PickToCpi__(edge)), out var scostub) && scostub <= scost) {
                              continue;
                           }
                           priorityUpperBounds[(PickEdgeGroupTo(g), PickToCpi__(edge))] = scost;
                           ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> nitem = (sprior, scost, ndstnode, ndstcpi, PickEdgeGroupTo(g), PickToCpi__(edge), edge);
                           q.Enqueue(nitem);
                           enqueueCount++;
                           DrawPqItem(nitem, StrokeStyle.MagentaHairLineSolid);
                        }
                     }
                  }
               }
            }

            // expansion to terminal if current node is destination node
            for (var destinationIndex = 0; destinationIndex < destinations.Length; destinationIndex++) {
               if (isDestinationVisited[destinationIndex]) continue;

               var (destinationNode, destinationPoint) = destinations[destinationIndex];
               if (ndstnode == destinationNode) {
                  var destinationOptimalLinkToCrossovers = destinationOptimalLinkToCrossoversByDestinationIndex[destinationIndex];
                  var link = destinationOptimalLinkToCrossovers[ndstcpi];
                  Trace.Assert(link.TotalCost >= CDoubleMath.c0);

                  var scost = ncost + link.TotalCost * destinationNode.SectorNodeDescription.LocalToWorldScalingFactor;
                  var sprior = scost + CDoubleMath.c0;
                  ValueTuple<cDouble, cDouble, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge> nitem = (sprior, scost, ndstnode, ndstcpi, destinationNode, ComputeDestinationIndexCpi(destinationIndex), null);
                  q.Enqueue(nitem);
                  enqueueCount++;
                  DrawPqItem(nitem, StrokeStyle.CyanHairLineSolid);

                  if (!terminalEnqueued) {
                     terminalEnqueued = true;
                     //Console.WriteLine("Terminal Enqueued: Dequeues such far: " + dequeueCount + " and enqueues: " + enqueueCount);
                  }
               }
            }
         }

         if (destinationsRemaining > 0) {
            Console.WriteLine("Failure! Dequeues: " + dequeueCount + " and enqueues: " + enqueueCount);
         } else {
            //Console.WriteLine("Success! Dequeues: " + dequeueCount + " and enqueues: " + enqueueCount);
         }
         return new PathfinderResultContext(source, destinations, predecessor, sourceOptimalLinkToCrossovers, destinationOptimalLinkToCrossoversByDestinationIndex);
      }

      private static void AddInterTerrainOverlayNetworkNodeWaypointToWaypointRoadmapActions(MotionRoadmap roadmap, TerrainOverlayNetworkNode node, int sourceWaypoint, int destinationWaypoint) {
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

      void AddInterTerrainOverlayNetworkNodeWaypointToWaypointRoadmapActions2(Dictionary<(TerrainOverlayNetworkNode, int), (TerrainOverlayNetworkNode, int, TerrainOverlayNetworkEdge, float)> predecessor, TerrainOverlayNetworkNode node, int sourceWaypoint, int destinationWaypoint, float costToSourceWaypoint) {
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
            predecessor[(node, next)] = (node, prior, null, 0.0f);
            prior = next;
         }

         // skip last item since is link to last of source plan
         for (var i = destPath.Count - 2; i >= 0; i--) {
            var next = destPath[i].PriorIndex;
            predecessor[(node, next)] = (node, prior, null, 0.0f);
            prior = next;
         }

         if (prior != destinationWaypoint) {
            predecessor[(node, destinationWaypoint)] = (node, prior, null, 0.0f);
         }
      }

      private void DebugDrawLine(IDebugCanvas debugCanvas, TerrainOverlayNetworkNode p1Node, IntVector2 p1Local, TerrainOverlayNetworkNode p2Node, IntVector2 p2Local, StrokeStyle strokeStyle) {
         if (debugCanvas == null) return;
         debugCanvas.DrawLine(
            Vector3.Transform(new Vector3(p1Local.X, p1Local.Y, 0), p1Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector(),
            Vector3.Transform(new Vector3(p2Local.X, p2Local.Y, 0), p2Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector(),
            strokeStyle);
      }
   }
}