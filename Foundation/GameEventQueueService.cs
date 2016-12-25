using OpenMOBA.Utilities;

namespace OpenMOBA.Foundation
{
   public interface IGameEventQueueService
   {
      void AddGameEvent(GameEvent gameEvent);
      void RemoveGameEvent(GameEvent banlist);
   }

   public class GameLoop : IGameEventQueueService
   {
      private readonly PriorityQueue<GameEvent> gameEventQueue = new PriorityQueue<GameEvent>(GameEvent.CompareByTime);
      private readonly GameTimeService gameTimeService;

      public GameLoop(GameTimeService gameTimeService)
      {
         this.gameTimeService = gameTimeService;
      }

      public void AddGameEvent(GameEvent gameEvent)
      {
         gameEventQueue.Enqueue(gameEvent);
      }

      public void RunLoop()
      {
         while (true)
         {
            var now = gameTimeService.Now;
            while (now >= gameEventQueue.Peek().Time)
            {
               var gameEvent = gameEventQueue.Dequeue();
               gameEvent.Execute();
            }
            gameTimeService.IncrementTicks();
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