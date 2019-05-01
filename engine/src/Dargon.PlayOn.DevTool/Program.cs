using System;
using System.Numerics;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.DevTool {
   public static class Program {
      [STAThread]
      public static void Main(string[] args) {
         var gameFactory = new GameFactory();
         gameFactory.GameCreated += (s, game) => {
            // GameDebugger.AttachToWithHardwareRendering(game);
            GameDebugger.AttachToWithSoftwareRendering(game);

            var tiles = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Test2D, SectorMetadataPresets.CrossCircle };
            var sectors = new SectorNodeDescription[tiles.Length];
            for (var i = 0; i < tiles.Length; i++) {
               var preset = tiles[i];
               var snd = sectors[i] = game.TerrainFacade.CreateSectorNodeDescription(preset);
               snd.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1000.0f / preset.LocalBoundary.Width), Matrix4x4.CreateTranslation((i - 1) * 1000, 0, 0));
               snd.WorldToLocalScalingFactor = (cDouble)preset.LocalBoundary.Width / (cDouble)1000;
               game.TerrainFacade.AddSectorNodeDescription(snd);
            }

            ConnectTwoLink(game, sectors[0], sectors[1]);
            ConnectOneLink(game, sectors[1], sectors[2]);
         };
         Dargon.PlayOn.Program.Main(gameFactory);
      }

      private static void ConnectOneLink(Game game, SectorNodeDescription a, SectorNodeDescription b) {
         var c30000 = SectorMetadataPresets.DesiredSectorExtents;
         var c6000 = SectorMetadataPresets.DesiredSectorExtents * 1 / 5;
         var left1 = new IntLineSegment2(new IntVector2(-c30000, -c6000), new IntVector2(-c30000, c6000));
         var right1 = new IntLineSegment2(new IntVector2(c30000, -c6000), new IntVector2(c30000, c6000));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(a, b, right1, Clockness.CounterClockwise, left1, Clockness.Clockwise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(b, a, left1, Clockness.Clockwise, right1, Clockness.CounterClockwise));
      }

      private static void ConnectTwoLink(Game game, SectorNodeDescription a, SectorNodeDescription b) {
         var c30000 = SectorMetadataPresets.DesiredSectorExtents;
         var c18000 = SectorMetadataPresets.DesiredSectorExtents * 3 / 5;
         var c6000 = SectorMetadataPresets.DesiredSectorExtents * 1 / 5;
         var left1 = new IntLineSegment2(new IntVector2(-c30000, -c18000), new IntVector2(-c30000, -c6000));
         var left2 = new IntLineSegment2(new IntVector2(-c30000, c6000), new IntVector2(-c30000, c18000));
         var right1 = new IntLineSegment2(new IntVector2(c30000, -c18000), new IntVector2(c30000, -c6000));
         var right2 = new IntLineSegment2(new IntVector2(c30000, c6000), new IntVector2(c30000, c18000));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(a, b, right1, Clockness.CounterClockwise, left1, Clockness.Clockwise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(a, b, right2, Clockness.CounterClockwise, left2, Clockness.Clockwise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(b, a, left1, Clockness.Clockwise, right1, Clockness.CounterClockwise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(b, a, left2, Clockness.Clockwise, right2, Clockness.CounterClockwise));
      }
   }
}
