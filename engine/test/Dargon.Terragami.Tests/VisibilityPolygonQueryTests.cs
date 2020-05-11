using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Dviz;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using Dargon.Terragami.Sectors;

namespace Dargon.Terragami.Tests {
   public class VisibilityPolygonQueryTests {
      public void Exec() {
         var blueprint = SectorBlueprints.Test2D;
         var input = new SectorCompilationInput {
            Land = new GeometryInput {
               Blueprint = blueprint,
               Transform = new CoreTransform(Matrix4x4.Identity, Matrix4x4.Identity, 2.5f / blueprint.LocalBoundary.Width),
            },
         };
         var compilation = new SectorCompiler().Compile(input, null);
         var canvasHost = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(AxisAlignedBoundingBox2.BoundingPolygonNode(compilation.PunchedLand));
         var canvas = canvasHost.CreateAndAddCanvas(0);
         canvas.FillTriangulation(compilation.Triangulation, new FillStyle(Color.White));
         // canvas.DrawPolygonNode(compilation.PunchedLand);

         foreach (var island in compilation.Triangulation.Islands) {
            var poly = ConvertTriangulationToWeaklySimplePolygon(island, canvas);
            var eroded = PolygonOperations.Offset()
                                          .Include(new Polygon2(poly.Select(x => x.LossyToIntVector2()).Reverse().ToList()))
                                          .Erode(500)
                                          .Execute();
            canvas.DrawPolygonNode(eroded, StrokeStyle.CyanThick5Solid);
         }
      }

      private static List<DoubleVector2> ConvertTriangulationToWeaklySimplePolygon(TriangulationIsland island, IDebugCanvas canvas = null) {
         const int TREE_ROOT_TRIANGLE_INDEX = 0;
         const int TREE_ROOT_PARENT_INDEX = -1337;
         const int NONE = Triangle3.NO_NEIGHBOR_INDEX;

         var tris = island.Triangles;

         // Build any spanning tree (in this case via DFS), tracking parents (search predecessors)
         var preds = new int[tris.Length];
         for (var i = 0; i < preds.Length; i++) {
            preds[i] = NONE;
         }

         var cuts = new HashSet<(int t1, int t2)>();

         void VisitDFS(int curti, int predti) {
            ref var triangle = ref tris[curti];

            preds[curti] = predti;

            for (var i = 0; i < 3; i++) {
               var succti = triangle.NeighborOppositePointIndices[i];
               if (succti == NONE) continue; // No triangle shares edge.
               if (succti == predti) continue; // It's our parent
               if (preds[succti] != NONE) {
                  // Already visited -- this is a cut!
                  var lo = succti;
                  var hi = curti;

                  if (lo > hi) (lo, hi) = (hi, lo);
                  cuts.Add((lo, hi));
                  continue;
               }
               VisitDFS(succti, curti);
            }
         }

         VisitDFS(TREE_ROOT_TRIANGLE_INDEX, TREE_ROOT_PARENT_INDEX);

         canvas?.BatchDraw(() => {
            for (var ti = 0; ti < tris.Length; ti++) {
               if (preds[ti] >= 0) {
                  canvas.DrawLine(tris[ti].Centroid, tris[preds[ti]].Centroid, StrokeStyle.RedThick5Solid);
               }
            }

            foreach (var (t1, t2) in cuts) {
               canvas.DrawLine(tris[t1].Centroid, tris[t2].Centroid, StrokeStyle.LimeThick5Solid);
            }
         });

         // Walk spanning tree hugging CCW to emit weak simple polygon boundary.
         // Additional 2 boundary points for root triangle.
         var boundary = new List<DoubleVector2>(tris.Length * 2 + 2);

         // For a given triangle and predecessor-shared edge, emit CCW boundary
         // Assume boudnary point from predecessor-shared edge is already emitted.
         void WalkMSTAndEmitSimplePolygonBoundary(int curti, int predti) {
            ref var triangle = ref tris[curti];

            var isCurrentTriangleRoot = predti == NONE;
            var predei = isCurrentTriangleRoot ? NONE
               : triangle.NeighborOppositePointIndices.A == predti ? 0
                  : triangle.NeighborOppositePointIndices.B == predti ? 1 : 2;

            // For the root triangle, repeat the emitted point of the last iteration
            // in the below for loop to form a closed polygon.
            if (isCurrentTriangleRoot) {
               boundary.Add(triangle.Points.A);
            }

            // Loop invariant: the CCmost point of the next-to-emit edge is already emitted.
            // triangles are CCW, polygon2s are CCW.
            var startEdgeIndexOffset = isCurrentTriangleRoot ? 0 : 1;
            for (var i = startEdgeIndexOffset; i < 3; i++) {
               var succei = (predei + i + 3) % 3;
               var succti = triangle.NeighborOppositePointIndices[succei];
               if (succti != NONE && preds[succti] == curti) {
                  WalkMSTAndEmitSimplePolygonBoundary(succti, curti);
               }
               var pointIndexCounterClockWisemostOfEdge = (predei + i + 2) % 3;
               boundary.Add(triangle.Points[pointIndexCounterClockWisemostOfEdge]);
            }
         }

         WalkMSTAndEmitSimplePolygonBoundary(TREE_ROOT_TRIANGLE_INDEX, NONE);
         Assert.Equals(boundary.Count, boundary.Capacity);
         Assert.Equals(boundary[0], boundary[boundary.Count - 1]);
         return boundary;
      }
   }
}
