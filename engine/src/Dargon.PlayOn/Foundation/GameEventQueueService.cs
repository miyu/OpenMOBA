using System;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.Terrain.Declarations;

namespace Dargon.PlayOn.Foundation {
   public interface IGameEventQueueManager {
      void AddGameEvent(GameEvent gameEvent);
      void RemoveGameEvent(GameEvent banlist);
   }

   public class GameEventQueueManager : IGameEventQueueManager {
      private readonly RemovablePriorityQueue<GameEvent> gameEventQueue = new RemovablePriorityQueue<GameEvent>(GameEvent.CompareByTime);
      private readonly GameTimeManager gameTimeManager;

      public GameEventQueueManager(GameTimeManager gameTimeManager) {
         this.gameTimeManager = gameTimeManager;
      }

      public void AddGameEvent(GameEvent gameEvent) {
         gameEventQueue.Enqueue(gameEvent);
      }

      public void RemoveGameEvent(GameEvent gameEvent) {
         gameEventQueue.Remove(gameEvent);
      }

      public void ProcessPendingGameEvents(out int eventsProcessed) {
         eventsProcessed = 0;
         var now = gameTimeManager.Now;
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
      private readonly DynamicTerrainHoleDescription dynamicTerrainHoleDescription;

      public AddTemporaryHoleGameEvent(GameTime time, GameLogicFacade gameLogicFacade, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) : base(time) {
         this.gameLogicFacade = gameLogicFacade;
         this.dynamicTerrainHoleDescription = dynamicTerrainHoleDescription;
      }

      public override void Execute() {
         Console.WriteLine("Add " + dynamicTerrainHoleDescription.GetHashCode() + " at " + Time.Ticks);
         gameLogicFacade.AddTemporaryHole(dynamicTerrainHoleDescription);
      }
   }

   public class RemoveTemporaryHoleGameEvent : GameEvent {
      private readonly GameLogicFacade gameLogicFacade;
      private readonly DynamicTerrainHoleDescription dynamicTerrainHoleDescription;

      public RemoveTemporaryHoleGameEvent(GameTime time, GameLogicFacade gameLogicFacade, DynamicTerrainHoleDescription dynamicTerrainHoleDescription) : base(time) {
         this.gameLogicFacade = gameLogicFacade;
         this.dynamicTerrainHoleDescription = dynamicTerrainHoleDescription;
      }

      public override void Execute() {
         Console.WriteLine("Remove " + dynamicTerrainHoleDescription.GetHashCode() + " at " + Time.Ticks);
         gameLogicFacade.RemoveTemporaryHole(dynamicTerrainHoleDescription);
      }
   }
}