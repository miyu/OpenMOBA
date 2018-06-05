using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace OpenMOBA.Debugging {
   public class DebugProfiler {
      private readonly SortedDictionary<int, TickAnalytics> analyticsByTick = new SortedDictionary<int, TickAnalytics>();
      private readonly Stopwatch stopwatch = new Stopwatch();
      private TickAnalytics currentTickAnalytics;

      public void EnterTick(int tick) {
         currentTickAnalytics = new TickAnalytics { StartTime = DateTime.UtcNow };
         analyticsByTick[tick] = currentTickAnalytics;
         stopwatch.Restart();
      }

      public void LeaveTick() {
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
         Clipboard.SetText(JsonConvert.SerializeObject(analyticsByTick));
      }

      public class TickAnalytics {
         public DateTime StartTime { get; set; }
         public long ElapsedMilliseconds { get; set; }
         public Dictionary<string, List<double>> DataPoints { get; set; } = new Dictionary<string, List<double>>();
      }
   }
}
