using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;
using OpenMOBA.Geometry;

namespace OpenMOBA.Foundation.Terrain.CompilationResults.Local {
   public static class PunchedLandPolyNodeExtensions {
      public static bool SegmentInLandPolygonNonrecursive(this PolyNode landNode, IntVector2 first, IntVector2 second) {
         return SegmentInLandPolygonNonrecursive(landNode, new IntLineSegment2(first, second));
      }

      /// <summary>
      /// A line segment S is contained within a polynode P if it is contained in 
      /// P's contour (no points are outside contour) and none of P's child contours.
      /// 
      /// Note: Given a triangle-shaped land node. A line segment containing one of its edges
      /// (or a segment contained within that edge) would normally be counted as a part of the
      /// interior. For simplicity reasons, this is the case here; however, a sub-segment of
      /// that edge will not be counted as a part of the interior. We don't handle the sub-segment
      /// case because that'd be done imprecisely anyway (we're dealing with int line segments)
      /// </summary>
      public static bool SegmentInLandPolygonNonrecursive(this PolyNode landNode, IntLineSegment2 query) {
         var segmentContainment = landNode.SegmentInPolygon(query);

         switch (segmentContainment) {
            case PolygonContainmentResult.OutsidePolygon:
            case PolygonContainmentResult.IntersectsPolygon:
               return false;
            case PolygonContainmentResult.OnPolygon:
               return true;
            case PolygonContainmentResult.InPolygon:
               foreach (var child in landNode.Childs) {
                  var childContainment = child.SegmentInPolygon(query);
                  if (childContainment == PolygonContainmentResult.OutsidePolygon || childContainment == PolygonContainmentResult.OnPolygon) {
                     continue;
                  }
                  return false;
               }
               return true;
            default:
               throw new Exception("Invalid State");
         }
      }

      public static bool PointInLandPolygonNonrecursive(this PolyNode landNode, IntVector2 query) {
         var outerPipResult = Clipper.PointInPolygon(query, landNode.Contour);
         if (outerPipResult == PolygonContainmentResult.OutsidePolygon) return false;
         if (outerPipResult == PolygonContainmentResult.OnPolygon) return true;
         Trace.Assert(outerPipResult == PolygonContainmentResult.InPolygon);

         foreach (var child in landNode.Childs) {
            var childContainment = Clipper.PointInPolygon(query, child.Contour);
            if (childContainment == PolygonContainmentResult.OutsidePolygon || childContainment == PolygonContainmentResult.OnPolygon) {
               continue;
            }
            return false;
         }
         return true;
      }

      public static IEnumerable<PolyNode> EnumerateLandNodes(this PolyNode node) {
         var s = new Stack<PolyNode>();
         if (!node.IsHole) {
            s.Push(node);
         } else {
            foreach (var child in node.Childs) {
               s.Push(child);
            }
         }
         while (s.Count != 0) {
            var landNode = s.Pop();
            yield return landNode;
            foreach (var childHoleNode in landNode.Childs) {
               foreach (var childChildLandNode in childHoleNode.Childs) {
                  s.Push(childChildLandNode);
               }
            }
         }
      }

      public static List<PolyNode> GetLandNodes(this PolyNode node) {
         var q = new List<PolyNode>();
         if (!node.IsHole) {
            q.Add(node);
         } else {
            q.AddRange(node.Childs);
         }
         for (var i = 0; i < q.Count; i++) {
            var childHoles = q[i].Childs;
            for (var j = 0; j < childHoles.Count; j++) {
               q.AddRange(childHoles[j].Childs);
            }
         }
         return q;
      }
   }
}
