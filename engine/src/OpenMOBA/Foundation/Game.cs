using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

   public class InternalNetworkingLogService {
      private readonly List<object> list;

      public void Add(object y) {
        list.Add(y);
      }
   }

   [Guid("9F71AC5C-E738-4BFC-81AE-525AA8C286F0")]
   public class NetworkingLogServiceProxyDispatcher {
      private readonly InternalNetworkingLogService internalNetworkingLogService;

      public NetworkingLogServiceProxyDispatcher(InternalNetworkingLogService internalNetworkingLogService) {
         this.internalNetworkingLogService = internalNetworkingLogService;
      }
   }
}
