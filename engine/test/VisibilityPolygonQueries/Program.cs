using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Debugging;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace VisibilityPolygonQueries {
   public class Program {
      private static readonly Size bounds = new Size(1000, 1000);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(
         bounds,
         new Point(50, 50),
         new OrthographicXYProjector(
            1.8, 
            new IntVector2(500, 500),
            new IntVector2(bounds.Width / 2, bounds.Height / 2),
            true));
      private static int nextFrameIndex = 0;

      public static void Main(string[] args) {
         RenderTestQueries(SectorMetadataPresets.Blank2D);
         RenderTestQueries(SectorMetadataPresets.Test2D);
         RenderTestQueries(SectorMetadataPresets.CrossCircle);
         RenderTestQueries(SectorMetadataPresets.FourSquares2D);
         RenderTestQueries(SectorMetadataPresets.HashCircle1);
         RenderTestQueries(SectorMetadataPresets.DotaStyleMoba);
         host.DumpScreenshotsToDocumentsPictures();
      }

      private static void RenderTestQueries(TerrainStaticMetadata sectorMetadataPresets) {
         var terrainStaticMetadata = new TerrainStaticMetadata {
            LocalBoundary = sectorMetadataPresets.LocalBoundary,
            LocalIncludedContours = sectorMetadataPresets.LocalIncludedContours,
            LocalExcludedContours = sectorMetadataPresets.LocalExcludedContours
         };
         var localGeometryJob = new LocalGeometryJob(terrainStaticMetadata, new HashSet<(IntLineSegment2 segment, Clockness inClockness)>());
         var localGeometryViewManager = new LocalGeometryViewManager(localGeometryJob);
         var actorRadius = 1;
         var localGeometryView = localGeometryViewManager.GetErodedView(actorRadius);

         var canvas = host.CreateAndAddCanvas(nextFrameIndex++);
         canvas.Transform = Matrix4x4.CreateScale(1000 / 60000.0f) * Matrix4x4.CreateTranslation(500, 500, 0);
         canvas.DrawPolyNode(localGeometryView.PunchedLand, StrokeStyle.BlackHairLineSolid, StrokeStyle.RedHairLineSolid);
         canvas.DrawTriangulation(localGeometryView.Triangulation, StrokeStyle.CyanHairLineDashed5);
         foreach (var (i, island) in localGeometryView.Triangulation.Islands.Enumerate()) {
            var poly = ConvertTriangulationToWeaklySimplePolygon(island, canvas);
            var eroded = PolygonOperations.Offset()
                                          .Include(new Polygon2(poly.Select(x => x.LossyToIntVector2()).ToList()))
                                          .Erode(500)
                                          .Execute();
            canvas.DrawPolyNode(eroded, StrokeStyle.CyanThick5Solid);
         }
      }

      /// <summary>
      /// Builds MST of triangulation island graph { Nodes = Triangles, Edges = Neighbor Relationships }
      /// Then walks MST CCW emitting a closed weakly simple polygon boundary.
      /// </summary>
      private static List<DoubleVector2> ConvertTriangulationToWeaklySimplePolygon(TriangulationIsland island, IDebugCanvas canvas = null) {
         const int TREE_ROOT_TRIANGLE_INDEX = 0;
         const int NONE = Triangle3.NO_NEIGHBOR_INDEX;

         var tris = island.Triangles;

         // Build any spanning tree (in this case via DFS), tracking parents (search predecessors)
         var preds = new int[tris.Length];
         for (var i = 0; i < preds.Length; i++) {
            preds[i] = NONE;
         }

         void VisitDFS(int curti, int predti) {
            ref var triangle = ref tris[curti];

            preds[curti] = predti;

            for (var i = 0; i < 3; i++) {
               var succti = triangle.NeighborOppositePointIndices[i];
               if (succti == NONE || succti == TREE_ROOT_TRIANGLE_INDEX) continue;
               if (preds[succti] != NONE) continue;
               VisitDFS(succti, curti);
            }
         }

         VisitDFS(TREE_ROOT_TRIANGLE_INDEX, NONE);

         canvas?.BatchDraw(() => {
            for (var ti = 0; ti < tris.Length; ti++) {
               if (preds[ti] != NONE) {
                  canvas.DrawLine(tris[ti].Centroid, tris[preds[ti]].Centroid, StrokeStyle.RedThick5Solid);
               }
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
         Trace.Assert(boundary.Count == boundary.Capacity);
         Trace.Assert(boundary[0] == boundary[boundary.Count - 1]);

         return boundary;
      }
   }
}