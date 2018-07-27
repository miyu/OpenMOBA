using Dargon.PlayOn.Foundation;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.DevTool {
   public static class Program {
      public static void Main(string[] args) {
         var gameFactory = new GameFactory();
         gameFactory.GameCreated += (s, game) => {
            GameDebugger.AttachToWithHardwareRendering(game);
            // GameDebugger.AttachToWithSoftwareRendering(game);
         };
         Dargon.PlayOn.Program.Main(gameFactory);
      }
   }

   public class NetworkingSubsystem {
      public void Hook() {

      }
   }
}
