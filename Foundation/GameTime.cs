using System;

namespace OpenMOBA.Foundation
{
    public struct GameTime : IComparable<GameTime> {
        public int Ticks { get; set; }

        public int CompareTo(GameTime other) => Ticks.CompareTo(other.Ticks);

        public static bool operator <(GameTime a, GameTime b) => a.Ticks < b.Ticks;
        public static bool operator >(GameTime a, GameTime b) => a.Ticks > b.Ticks;
        public static bool operator <=(GameTime a, GameTime b) => a.Ticks <= b.Ticks;
        public static bool operator >=(GameTime a, GameTime b) => a.Ticks >= b.Ticks;
        public static bool operator ==(GameTime a, GameTime b) => a.Ticks == b.Ticks;
        public static bool operator !=(GameTime a, GameTime b) => a.Ticks != b.Ticks;
    }
}