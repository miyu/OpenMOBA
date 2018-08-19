using System;

namespace Dargon.PlayOn.Geometry {
   public static class Frac01Lut {
      private const int LutSizeMinus1 = 256;

      private static readonly Double[] powLut4_0 = BuildPowLut(CDoubleMath.c4);
      public static Double Pow4_0(int numerator, int denominator) => Lookup(powLut4_0, numerator, denominator);

      private static readonly Double[] powLut2_0 = BuildPowLut(CDoubleMath.c2);
      public static Double Pow2_0(int numerator, int denominator) => Lookup(powLut2_0, numerator, denominator);

      private static readonly Double[] powLut1_8 = BuildPowLut(CDoubleMath.c1 + CDoubleMath.c0_8);
      public static Double Pow1_8(int numerator, int denominator) => Lookup(powLut1_8, numerator, denominator);

      private static readonly Double[] powLut1_5 = BuildPowLut(CDoubleMath.c1 + CDoubleMath.c0_5);
      public static Double Pow1_5(int numerator, int denominator) => Lookup(powLut1_5, numerator, denominator);

      private static readonly Double[] powLut1_2 = BuildPowLut(CDoubleMath.c1 + CDoubleMath.c0_2);
      public static Double Pow1_2(int numerator, int denominator) => Lookup(powLut1_2, numerator, denominator);

      private static readonly Double[] powLut0_8 = BuildPowLut(CDoubleMath.c0_8);
      public static Double Pow0_8(int numerator, int denominator) => Lookup(powLut0_8, numerator, denominator);

      private static readonly Double[] powLut0_5 = BuildPowLut(CDoubleMath.c0_5);
      public static Double Pow0_5(int numerator, int denominator) => Lookup(powLut0_5, numerator, denominator);

      private static readonly Double[] powLut0_3 = BuildPowLut(CDoubleMath.c0_3);
      public static Double Pow0_3(int numerator, int denominator) => Lookup(powLut0_3, numerator, denominator);

      public static Double[] BuildLut(Func<int, int, Double> f) => Util.Generate(LutSizeMinus1 + 1, i => f(i, LutSizeMinus1));
      public static Double[] BuildPowLut(Double pow) => BuildLut((i, j) => CDoubleMath.Pow((Double)i / (Double)j, pow));
      public static Double Lookup(Double[] lut, int numerator, int denominator) => lut[(int)((numerator * (long)LutSizeMinus1) / denominator)];
   }
}