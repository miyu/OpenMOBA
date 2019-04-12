using System;
using System.Numerics;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
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

            var preset = SectorMetadataPresets.TestMiyu;
            var snd = game.TerrainFacade.CreateSectorNodeDescription(preset);
            snd.WorldTransform = Matrix4x4.Multiply(Matrix4x4.CreateScale(1000.0f / preset.LocalBoundary.Width), Matrix4x4.CreateTranslation(0, 0, 0));
            snd.WorldToLocalScalingFactor = (cDouble)preset.LocalBoundary.Width / (cDouble)1000;
            game.TerrainFacade.AddSectorNodeDescription(snd);

         };
         Dargon.PlayOn.Program.Main(gameFactory);
      }
   }

   public class NetworkingSubsystem {
      public void Hook() {

      }
   }
}
