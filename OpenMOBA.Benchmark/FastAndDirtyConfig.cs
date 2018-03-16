using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Jobs;

namespace OpenMOBA.Benchmarks {
   public class FastAndDirtyConfig : ManualConfig {
      public FastAndDirtyConfig() {
         Add(DefaultConfig.Instance); // *** add default loggers, reporters etc? ***

         Add(Job.Default
                .WithLaunchCount(1) // benchmark process will be launched only once
                .WithIterationTime(100 * TimeInterval.Millisecond) // 100ms per iteration
                .WithWarmupCount(3) // 3 warmup iteration
                .WithTargetCount(3) // 3 target iteration
         );
      }
   }
}