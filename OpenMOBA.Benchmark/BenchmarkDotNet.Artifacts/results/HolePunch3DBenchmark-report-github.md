``` ini

BenchmarkDotNet=v0.10.13, OS=Windows 10 Redstone 2 [1703, Creators Update] (10.0.15063.909)
Intel Core i7-4770K CPU 3.50GHz (Haswell), 1 CPU, 8 logical cores and 4 physical cores
Frequency=3417967 Hz, Resolution=292.5716 ns, Timer=TSC
  [Host]     : .NET Framework 4.6.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.2115.0
  DefaultJob : .NET Framework 4.6.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.2115.0


```
|                                 Method |        Mean |      Error |    StdDev |
|--------------------------------------- |------------:|-----------:|----------:|
|                              LoadBunny |    25.64 ms |  0.5102 ms |  1.203 ms |
|                           CompileBunny |   158.31 ms |  3.0820 ms |  3.027 ms |
| IncrementallyRecompileHolePunchedBunny | 2,422.09 ms | 47.8609 ms | 60.529 ms |
