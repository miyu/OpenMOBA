using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace MatrixMultiplicationPerformanceBenchmark {
   using SNMatrix4x4 = System.Numerics.Matrix4x4;
   using SNVector4 = System.Numerics.Vector4;

   public class Program {
      [STAThread]
      public static void Main() {
         Console.WriteLine(new TransformBenchmark().Numerics());
         Console.WriteLine(new TransformBenchmark().Custom());

         Console.WriteLine(new MultiplyBenchmark().Numerics());
         Console.WriteLine(new MultiplyBenchmark().Custom());

         Console.WriteLine(new MultiplyBenchmarkNoUnused().Numerics());
         Console.WriteLine(new MultiplyBenchmarkNoUnused().Custom());

         BenchmarkRunner.Run<TransformBenchmark>();
         BenchmarkRunner.Run<MultiplyBenchmark>();
         BenchmarkRunner.Run<MultiplyBenchmarkNoUnused>();
      }
   }

   public class TransformBenchmark {
      [Benchmark]
      public SNVector4 Numerics() {
         var mat = SNMatrix4x4.Identity;
         var vec = new SNVector4(1, 2, 3, 4);

         var acc = SNVector4.Zero;
         for (var i = 0; i < 1000; i++) {
            acc += SNVector4.Transform(vec, mat);
         }
         return acc;
      }

      [Benchmark]
      public SNVector4 Custom() {
         var mat = FMatrix4x4.Identity;
         var vec = new SNVector4(1, 2, 3, 4);

         var acc = SNVector4.Zero;
         for (var i = 0; i < 1000; i++) {
            acc += mat * vec;
         }
         return acc;
      }
   }

   public class MultiplyBenchmark {
      [Benchmark]
      public float Numerics() {
         var a = new SNMatrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
         var b = new SNMatrix4x4(17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32);

         var acc = 0.0f;
         for (var i = 0; i < 1000; i++) {
            acc += SNMatrix4x4.Multiply(a, b).M11;
         }
         return acc;
      }

      [Benchmark]
      public float Custom() {
         var a = new FMatrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
         var b = new FMatrix4x4(17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32);

         var acc = 0.0f;
         for (var i = 0; i < 1000; i++) {
            acc += (a * b).Row1.X;
         }
         return acc;
      }
   }

   public class MultiplyBenchmarkNoUnused {
      [Benchmark]
      public SNMatrix4x4 Numerics() {
         var a = new SNMatrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
         var b = new SNMatrix4x4(17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32);

         var acc = new SNMatrix4x4();
         for (var i = 0; i < 1000; i++) {
            acc += SNMatrix4x4.Multiply(a, b);
         }
         return acc;
      }

      [Benchmark]
      public FMatrix4x4 Custom() {
         var a = new FMatrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
         var b = new FMatrix4x4(17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32);

         var acc = new FMatrix4x4();
         for (var i = 0; i < 1000; i++) {
            acc += a * b;
         }
         return acc;
      }
   }
}