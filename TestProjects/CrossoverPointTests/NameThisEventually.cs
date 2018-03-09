using System;
using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain;
using System.Numerics;
using OpenMOBA;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain.Snapshots;
using OpenMOBA.Geometry;
using Xunit;

namespace CrossoverPointTests {
   public class NameThisEventually {
      private LocalGeometryView BuildLgv(double holeDilationRadius, params TerrainStaticMetadata[] holeMetadatas) {
         var store = new SectorGraphDescriptionStore();
         var terrainService = new TerrainService(store, new TerrainSnapshotCompiler(store));

         var sector = terrainService.CreateSectorNodeDescription(SectorMetadataPresets.Blank2D);
         sector.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1), Matrix4x4.CreateTranslation(1 * 1000 - 1500, 0 * 1000 - 500, 0));
         terrainService.AddSectorNodeDescription(sector);

         var left2 = new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800));
         terrainService.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(sector, sector, left2, left2));

         foreach (var holeMetadata in holeMetadatas) {
            var terrainHole = terrainService.CreateHoleDescription(holeMetadata);
            terrainService.AddTemporaryHoleDescription(terrainHole);
         }

         var terrainOverlayNetwork = terrainService.CompileSnapshot().OverlayNetworkManager.CompileTerrainOverlayNetwork(holeDilationRadius);
         return terrainOverlayNetwork.TerrainNodes.First().LocalGeometryView;
      }

      private TerrainStaticMetadata BuildRectangleHole(int x, int y, int width, int height, long rotationBits = -1) {
         var rotation = rotationBits == -1 ? 0.0 : BitConverter.Int64BitsToDouble(rotationBits);
         var contour = Polygon2.CreateRect(-width / 2, -height / 2, width, height).Points;
         var transform = Matrix3x2.CreateRotation((float)rotation);
         contour = contour.Map(p => Vector2.Transform(p.ToDoubleVector2().ToDotNetVector(), transform).ToOpenMobaVector().LossyToIntVector2())
                          .Map(p => p + new IntVector2(x, y))
                          .ToList();

         var bounds = IntRect2.BoundingPoints(contour.ToArray()).ToDotNetRectangle();

         return new TerrainStaticMetadata {
            LocalBoundary = bounds,
            LocalIncludedContours = new[] { new Polygon2(contour, false) }
         };
      }

      [Fact]
      public void A() {
         var localGeometryView = BuildLgv(15, BuildRectangleHole(-486, 213, 119, 164));
         PortalSectorEdgeDescription.X(new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)), localGeometryView);
      }

      [Fact]
      public void B() {
         var localGeometryView = BuildLgv(14, BuildRectangleHole(-486, 213, 119, 164));
         PortalSectorEdgeDescription.X(new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)), localGeometryView);
      }

      [Fact]
      public void C() {
         var localGeometryView = BuildLgv(14, BuildRectangleHole(-664, 133, 174, 188));
         PortalSectorEdgeDescription.X(new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)), localGeometryView);
      }

      [Fact]
      public void D() {
         var localGeometryView = BuildLgv(
            14,
            BuildRectangleHole(-517, 107, 5, 5, 4607552631852924299),
            BuildRectangleHole(-506, 100, 6, 7, 4613867344244209246),
            BuildRectangleHole(-505, 136, 6, 7, 4615794974257582119)
         );
         PortalSectorEdgeDescription.X(new IntLineSegment2(new IntVector2(0, 600), new IntVector2(0, 800)), localGeometryView);
      }
   }
}
