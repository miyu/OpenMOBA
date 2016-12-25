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
   }

   public class AddTemporaryHoleGameEvent : GameEvent {
      private readonly TerrainService terrainService;
      private readonly TerrainHole terrainHole;

      public AddTemporaryHoleGameEvent(GameTime time, TerrainService terrainService, TerrainHole terrainHole) : base(time) {
         this.terrainService = terrainService;
         this.terrainHole = terrainHole;
      }

      public override void Execute() {
         terrainService.AddTemporaryHole(terrainHole);
      }
   }

   public class RemoveTemporaryHoleGameEvent : GameEvent {
      private readonly TerrainService terrainService;
      private readonly TerrainHole terrainHole;

      public RemoveTemporaryHoleGameEvent(GameTime time, TerrainService terrainService, TerrainHole terrainHole) : base(time) {
         this.terrainService = terrainService;
         this.terrainHole = terrainHole;
      }

      public override void Execute() {
         terrainService.RemoveTemporaryHole(terrainHole);
      }
   }
}