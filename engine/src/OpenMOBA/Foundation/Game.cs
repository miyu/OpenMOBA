using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dargon.Vox;
using OpenMOBA.Debugging;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.Declarations;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Foundation {
   public interface IGameEventFactory {
      GameEvent CreateAddTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription temporaryHoleDescription);
      GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription temporaryHoleDescription);
   }

   public class Game : IGameEventFactory {
      public List<GameEventListener> Debuggers { get; set; } = new List<GameEventListener> { new DebugProfiler() }; // really should be concurrentset
      public GameTimeManager GameTimeManager { get; set; }
      public GameEventQueueManager GameEventQueueManager { get; set; }
      public TerrainFacade TerrainFacade { get; set; }
      public EntityWorld EntityWorld { get; set; }
      public PathfinderCalculator PathfinderCalculator { get; set; }
      public MovementSystem MovementSystem { get; set; }
      public GameLogicFacade GameLogicFacade { get; set; }

      public GameEvent CreateAddTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) {
         return new AddTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHoleDescription);
      }

      public GameEvent CreateRemoveTemporaryHoleEvent(GameTime time, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) {
         return new RemoveTemporaryHoleGameEvent(time, GameLogicFacade, dynamicTerrainHoleDescription);
      }

      public void Initialize() {
         EntityWorld.InitializeSystems();
      }

      public void Run() {
         var sw = new Stopwatch();
         sw.Start();

         while (true) {
            if (GameTimeManager.Ticks >= GameTimeManager.TicksPerSecond * 20) {
               Console.WriteLine($"Done! {sw.Elapsed.TotalSeconds} at tick {GameTimeManager.Ticks}");
               foreach (var debugger in Debuggers) {
                  debugger.HandleEndOfGame();
               }
               break;
            }
            Tick();
         }
      }

      public void Tick() {
         foreach (var debugger in Debuggers) {
            debugger.HandleEnterTick(new EnterTickStatistics {
               Tick = GameTimeManager.Ticks
            });
         }

         GameEventQueueManager.ProcessPendingGameEvents(out var eventsProcessed);

         EntityWorld.ProcessSystems();

         foreach (var debugger in Debuggers) {
            debugger.HandleLeaveTick(new LeaveTickStatistics {
               EventsProcessed = eventsProcessed,
               Tick = GameTimeManager.Ticks
            });
         }

         GameTimeManager.IncrementTicks();
      }
   }

   public class ReplayLog {
      private readonly Guid secret;
      private readonly Dictionary<Guid, Cons<(int Index, object Item)>> readersToReadProgress;
      // Network log starts with a sentinel noop, considered already read by readers
      private Cons<(int Index, object Item)> tail = new Cons<(int, object)> {
         Value = (0, (object)new ReplayLogEntries.Noop())
      };

      public ReplayLog(Guid secret, Guid[] readers) {
         this.secret = secret;
         this.readersToReadProgress = readers.UniqueMap(_ => tail);
      }

      public Guid Secret => secret;

      public void Add(object item) {
         tail = tail.Next = new Cons<(int, object)> {
            Value = (tail.Value.Index + 1, item)
         };
      }

      public object[] Since(int i) => tail.Skip(i).ToArray();

      public void Done(Guid g) {

      }

      public class Cons<T> {
         public T Value { get; set; }
         public Cons<T> Next { get; set; }
      }
   }

   public class ReplayLogEntries {
      [AutoSerializable]
      public class Noop { }
   }

   public class ReplayLogManager {
      private readonly Dictionary<Guid, ReplayLog> logs = new Dictionary<Guid, ReplayLog>();

      public ReplayLog Create(Guid key, Guid secret, Guid[] cullers) {
         var log = new ReplayLog(secret, cullers);
         logs.Add(key, log);
         return log;
      }

      public ReplayLog Get(Guid key) => logs[key];
   }

   [Guid("9F71AC5C-E738-4BFC-81AE-525AA8C286F0")]
   public class NetworkingServiceProxyDispatcher {
      private readonly ReplayLogManager replayLogManager;

      public NetworkingServiceProxyDispatcher(ReplayLogManager replayLogManager) {
         this.replayLogManager = replayLogManager;
      }
      
      public object[] GetLog(Guid guid, Guid secret, int i) {
         var rlm = replayLogManager.Get(guid);
         if (rlm.Secret != secret) throw new InvalidOperationException();
         return rlm.Since(i);
      }
   }
}
