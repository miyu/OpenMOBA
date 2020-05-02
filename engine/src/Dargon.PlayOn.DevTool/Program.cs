using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;
using Dargon.PlayOn.Vox;
using Dargon.Vox;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.DevTool {
   public static class Program {
      [STAThread]
      public static void Main(string[] args) {
         Globals.Serializer.ImportTypes(new PlayOnVoxTypes());

         var gameFactory = new GameFactory();
         gameFactory.GameCreated += (s, game) => {
            // GameDebugger.AttachToWithHardwareRendering(game);
            GameDebugger.AttachToWithSoftwareRendering(game);

            Load(game.TerrainFacade);
            // var tiles = new[] { SectorMetadataPresets.HashCircle2, SectorMetadataPresets.Test2D, SectorMetadataPresets.CrossCircle };
            // var sectors = new SectorNodeDescription[tiles.Length];
            // for (var i = 0; i < tiles.Length; i++) {
            //    var preset = tiles[i];
            //    var snd = sectors[i] = game.TerrainFacade.CreateSectorNodeDescription(preset);
            //    snd.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1000.0f / preset.LocalBoundary.Width), Matrix4x4.CreateTranslation((i - 1) * 1000, 0, 0));
            //    snd.WorldToLocalScalingFactor = (cDouble)preset.LocalBoundary.Width / (cDouble)1000;
            //    game.TerrainFacade.AddSectorNodeDescription(snd);
            // }
            //
            // var bytes = Serialize.ToBytes(sectors.Map(sector => sector.StaticMetadata));
            // var tsms = Deserialize.From<TerrainStaticMetadata[]>(new MemoryStream(bytes, 0, bytes.Length, false, true));

            // ConnectTwoLink(game, sectors[0], sectors[1]);
            // ConnectOneLink(game, sectors[1], sectors[2]);
         };
         Dargon.PlayOn.Program.Main(gameFactory);
      }

      private static void Load(TerrainFacade terrainFacade) {
         var str =
            @"GameObject
{X=-999,Y=-583,Width=1998,Height=1166}
I: (1000, 579), (-925, 583), (-1000, -583), (950, -558), (1000, 579)
I: (-267, -178), (-634, 472), (319, 392), (11, -207), (-267, -178)
I: (-275, -6), (-5, -30), (144, 298), (-400, 346), (-275, -6)
I: (337, -388), (309, -12), (625, -6), (730, -342), (337, -388)
I: (-770, -455), (-781, -137), (-473, -144), (-517, -410), (-770, -455)
{ {M11:0.005891537 M12:0 M13:0 M14:0} {M21:0 M22:0.005891537 M23:0 M24:0} {M31:0 M32:0 M33:0.005891537 M34:0} {M41:0.8252628 M42:-1.789684 M43:0.06 M44:1} }
169.735000610352";
         var lines = str.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
         var tsm = new TerrainStaticMetadata();
         tsm.Name = lines[0].Trim();
         tsm.LocalBoundary = new Rectangle(-999, -583, 1998, 1166);
         var lic = new List<Polygon2>();
         for (var i = 2; i < lines.Length; i++) {
            var line = lines[i];
            if (!line.StartsWith("I:")) continue;
            var parts = line.Substring(4, line.Length - 4 - 1).Split(new[] { "), (" }, StringSplitOptions.RemoveEmptyEntries)
                            .Map(x => {
                               var components = x.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                               return new IntVector2(int.Parse(components[0]), int.Parse(components[1]));
                            })
                            .ToList();
            lic.Add(new Polygon2(parts));
         }
         tsm.LocalIncludedContours = lic;
         var snd = terrainFacade.CreateSectorNodeDescription(tsm);
         snd.WorldTransform = new Matrix4x4(0.005891537f, 0, 0, 0, 0, 0.005891537f, 0, 0, 0, 0, 0.005891537f, 0, 0.8252628f, -1.789684f, 0.06f, 1);
         snd.WorldToLocalScalingFactor = 169.735000610352f;
         terrainFacade.AddSectorNodeDescription(snd);
      }

      private static void ConnectOneLink(Game game, SectorNodeDescription a, SectorNodeDescription b) {
         var c30000 = SectorMetadataPresets.DesiredSectorExtents;
         var c6000 = SectorMetadataPresets.DesiredSectorExtents * 1 / 5;
         var left1 = new IntLineSegment2(new IntVector2(-c30000, -c6000), new IntVector2(-c30000, c6000));
         var right1 = new IntLineSegment2(new IntVector2(c30000, -c6000), new IntVector2(c30000, c6000));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(a, b, right1, Clockness.ClockWise, left1, Clockness.CounterClockWise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(b, a, left1, Clockness.CounterClockWise, right1, Clockness.ClockWise));
      }

      private static void ConnectTwoLink(Game game, SectorNodeDescription a, SectorNodeDescription b) {
         var c30000 = SectorMetadataPresets.DesiredSectorExtents;
         var c18000 = SectorMetadataPresets.DesiredSectorExtents * 3 / 5;
         var c6000 = SectorMetadataPresets.DesiredSectorExtents * 1 / 5;
         var left1 = new IntLineSegment2(new IntVector2(-c30000, -c18000), new IntVector2(-c30000, -c6000));
         var left2 = new IntLineSegment2(new IntVector2(-c30000, c6000), new IntVector2(-c30000, c18000));
         var right1 = new IntLineSegment2(new IntVector2(c30000, -c18000), new IntVector2(c30000, -c6000));
         var right2 = new IntLineSegment2(new IntVector2(c30000, c6000), new IntVector2(c30000, c18000));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(a, b, right1, Clockness.ClockWise, left1, Clockness.CounterClockWise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(a, b, right2, Clockness.ClockWise, left2, Clockness.CounterClockWise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(b, a, left1, Clockness.CounterClockWise, right1, Clockness.ClockWise));
         game.TerrainFacade.AddSectorEdgeDescription(PortalSectorEdgeDescription.Build(b, a, left2, Clockness.CounterClockWise, right2, Clockness.ClockWise));
      }
   }
}
