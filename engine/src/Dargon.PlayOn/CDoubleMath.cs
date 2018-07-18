using System;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn {
   public static class CDoubleMath {
      public static readonly cDouble c0;
      public static readonly cDouble c0_001 = (cDouble)1 / (cDouble)1000;
      public static readonly cDouble c0_01 = (cDouble)1 / (cDouble)100;
      public static readonly cDouble c0_1 = (cDouble)1 / (cDouble)10;
      public static readonly cDouble c0_3 = (cDouble)3 / (cDouble)10;
      public static readonly cDouble c0_5 = (cDouble)1 / (cDouble)2;
      public static readonly cDouble c0_8 = (cDouble)8 / (cDouble)10;
      public static readonly cDouble c0_9 = (cDouble)9 / (cDouble)10;
      public static readonly cDouble c1 = (cDouble)1;
      public static readonly cDouble c2 = (cDouble)2;
      public static readonly cDouble c3 = (cDouble)3;
      public static readonly cDouble c4 = (cDouble)4;
      public static readonly cDouble c5 = (cDouble)5;
      public static readonly cDouble c10 = (cDouble)10;
      public static readonly cDouble c100 = (cDouble)100;
      public static readonly cDouble cNeg1 = (cDouble)(-1);
      public static readonly cDouble cNeg2 = (cDouble)(-2);

#if use_fixed
      public static readonly cDouble Epsilon = cDouble.FromRaw(1);
      public static readonly cDouble Pi = cDouble.Pi;
      public static readonly cDouble TwoPi = cDouble.PiTimes2;
      public static readonly cDouble PiDiv2 = cDouble.PiOver2;
#else
      // C# double.Epsilon is denormal = terrible perf; avoid and use this instead.
      // https://www.johndcook.com/blog/2012/01/05/double-epsilon-dbl_epsilon/
      public const double Epsilon = 10E-16;
      public const cDouble Pi = Math.PI;
      public const cDouble TwoPi = Math.PI * 2;
      public const cDouble PiDiv2 = Math.PI / 2;
#endif

      public static cDouble Sqrt(cDouble v) {
#if use_fixed
         return cDouble.Sqrt(v);
#else
         return Math.Sqrt(v);
#endif
      }

      public static cDouble Abs(cDouble v) {
#if use_fixed
         return cDouble.Abs(v);
#else
         return Math.Abs(v);
#endif
      }

      public static cDouble Sin(cDouble v) {
#if use_fixed
         return cDouble.Sin(v);
#else
         return Math.Sin(v);
#endif
      }

      public static cDouble Cos(cDouble v) {
#if use_fixed
         return cDouble.Cos(v);
#else
         return Math.Cos(v);
#endif
      }

      public static cDouble Tan(cDouble v) {
#if use_fixed
         return cDouble.Tan(v);
#else
         return Math.Tan(v);
#endif
      }

      public static cDouble Acos(cDouble v) {
#if use_fixed
         return cDouble.Acos(v);
#else
         return Math.Acos(v);
#endif
      }

      public static cDouble Atan2(cDouble y, cDouble x) {
#if use_fixed
         return cDouble.Atan2(y, x);
#else
         return Math.Atan2(y, x);
#endif
      }

      public static cDouble Floor(cDouble v) {
#if use_fixed
         return cDouble.Floor(v);
#else
         return Math.Floor(v);
#endif
      }

      public static cDouble Round(cDouble v) {
#if use_fixed
         return cDouble.Round(v);
#else
         return Math.Round(v);
#endif
      }

      public static cDouble Ceiling(cDouble v) {
#if use_fixed
         return cDouble.Ceiling(v);
#else
         return Math.Ceiling(v);
#endif
      }

      public static cDouble Min(cDouble a, cDouble b) {
         return a < b ? a : b;
      }

      public static cDouble Max(cDouble a, cDouble b) {
         return a > b ? a : b;
      }

      public static int Sign(cDouble v) {
#if use_fixed
         return cDouble.Sign(v);
#else
         return Math.Sign(v);
#endif
      }


      public static uint NextUInt32(this Random r) {
         var lo = (uint)r.Next(4);
         var hi = (uint)(r.Next(1 << 30)) << 2;
         return lo | hi;
      }

      public static cDouble NextCDouble(this Random r) {
#if use_fixed
         Debug.Assert(cDouble.FRACTIONAL_PLACES == 32);
         return cDouble.FromRaw((long)r.NextUInt32());
#else
         return r.NextDouble();
#endif
      }

      public static cDouble Pow(cDouble b, cDouble exp) {
#if use_fixed
         return cDouble.Pow(b, exp);
#else
         return Math.Pow(b, exp);
#endif
      }
   }
}
