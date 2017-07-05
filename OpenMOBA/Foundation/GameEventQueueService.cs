using System;
using OpenMOBA.DataStructures;
using OpenMOBA.Foundation.Terrain;

namespace OpenMOBA.Foundation {
   public interface IGameEventQueueService {
      void AddGameEvent(GameEvent gameEvent);
      void RemoveGameEvent(GameEvent banlist);
   }

   public class GameEventQueueService : IGameEventQueueService {
      private readonly RemovablePriorityQueue<GameEvent> gameEventQueue = new RemovablePriorityQueue<GameEvent>(GameEvent.CompareByTime);
      private readonly GameTimeService gameTimeService;

      public GameEventQueueService(GameTimeService gameTimeService) {
         this.gameTimeService = gameTimeService;
      }

      public void AddGameEvent(GameEvent gameEvent) {
         gameEventQueue.Enqueue(gameEvent);
      }

      public void RemoveGameEvent(GameEvent gameEvent) {
         gameEventQueue.Remove(gameEvent);
      }

      public void ProcessPendingGameEvents(out int eventsProcessed) {
         eventsProcessed = 0;
         var now = gameTimeService.Now;
         while (!gameEventQueue.IsEmpty && now >= gameEventQueue.Peek().Time) {
            var gameEvent = gameEventQueue.Dequeue();
            gameEvent.Execute();
            eventsProcessed++;
         }
      }
   }

   public abstract class GameEvent {
      protected GameEvent(GameTime time) {
         Time = time;
      }

      public GameTime Time { get; }
      public abstract void Execute();

      public static int CompareByTime(GameEvent a, GameEvent b) {
         return a.Time.CompareTo(b.Time);
      }

      public override string ToString() => $"[{GetType().Name} at {Time.Ticks} Ticks]";
   }

   public class AddTemporaryHoleGameEvent : GameEvent {
      private readonly GameLogicFacade gameLogicFacade;
      private readonly DynamicTerrainHole dynamicTerrainHole;

      public AddTemporaryHoleGameEvent(GameTime time, GameLogicFacade gameLogicFacade, DynamicTerrainHole dynamicTerrainHole) : base(time) {
         this.gameLogicFacade = gameLogicFacade;
         this.dynamicTerrainHole = dynamicTerrainHole;
      }

      public override void Execute() {
         Console.WriteLine("Add " + dynamicTerrainHole.GetHashCode() + " at " + Time.Ticks);
         gameLogicFacade.AddTemporaryHole(dynamicTerrainHole);
      }
   }

   public class RemoveTemporaryHoleGameEvent : GameEvent {
      private readonly GameLogicFacade gameLogicFacade;
      private readonly DynamicTerrainHole dynamicTerrainHole;

      public RemoveTemporaryHoleGameEvent(GameTime time, GameLogicFacade gameLogicFacade, DynamicTerrainHole dynamicTerrainHole) : base(time) {
         this.gameLogicFacade = gameLogicFacade;
         this.dynamicTerrainHole = dynamicTerrainHole;
      }

      public override void Execute() {
         Console.WriteLine("Remove " + dynamicTerrainHole.GetHashCode() + " at " + Time.Ticks);
         gameLogicFacade.RemoveTemporaryHole(dynamicTerrainHole);
      }
   }
}