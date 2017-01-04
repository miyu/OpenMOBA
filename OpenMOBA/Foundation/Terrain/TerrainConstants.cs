using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenMOBA.Foundation.Terrain {
   public class TerrainConstants {
      /// <summary>
      /// Note that no matter how vector (0, 3.0) is rotated, one component is greater than 1.
      /// Technically sqrt(2) would work, but integer truncation happens in visibility graph computation
      /// so best to just round up twice to 3.
      /// This fixes a case where the character paths along a wall and, while pathing, walks into a spot
      /// that's considered a hole (the terrain representation is integer-based and cannot describe
      /// such situations well) due to floating point error.
      /// </summary>
      public const double AdditionalHoleDilationRadius = 3.0;
   }
}
