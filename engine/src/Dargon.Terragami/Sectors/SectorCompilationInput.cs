using System.Collections.Generic;
using Dargon.Commons.Collections;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   public class SectorCompilationInput {
      public GeometryInput Land;
      public HashSet<IntVector2> TraversableCorners = new HashSet<IntVector2>();
      public HashSet<IntVector2> PinPoints = new HashSet<IntVector2>();
      public ExposedArrayList<SectorPortal> Portals = new ExposedArrayList<SectorPortal>();
      public ExposedArrayList<HoleInput> Holes = new ExposedArrayList<HoleInput>();
   }
}
