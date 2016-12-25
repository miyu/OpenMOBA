using OpenMOBA.Utilities;

namespace OpenMOBA.Foundation
{
   public interface IGameEventQueueService
   {
      void AddGameEvent(GameEvent gameEvent);
      void RemoveGameEvent(GameEvent banlist);
   }

   public class GameEventQueueService : IGameEventQueueService
   {
      private readonly RemovablePriorityQueue<GameEvent> gameEventQueue = new RemovablePriorityQueue<GameEvent>(GameEvent.CompareByTime);
      private readonly GameTimeService gameTimeService;

      public GameEventQueueService(GameTimeService gameTimeService)
      {
         this.gameTimeService = gameTimeService;
      }

      public void AddGameEvent(GameEvent gameEvent)
      {
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

   public abstract class GameEvent
   {
      public GameTime Time { get; set; }
      public abstract void Execute();

      public static int CompareByTime(GameEvent a, GameEvent b)
      {
         return a.Time.CompareTo(b.Time);
      }
   }
}