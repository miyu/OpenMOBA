using System;
using System.Collections.Generic;
using System.Text;
using Exception = System.Exception;

namespace Dargon.Terragami.Tests {
   public static class Program {
      public static void Main(string[] args) {
         // new CoordinateSystemConventionsTests().TerrainDefinitionIsPositiveClockWiseNegativeCounterClockWise();
         // new CoordinateSystemConventionsTests().PolygonUnionPunchOperationsOrientationsArentBorked_FourSquareDonutTests();
         // new ArrangementOfLinesTests().Test();
         // new VisibilityPolygonQueryTests().Exec();
         // new VisibilityPolygonOfSimplePolygonsTests().Exec();

         // try {
            new VisibilityPolygonOfSimplePolygonsTests().Execute();
         // } catch (Exception e) {
            // Console.Error.WriteLine(e);
            // while (true) ;
         // }
      }
   }
}
