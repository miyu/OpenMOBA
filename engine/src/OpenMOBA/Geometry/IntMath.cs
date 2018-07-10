using System;
using System.Runtime.CompilerServices;

namespace OpenMOBA.Geometry {
   public static class IntMath {
      private const int MaxLutIntExclusive = 1024 * 1024;
      private static readonly Int32[] IntSqrtLut = Util.Generate(MaxLutIntExclusive, x => (Int32)CDoubleMath.Sqrt((Double)x));

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Int32 Square(Int32 x) {
         return x * x;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Int32 Quad(Int32 x) {
         return Square(Square(x));
      }

      public static Int32 Sqrt(Int32 x) {
         if (x < 0) throw new ArgumentException($"sqrti({x})");
         else if (x < MaxLutIntExclusive) return IntSqrtLut[x];
         else return (Int32)Math.Sqrt(x);
      }
   }
}