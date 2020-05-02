using System.Collections.Generic;
using Dargon.Commons.Collections;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   public class SectorCompilationInput {
      public GeometryInput Land;
      public HashSet<IntVector2> TraversableCorners;
      public HashSet<IntVector2> PinPoints;
      public ExposedArrayList<SectorPortal> Portals;
      public ExposedArrayList<HoleInput> Holes;
   }
}
