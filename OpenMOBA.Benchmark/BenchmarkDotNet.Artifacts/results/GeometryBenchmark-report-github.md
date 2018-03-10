``` ini

BenchmarkDotNet=v0.10.1, OS=Microsoft Windows NT 6.2.9200.0
Processor=Intel(R) Core(TM) i7-6600U CPU 2.60GHz, ProcessorCount=4
Frequency=2742187 Hz, Resolution=364.6724 ns, Timer=TSC
  [Host]     : Clr 4.0.30319.42000, 64bit RyuJIT-v4.6.1586.0
  DefaultJob : Clr 4.0.30319.42000, 64bit RyuJIT-v4.6.1586.0

Gen 0=55.2083  Allocated=204.76 kB  

```
          Method |      Mean |    StdDev |
---------------- |---------- |---------- |
 VisibilityGraph | 1.1792 ms | 0.0125 ms |
