using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenMOBA.Foundation.Visibility;
using OpenMOBA.Geometry;

namespace Benchmark {
   public class Program {
      public static void Main(string[] args) {
         BenchmarkRunner.Run<GeometryBenchmark>();
      }
   }

   public class GeometryBenchmark {
      private readonly Size mapDimensions;
      private readonly Polygon[] holePolygons;

      public GeometryBenchmark() {
         mapDimensions = new Size(1000, 1000);
         var simpleHoles = new[] {
            Polygon.CreateRectXY(100, 100, 300, 300),
            Polygon.CreateRectXY(400, 200, 100, 100),
            Polygon.CreateRectXY(200, -50, 100, 150),
            Polygon.CreateRectXY(600, 600, 300, 300),
            Polygon.CreateRectXY(700, 500, 100, 100),
            Polygon.CreateRectXY(200, 700, 100, 100),
            Polygon.CreateRectXY(600, 100, 300, 50),
            Polygon.CreateRectXY(600, 150, 50, 200),
            Polygon.CreateRectXY(850, 150, 50, 200),
            Polygon.CreateRectXY(600, 350, 300, 50),
            Polygon.CreateRectXY(700, 200, 100, 100)
         };

         var holeSquiggle = PolylineOperations.ExtrudePolygon(
            new[] {
               new IntVector2(100, 50),
               new IntVector2(100, 100),
               new IntVector2(200, 100),
               new IntVector2(200, 150),
               new IntVector2(200, 200),
               new IntVector2(400, 250),
               new IntVector2(200, 300),
               new IntVector2(400, 315),
               new IntVector2(200, 330),
               new IntVector2(210, 340),
               new IntVector2(220, 350),
               new IntVector2(220, 400),
               new IntVector2(221, 400)
            }.Select(iv => new IntVector2(iv.X + 160, iv.Y + 200)).ToArray(), 10).FlattenToPolygons();
         holePolygons = simpleHoles.Concat(holeSquiggle).ToArray();

         hd = HoleDilation();
      }

//      [Benchmark]
      public List<Polygon> HoleDilation() {
         return PolygonOperations.Offset().Include(holePolygons).Dilate(15).Execute().FlattenToPolygons();
      }

      private List<Polygon> hd;

      [Benchmark]
      public void VisibilityGraph() {
         VisibilityGraphOperations.CreateVisibilityGraph(mapDimensions, hd);
      }
   }
}
