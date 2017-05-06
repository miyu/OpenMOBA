using System;
using ClipperLib;
using OpenMOBA.Foundation.Terrain;

namespace OpenMOBA.Geometry {
   public static class PolytreeExtensions {
      public static void AssertIsContourlessRootHolePunchResult(this PolyTree polytree) {
         // Clipper's punch operation can potentially return two nonoverlapping polygons.
         // For that reason, its returned polytree's root is a hole which contains
         // non-hole polynodes. The child of this root hole node will be the borders of the
         // distinct polygons in the result and from their childs, holes and islands within
         // those borders.
         if (!polytree.IsHole || polytree.Contour.Count != 0) {
            throw new ArgumentException("Expected land punch result polytree root to be a countourless hole?");
         }
      }

      /// <summary>
      /// Note: Containment definition varies by hole vs terrain: Containment for holes
      /// does not include the hole edge, containment for terrain includes the terrain edge.
      /// This is important, else e.g. knockback + terrain push placing an entity on an edge
      /// would potentially infinite loop.
      /// </summary>
      public static void PickDeepestPolynode(this PolyTree polyTree, IntVector2 query, out PolyNode result, out bool isHole) {
         polyTree.AssertIsContourlessRootHolePunchResult();
         PolyNode current = polyTree;
         while (true) {
            // current is a hole
            PolyNode match;

            // if we fail to find the first child land node border-inclusively containing the query point
            if (!current.Childs.TryFindFirst(child => Clipper.PointInPolygon(new IntVector3(query.X, query.Y), child.Contour) != (int)ClipperPointInPolygonResult.OutsidePolygon, out match)) {
               result = current;
               isHole = true;
               return;
            }

            // next off, current is land
            current = match;

            // If we fail to find a child hole border-excludingly containing the query point
            if (!current.Childs.TryFindFirst(child => Clipper.PointInPolygon(new IntVector3(query.X, query.Y), child.Contour) == (int)ClipperPointInPolygonResult.InPolygon, out match)) {
               result = current;
               isHole = false;
               return;
            }

            // next off, current is a hole
            current = match;
         }
      }


      /// <summary>
      /// Note: Containment definition varies by hole vs terrain: Containment for holes
      /// does not include the hole edge, containment for terrain includes the terrain edge.
      /// This is important, else e.g. knockback + terrain push placing an entity on an edge
      /// would potentially infinite loop.
      /// </summary>
      public static void PickDeepestPolynodeGivenHoleShapePolytree(this PolyTree polyTree, IntVector2 query, out PolyNode result, out bool isHole) {
         polyTree.AssertIsContourlessRootHolePunchResult();
         PolyNode current = polyTree;
         while (true) {
            // current is a hole of a hole's shape
            PolyNode match;

            // if we fail to find the first child land node border-inclusively containing the query point
            if (!current.Childs.TryFindFirst(child => Clipper.PointInPolygon(new IntVector3(query.X, query.Y), child.Contour) == (int)ClipperPointInPolygonResult.InPolygon, out match)) {
               result = current;
               isHole = true;
               return;
            }

            // next off, current is land of a hole's shape
            current = match;

            // If we fail to find a child hole border-excludingly containing the query point
            if (!current.Childs.TryFindFirst(child => Clipper.PointInPolygon(new IntVector3(query.X, query.Y), child.Contour) != (int)ClipperPointInPolygonResult.OutsidePolygon, out match)) {
               result = current;
               isHole = false;
               return;
            }

            // next off, current is a hole of a hole's shape
            current = match;
         }
      }
   }
}