using OpenMOBA.Debugging;
using OpenMOBA.Geometry;
using System;
using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA {
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
