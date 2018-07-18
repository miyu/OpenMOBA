using Dargon.PlayOn.Foundation;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else

#endif

namespace Dargon.PlayOn {
   public class Program {
      public static void Main(string[] args) {
         while (true) {
            Main(new GameFactory());
         }
      }

      public static void Main(GameFactory gameFactory) {
         var gameInstance = gameFactory.Create();
         gameInstance.Run();
      }
   }
}