using System;
using System.Collections.Generic;
using Dargon.Vox;

namespace Dargon.PlayOn.Foundation {
   public class PeriodicStateSnapshotListener : GameEventListener {
      private readonly EntityWorld world;
      private readonly ReplayLog replayLog;
      private readonly Func<object, object>[] componentSnapshotFuncs;
      private readonly int periodicity;

      public PeriodicStateSnapshotListener(EntityWorld world, ReplayLog replayLog, Func<object, object>[] componentSnapshotFuncs, int periodicity) {
         this.world = world;
         this.replayLog = replayLog;
         this.componentSnapshotFuncs = componentSnapshotFuncs;
         this.periodicity = periodicity;
      }

      public override void HandleEnterTick(EnterTickStatistics statistics) {
         if (statistics.Tick % periodicity != 0) return;
         var snapshot = CaptureSnapshot();
         replayLog.Add(snapshot);
      } 

      private PeriodicStateSnapshot CaptureSnapshot() {
         var snapshot = new PeriodicStateSnapshot {
            EntitySnapshots = new Dictionary<int, Dictionary<int, object>>(),
            SystemSnapshots = new Dictionary<Type, object>()
         };
         foreach (var entity in world.EnumerateEntities()) {
            var map = new Dictionary<int, object>();
            for (var i = 0; i < entity.ComponentsByType.Length; i++) {
               var component = entity.ComponentsByType[i];
               var componentSnapshotFunc = componentSnapshotFuncs[i];
               if (componentSnapshotFunc == null) continue;
               map[i] = componentSnapshotFunc(component);
            }
            snapshot.EntitySnapshots[entity.Id] = map;
         }
         foreach (var system in world.EnumerateSystems()) {
            if (!(system is INetworkedSystem ns)) continue;
            snapshot.SystemSnapshots[system.GetType()] = ns.SaveState();
         }
         return snapshot;
      }
   }

   [AutoSerializable]
   public class PeriodicStateSnapshot {
      public Dictionary<int, Dictionary<int, object>> EntitySnapshots;
      public Dictionary<Type, object> SystemSnapshots;
   }
}