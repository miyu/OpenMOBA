namespace Dargon.PlayOn.Foundation {
   public abstract class GameEventListener {
      public virtual void HandleEnterTick(EnterTickStatistics statistics) { }
      public virtual void HandleLeaveTick(LeaveTickStatistics statistics) { }
      public virtual void HandleEndOfGame() { }
   }
}