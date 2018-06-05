``` ini

BenchmarkDotNet=v0.10.13, OS=Windows 10 Redstone 3 [1709, Fall Creators Update] (10.0.16299.125)
Intel Core i7-4770K CPU 3.50GHz (Haswell), 1 CPU, 8 logical cores and 4 physical cores
Frequency=3417965 Hz, Resolution=292.5717 ns, Timer=TSC
  [Host]     : .NET Framework 4.6.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.2600.0
  Job-PEFJFA : .NET Framework 4.6.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.2600.0

IterationTime=100.0000 ms  LaunchCount=1  TargetCount=3  
WarmupCount=3  

```
|                                 Method |           Mean |          Error |         StdDev |
|--------------------------------------- |---------------:|---------------:|---------------:|
|                              LoadBunny |  31,365.994 us |  18,824.341 us |  1,063.6102 us |
|                           CompileBunny |       3.341 us |       6.000 us |      0.3390 us |
| IncrementallyRecompileHolePunchedBunny | 618,428.353 us | 668,805.926 us | 37,788.7751 us |
