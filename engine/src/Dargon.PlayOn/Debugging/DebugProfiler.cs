using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Dargon.PlayOn.Foundation;
using Newtonsoft.Json;

namespace Dargon.PlayOn.Debugging {
   public class DebugProfiler : GameEventListener {
      private readonly SortedDictionary<int, TickAnalytics> analyticsByTick = new SortedDictionary<int, TickAnalytics>();
      private readonly Stopwatch stopwatch = new Stopwatch();
      private TickAnalytics currentTickAnalytics;

      public override void HandleEnterTick(EnterTickStatistics statistics) {
         currentTickAnalytics = new TickAnalytics { StartTime = DateTime.UtcNow };
         analyticsByTick[statistics.Tick] = currentTickAnalytics;
         stopwatch.Restart();
      }

      public override void HandleLeaveTick(LeaveTickStatistics statistics) {
         currentTickAnalytics.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
      }

      // safe to call after LeaveTick
      public void AddStatistic(string key, double value) {
         List<double> dataPoints;
         if (!currentTickAnalytics.DataPoints.TryGetValue(key, out dataPoints)) {
            dataPoints = new List<double>();
            currentTickAnalytics.DataPoints[key] = dataPoints;
         }
         dataPoints.Add(value);
      }

      public void DumpToClipboard() {
         if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) {
            Clipboard.SetText(JsonConvert.SerializeObject(analyticsByTick));
         } else {
            var latch = new CountdownEvent(1);

            new Thread(() => {
               DumpToClipboard();
               latch.Signal();
            }) { ApartmentState = ApartmentState.STA }.Start();

            latch.Wait();
         }
      }

      public class TickAnalytics {
         public DateTime StartTime { get; set; }
         public long ElapsedMilliseconds { get; set; }
         public Dictionary<string, List<double>> DataPoints { get; set; } = new Dictionary<string, List<double>>();
      }
   }
}
