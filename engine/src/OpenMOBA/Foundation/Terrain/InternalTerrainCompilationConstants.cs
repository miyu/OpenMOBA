#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Foundation.Terrain {
   internal static class InternalTerrainCompilationConstants {
      /// <summary>
      ///    Note that no matter how vector (0, 3.0) is rotated, one component is greater than 1.
      ///    Technically sqrt(2) would work, but integer truncation happens in visibility graph computation
      ///    so best to just round up twice to 3.
      ///    This fixes a case where the character paths along a wall and, while pathing, walks into a spot
      ///    that's considered a hole (the terrain representation is integer-based and cannot describe
      ///    such situations well) due to floating point error.
      /// </summary>
      public static readonly cDouble AdditionalHoleDilationRadius = (cDouble)3;

      /// <summary>
      ///    Minimum distance from a character point to an (already dilated) land triangle edge.
      ///    This ensures future land triangle lookups of the character position succeed rather
      ///    than failing due to ambiguity in position of on-edge points.
      /// </summary>
      public static readonly cDouble TriangleEdgeBufferRadius = (cDouble)5 / (cDouble)1000;
   }
}
