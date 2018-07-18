#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Foundation {
   public interface IReadableGameTimeService
   {
      int TicksPerSecond { get; }
      int Ticks { get; }
      GameTime Now { get; }
      cDouble ElapsedTimeSeconds { get; }
   }

   public interface IMutableGameTimeService : IReadableGameTimeService
   {
      void IncrementTicks();
   }

   public class GameTimeManager : IMutableGameTimeService
   {
      public GameTimeManager(int ticksPerSecond)
      {
         TicksPerSecond = ticksPerSecond;
         SecondsPerTick = CDoubleMath.c1 / (cDouble)ticksPerSecond;
      }

      public int TicksPerSecond { get; }
      public cDouble SecondsPerTick { get; }
      public int Ticks { get; private set; }
      public GameTime Now => new GameTime(Ticks);
      public cDouble ElapsedTimeSeconds { get; private set; }

      public void IncrementTicks()
      {
         Ticks++;
         ElapsedTimeSeconds = (cDouble)Ticks / (cDouble)TicksPerSecond;
      }
   }
}