using Dargon.Commons;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.ThirdParty.ClipperLib;

namespace Dargon.Terragami {
   public class PolygonNode {
      public PolygonNode Parent;
      public IntVector2[] Contour; // null if root
      public bool IsHole;
      public PolygonNode[] Children;

      public static PolygonNode FromClipperPolyTree(PolyTree tree) {
         return FromClipperPolyTree(tree, null, true);
      }

      // Note: Clipper outputs open contours (p[0] isn't necessarily p[^1])
      public static PolygonNode FromClipperPolyTree(PolyNode node, PolygonNode parent, bool isHole) {
         if (node.Contour.Count != 0) {
            Assert.IsTrue(node.Contour.Count >= 3);
         }
         var res = new PolygonNode {
            Parent = parent,
            Contour = node.Contour.Count == 0 ? null : node.Contour.ToArray(),
            IsHole = isHole,
            Children = new PolygonNode[node.Childs.Count],
         };
         for (var i = 0; i < node.Childs.Count; i++) {
            res.Children[i] = FromClipperPolyTree(node.Childs[i], res, !isHole);
         }
         return res;
      }

      public static PolygonNode CreateRootHole(params PolygonNode[] children) => Create(null, true, children);

      public static PolygonNode Create(IntVector2[] contour, bool isHole, params PolygonNode[] children) {
         var res = new PolygonNode() { Contour = contour, IsHole = isHole, Children = children };
         foreach (var child in children) child.Parent = res;
         return res;
      }
   }
}
