using System;
using System.Collections.Generic;
using System.Linq;
using Dargon.Vox;

namespace OpenMOBA.Foundation {
   public class PeriodicStateSnapshotListener : GameEventListener {
      private readonly EntityWorld world;
      private readonly ReplayLog replayLog;
      private readonly int periodicity;

      public PeriodicStateSnapshotListener(EntityWorld world, ReplayLog replayLog, int periodicity) {
         this.world = world;
         this.replayLog = replayLog;
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
               if (!(component is INetworkedComponent nc)) continue;
               map[i] = nc.SaveState();
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