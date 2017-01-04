using System;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Utilities;

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

      public void ProcessPendingGameEvents() {
         var now = gameTimeService.Now;
         while (!gameEventQueue.IsEmpty && now >= gameEventQueue.Peek().Time) {
            var gameEvent = gameEventQueue.Dequeue();
            gameEvent.Execute();
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
      private readonly TerrainHole terrainHole;

      public AddTemporaryHoleGameEvent(GameTime time, GameLogicFacade gameLogicFacade, TerrainHole terrainHole) : base(time) {
         this.gameLogicFacade = gameLogicFacade;
         this.terrainHole = terrainHole;
      }

      public override void Execute() {
         Console.WriteLine("Add " + terrainHole.GetHashCode() + " at " + Time.Ticks);
         gameLogicFacade.AddTemporaryHole(terrainHole);
      }
   }

   public class RemoveTemporaryHoleGameEvent : GameEvent {
      private readonly GameLogicFacade gameLogicFacade;
      private readonly TerrainHole terrainHole;

      public RemoveTemporaryHoleGameEvent(GameTime time, GameLogicFacade gameLogicFacade, TerrainHole terrainHole) : base(time) {
         this.gameLogicFacade = gameLogicFacade;
         this.terrainHole = terrainHole;
      }

      public override void Execute() {
         Console.WriteLine("Remove " + terrainHole.GetHashCode() + " at " + Time.Ticks);
         gameLogicFacade.RemoveTemporaryHole(terrainHole);
      }
   }
}