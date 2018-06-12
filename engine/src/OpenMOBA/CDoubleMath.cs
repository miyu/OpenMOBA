using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA {
   public static class CDoubleMath {
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

      public static cDouble Ceiling(cDouble v) {
#if use_fixed
         return cDouble.Ceiling(v);
#else
         return Math.Ceiling(v);
#endif
      }
   }
}
