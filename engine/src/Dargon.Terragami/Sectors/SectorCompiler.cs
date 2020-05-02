using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dargon.Commons.Collections;
using Dargon.Dviz;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;

namespace Dargon.Terragami.Sectors {
   public class SectorCompiler {
      public SectorCompilationOutput Compile(SectorCompilationInput input, IDebugCanvas debugCanvasOpt) {
         var portals = input.Portals;
         var portalPoints = ComputePortalPoints(portals);

         var punchedLand = ComputePunchedLand(input, debugCanvasOpt);
         var barriers = ComputeVisibilityBarriers(punchedLand);

         var portalPointLinkStates = ComputePortalPointLinkStates(debugCanvasOpt, portals, portalPoints, barriers);

         return new SectorCompilationOutput { 
            Input = input,
            PortalPoints = portalPoints,
            PunchedLand = punchedLand,
            VisibilityBarriers = barriers,
            PortalPointLinkStates = portalPointLinkStates,
         };
      }

      private static unsafe List<LinkState[]> ComputePortalPointLinkStates(IDebugCanvas debugCanvasOpt, ExposedArrayList<SectorPortal> portals, IntVector2[][] portalPoints, IntLineSegment2[] barriers) {
         var allLinkStates = new List<LinkState[]>();
         var pass = 0;

         NativeUtils.LoadPrequeryAnySegmentIntersections(barriers, out var segsIntersectPrequeryState);
         
         for (var a = 0; a < portals.Count; a++) {
            var portalA = portals[a];
            var pointsA = portalPoints[a];

            for (var b = a + 1; b < portals.Count; b++) {
               var portalB = portals[b];
               var pointsB = portalPoints[b];

               var numLinks = pointsA.Length * pointsB.Length;
               var linkStates = new LinkState[numLinks];
               var linkStateIndex = 0;

               var queries = stackalloc seg2i16[numLinks];
               var pNextQuerySegment = queries;

               for (var i = 0; i < pointsA.Length; i++) {
                  var pa = pointsA[i];
                  for (var j = 0; j < pointsB.Length; j++) {
                     var pb = pointsB[j];
                     pNextQuerySegment->x1 = (short)pa.X;
                     pNextQuerySegment->y1 = (short)pa.Y;
                     pNextQuerySegment->x2 = (short)pb.X;
                     pNextQuerySegment->y2 = (short)pb.Y;
                     pNextQuerySegment++;
                  }
               }

               var results = stackalloc byte[numLinks];
               NativeUtils.QueryAnySegmentIntersections(segsIntersectPrequeryState, queries, numLinks, results);

               for (var i = 0; i < pointsA.Length; i++) {
                  for (var j = 0; j < pointsB.Length; j++) {
                     var occluded = *results != 0;
                     linkStates[linkStateIndex] = new LinkState {
                        Occluded = occluded,
                     };
                     linkStateIndex++;
                     results++;
                     if (!occluded) pass++;
                  }
               }

               allLinkStates.Add(linkStates);
            }
         }

         NativeUtils.FreePrequeryAnySegmentIntersections(segsIntersectPrequeryState);

         if (debugCanvasOpt != null) {
            Console.WriteLine(pass);
         }

         return allLinkStates;
      }

      private static IntLineSegment2[] ComputeVisibilityBarriers(PolygonNode punchedLand) {
         return new BarrierCalculator().CalculateContourAndChildHoleBarriers(punchedLand);
      }

      private static IntVector2[][] ComputePortalPoints(ExposedArrayList<SectorPortal> portals) {
         var portalPoints = new IntVector2[portals.Count][];
         for (var i = 0; i < portals.Count; i++) {
            var pi = portals[i];
            var points = portalPoints[i] = new IntVector2[pi.CrossoverPointsGenerated];
            for (var j = 0; j < points.Length; j++) {
               points[j] = pi.Segment.PointAtRatioLossy(j, points.Length - 1);
            }
         }

         return portalPoints;
      }

      private static PolygonNode ComputePunchedLand(SectorCompilationInput input, IDebugCanvas debugCanvasOpt) {
         var punch = PolygonOperations.Punch();
         punch.Include(input.Land.Blueprint.Root);
         foreach (var hole in input.Holes) {
            var holeProjection = hole.HolePrimitive.Project(hole.Transform, input.Land.Transform);
            var holeTransform = holeProjection.Transform.Flatten();
            var mat = holeTransform.Matrix;

            var s = new Stack<PolygonNode>();
            var transformedContour = new List<IntVector2>();
            s.Push(holeProjection.Root);
            while (s.Count > 0) {
               var n = s.Pop();
               foreach (var succ in n.Children) {
                  s.Push(succ);
               }

               if (n.Contour == null) continue;

               transformedContour.Clear();
               foreach (var p in n.Contour) {
                  var q = Vector2.Transform(p.ToDotNetVector(), mat).ToOpenMobaVector().LossyToIntVector2();
                  transformedContour.Add(q);
               }

               punch.Exclude(transformedContour);
            }
         }

         // 46ms
         var punchedLand = punch.Execute(0, cleanupDegeneraciesWithOffset: false);
         if (debugCanvasOpt != null) {
            debugCanvasOpt.Transform = Matrix4x4.Identity;
            debugCanvasOpt.DrawPolygonNode(punchedLand);
            debugCanvasOpt.DrawPoints(input.PinPoints.ToArray(), StrokeStyle.RedThick25Solid);
            debugCanvasOpt.DrawPoints(input.TraversableCorners.ToArray(), StrokeStyle.LimeThick25Solid);
         }

         return punchedLand;
      }
   }

   public class SectorCompilationOutput {
      public SectorCompilationInput Input;
      public PolygonNode PunchedLand;
      public IntVector2[][] PortalPoints;
      public IntLineSegment2[] VisibilityBarriers;
      public List<LinkState[]> PortalPointLinkStates;
   }
}
