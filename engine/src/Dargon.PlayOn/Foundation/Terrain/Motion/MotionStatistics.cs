#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation.ECS {
   public struct MotionStatistics {
      public cDouble Radius;
      public cDouble Speed;
   }
}