``` ini

BenchmarkDotNet=v0.10.13, OS=Windows 10 Redstone 3 [1709, Fall Creators Update] (10.0.16299.125)
Intel Core i7-4770K CPU 3.50GHz (Haswell), 1 CPU, 8 logical cores and 4 physical cores
Frequency=3417965 Hz, Resolution=292.5717 ns, Timer=TSC
  [Host]     : .NET Framework 4.6.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.2600.0
  DefaultJob : .NET Framework 4.6.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.2600.0


```
|                Method |     Mean |     Error |    StdDev |
|---------------------- |---------:|----------:|----------:|
|      FastMapBenchmark | 5.762 ms | 0.1232 ms | 0.1265 ms |
| ToDictionaryBenchmark | 6.059 ms | 0.1183 ms | 0.1877 ms |
