namespace OpenMOBA.Foundation {
   public interface IReadableGameTimeService
   {
      int TicksPerSecond { get; }
      int Ticks { get; }
      GameTime Now { get; }
      double ElapsedTimeSeconds { get; }
   }

   public interface IMutableGameTimeService : IReadableGameTimeService
   {
      void IncrementTicks();
   }

   public class GameTimeService : IMutableGameTimeService
   {
      public GameTimeService(int ticksPerSecond)
      {
         TicksPerSecond = ticksPerSecond;
         SecondsPerTick = 1.0 / ticksPerSecond;
      }

      public int TicksPerSecond { get; }
      public double SecondsPerTick { get; }
      public int Ticks { get; private set; }
      public GameTime Now => new GameTime(Ticks);
      public double ElapsedTimeSeconds { get; private set; }

      public void IncrementTicks()
      {
         Ticks++;
         ElapsedTimeSeconds = Ticks / (double) TicksPerSecond;
      }
   }
}