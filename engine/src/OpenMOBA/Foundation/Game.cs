using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Dargon.Commons;
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
      private Cons<(int Index, object Item)> tail;
      private Cons<(int Index, object Item)> head;

      // Basic auth token guarding read/writes.
      private readonly Dictionary<Guid, ReaderContext> readerContextByReaderId;

      public ReplayLog(Dictionary<Guid, Guid> accessTokensByReaderId) {
         // Network log starts with a sentinel noop, considered already read by readers
         tail = head = new Cons<(int, object)> {
            Value = (0, (object)new ReplayLogEntries.Noop())
         };
         this.readerContextByReaderId = accessTokensByReaderId.MapByValue(
            accessToken => new ReaderContext(accessToken, tail));
      }

      public void Add(object item) {
         tail = tail.Next = new Cons<(int, object)> {
            Value = (tail.Value.Index + 1, item)
         };
      }

      public class ReaderContext {
         private readonly Guid accessToken;
         private Cons<(int Index, object Item)> greatestAcknowledgedLogEntry;

         public ReaderContext(Guid accessToken, Cons<(int Index, object Item)> greatestAcknowledgedLogEntry) {
            this.accessToken = accessToken;
            this.greatestAcknowledgedLogEntry = greatestAcknowledgedLogEntry;
         }


         public void Acknowledge(int logEntryIndex) {
            var n = greatestAcknowledgedLogEntry;
            while (n.Value.Index < logEntryIndex) {
               n = n.Next;
            }
            Assert.Equals(n.Value.Index, logEntryIndex);
            greatestAcknowledgedLogEntry = n;
         }

         public void Peek(int n) {
            var current = greatestAcknowledgedLogEntry.Next;
            for (var i = 0; i < n && current != null; i++) {
            }
         }
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
         throw new NotImplementedException();
//         var log = new ReplayLog(secret, cullers);
//         logs.Add(key, log);
//         return log;
      }

      public ReplayLog Get(Guid key) => logs[key];
   }

   [Guid("9F71AC5C-E738-4BFC-81AE-525AA8C286F0")]
   public class NetworkingServiceProxyDispatcher {
      private readonly ReplayLogManager replayLogManager;

      public NetworkingServiceProxyDispatcher(ReplayLogManager replayLogManager) {
         this.replayLogManager = replayLogManager;
      }
      
      public object[] GetLog(Guid guid, Guid token, int ack) {
         var rlm = GetAndVerifyReplayLogManager(guid, token);
//         return rlm.Done(guid, ack);
         throw new NotImplementedException();
      }

      private ReplayLog GetAndVerifyReplayLogManager(Guid key, Guid token) {
         throw new NotImplementedException();
//         var log = replayLogManager.Get(guid);
//         if (log.Secret != token) throw new InvalidOperationException();
//         return log;
      }
   }
}
