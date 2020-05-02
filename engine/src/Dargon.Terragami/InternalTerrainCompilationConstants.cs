#if use_fixed
using ClipperLib;
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.Terrain {
   public static class InternalTerrainCompilationConstants {
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

      /// <summary>
      /// Clipper in i32 mode takes numbers from -2^15+1 to 2^15-1.
      /// Most importantly, Clipper computes the distance between two points, so
      /// 2 * (MAX_VAL - MIN_VAL)^2 must not overflow. As we're using Q31.32 fixed-point arithmetic,
      /// we are effectively limited to MAX_VAL = 2^14. (Assume MAX_VAL = -MIN_VAL)
      /// That's ballpark [-16384, 16384]. We'll simplify this range to [-15000, 15000] (in i32 mode)
      /// for desired sector extents
      /// </summary>
      public const int DesiredSectorExtents = 15000;

      /// <summary>
      /// And from there, [-26000, 26000] (in i32 mode) for sector clipping bounds.
      ///
      /// Note: Buffer must be less than 2^15 - 1 - 10 as clipper internally buffers by 10px
      /// for certain operations, and probably still should be under 2^14 derived above.
      /// </summary>
      public const int SectorClipBounds = DesiredSectorExtents + 1000;
   }
}
