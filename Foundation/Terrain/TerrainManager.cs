using OpenMOBA.Geometry;
using OpenMOBA.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenMOBA.Foundation {
   public class MapConfiguration {
      public Size Size { get; set; }
      public List<Polygon> StaticHolePolygons { get; set; }
   }

   public class GameTimeManager {
      public GameTimeManager(int ticksPerSecond) {
         TicksPerSecond = ticksPerSecond;
      }

      public int TicksPerSecond { get; }
      public int Ticks { get; private set; }
      public GameTime Now => new GameTime { Ticks = Ticks };
      public double ElapsedTimeSeconds { get; private set; }

      public void IncrementTicks() {
         Ticks++;
         ElapsedTimeSeconds = Ticks / (double)TicksPerSecond;
      }
   }

   public class TerrainSnapshot {
      public int Version { get; }
   }

   public class TerrainManager {
      private const int kMaxBarrierCount = short.MaxValue;

      private readonly MapConfiguration mapConfiguration;
      private readonly GameTimeManager gameTimeManager;

      private readonly PriorityQueue<TemporaryBarrier> barrierQueue = new PriorityQueue<TemporaryBarrier>(BarrierEndTimeComparer);

      private int version;
      private TerrainSnapshot snapshot;

      public TerrainManager(MapConfiguration mapConfiguration, GameTimeManager gameTimeManager) {
         this.mapConfiguration = mapConfiguration;
         this.gameTimeManager = gameTimeManager;
      }

      public void AddTemporaryBarrier(TemporaryBarrier barrier) {
         barrierQueue.Enqueue(barrier);
      }

      public TerrainSnapshot TakeSnapshot() {
         if (snapshot == null || snapshot.Version != version ) {
            var now = gameTimeManager.Now;
            while (!barrierQueue.IsEmpty && barrierQueue.Peek().EndTime >= now) {
               barrierQueue.Dequeue();
            }

//            snapshot = BuildSnapshot(mapConfiguration, barrierQueue);
         }

         return snapshot;
      }

      private static TerrainSnapshot BuildSnapshot(int x, IReadOnlyCollection<TemporaryBarrier> barriers) {
         return null;
      }

      private static int BarrierEndTimeComparer(TemporaryBarrier x, TemporaryBarrier y) {
         return x.EndTime.CompareTo(y.EndTime);
      }
   }

   public class TemporaryBarrier {
      /// <summary>
      /// Time in game ticks at which the barrier disappears.
      /// At the given tick, the barrier is no longer valid.
      /// </summary>
      public GameTime EndTime { get; set; }
      public IReadOnlyList<Polygon> Polygons { get; set; }
   }

   public class Blockade {
      public GameTime EndTime { get; set; }
   }

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
