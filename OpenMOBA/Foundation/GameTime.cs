using System;

namespace OpenMOBA.Foundation {
   public struct GameTime : IComparable<GameTime>, IEquatable<GameTime> {
      public GameTime(int ticks) {
         Ticks = ticks;
      }

      public int Ticks { get; }

      public int CompareTo(GameTime other) => Ticks.CompareTo(other.Ticks);

      public static bool operator <(GameTime a, GameTime b) => a.Ticks < b.Ticks;
      public static bool operator >(GameTime a, GameTime b) => a.Ticks > b.Ticks;
      public static bool operator <=(GameTime a, GameTime b) => a.Ticks <= b.Ticks;
      public static bool operator >=(GameTime a, GameTime b) => a.Ticks >= b.Ticks;
      public static bool operator ==(GameTime a, GameTime b) => a.Ticks == b.Ticks;
      public static bool operator !=(GameTime a, GameTime b) => a.Ticks != b.Ticks;
      public bool Equals(GameTime other) => Ticks == other.Ticks;
      public override int GetHashCode() => Ticks.GetHashCode();
   }
}