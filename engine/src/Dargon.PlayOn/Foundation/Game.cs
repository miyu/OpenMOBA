using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dargon.Commons;
using Dargon.PlayOn.Debugging;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.Vox;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else

#endif

namespace Dargon.PlayOn.Foundation {
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
      private Cons<(int Index, object Item, byte[] Serialization)> tail;
      private Cons<(int Index, object Item, byte[] Serialization)> head;

      // Basic auth token guarding read/writes.
      private readonly Dictionary<Guid, ReaderContext> readerContextByAccessToken;

      public ReplayLog(Dictionary<Guid, Guid> accessTokensByReaderId) {
         // Network log starts with a sentinel noop, considered already read by readers
         tail = head = new Cons<(int, object, byte[])> {
            Value = (0, ReplayLogEntries.Noop.Instance, Serialize.ToBytes(ReplayLogEntries.Noop.Instance))
         };
         this.readerContextByAccessToken = accessTokensByReaderId.MapByValue(
            accessToken => new ReaderContext(accessToken, tail));
      }

      public void Add(object item, byte[] serialization = null) {
         tail = tail.Next = new Cons<(int, object, byte[])> {
            Value = (tail.Value.Index + 1, item, serialization ?? Serialize.ToBytes(item))
         };
      }

      public bool TryGetReaderContext(Guid accessToken, out ReaderContext readerContext) {
         return readerContextByAccessToken.TryGetValue(accessToken, out readerContext);
      }

      public class ReaderContext {
         private readonly Guid accessToken;
         private Cons<(int Index, object Item, byte[] Serialization)> greatestAcknowledgedLogEntry;

         public ReaderContext(Guid accessToken, Cons<(int Index, object Item, byte[] Serialization)> greatestAcknowledgedLogEntry) {
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

         public void Peek(byte[][] retbuf) {
            var current = greatestAcknowledgedLogEntry.Next;
            for (var i = 0; i < retbuf.Length; i++) {
               retbuf[i] = current?.Value.Serialization;
               current = current?.Next;
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
      public class Noop {
         public static readonly Noop Instance = new Noop();
      }
   }

   public class ReplayLogManager {
      private readonly Dictionary<Guid, ReplayLog> logs = new Dictionary<Guid, ReplayLog>();

      public ReplayLog Create(Guid key, Guid secret, Guid[] cullers) {
         throw new NotImplementedException();
//         var log = new ReplayLog(secret, cullers);
//         logs.Add(key, log);
//         return log;
      }

      public bool TryGetReplayLog(Guid key, out ReplayLog log) => logs.TryGetValue(key, out log);
   }

   [Guid("9F71AC5C-E738-4BFC-81AE-525AA8C286F0")]
   public class ReplayLogService {
      private readonly ReplayLogManager replayLogManager;

      public ReplayLogService(ReplayLogManager replayLogManager) {
         this.replayLogManager = replayLogManager;
      }
      
      public byte[][] GetLog(Guid guid, Guid accessToken, int ack) {
         var log = GetReplayLogReaderContextAndValidateAccessToken(guid, accessToken);
         log.Acknowledge(ack);
         var retbuf = new byte[50][];
         log.Peek(retbuf);
         return retbuf;
      }

      private ReplayLog.ReaderContext GetReplayLogReaderContextAndValidateAccessToken(Guid key, Guid accessToken) {
         if (!replayLogManager.TryGetReplayLog(key, out var log) ||
             !log.TryGetReaderContext(accessToken, out var readerContext)) {
            throw new Exception("auth"); // todo
         }
         return readerContext;
      }
   }
}
