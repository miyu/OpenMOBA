using System;

namespace Dargon.PlayOn.Geometry {
   public static class Frac01Lut {
      private const int LutSizeMinus1 = 256;

      private static readonly Double[] powLut0_5 = BuildPowLut(CDoubleMath.c0_5);
      public static Double Pow0_5(int numerator, int denominator) => Lookup(powLut0_5, numerator, denominator);

      private static readonly Double[] powLut0_3 = BuildPowLut(CDoubleMath.c0_3);
      public static Double Pow0_3(int numerator, int denominator) => Lookup(powLut0_3, numerator, denominator);

      public static Double[] BuildLut(Func<int, int, Double> f) => Util.Generate(LutSizeMinus1 + 1, i => f(i, LutSizeMinus1));
      public static Double[] BuildPowLut(Double pow) => BuildLut((i, j) => CDoubleMath.Pow((Double)i / (Double)j, pow));
      public static Double Lookup(Double[] lut, int numerator, int denominator) => lut[(int)((numerator * (long)LutSizeMinus1) / denominator)];
   }
}