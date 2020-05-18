using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dargon.Commons;
using Xunit;
using Xunit.Abstractions;

namespace Dargon.Terragami.Tests {
   public class MathUtilsTests {
      [Fact]
      public void FastAtan2ErrorBounds() {
         var data = new List<double>();
         for (var cy = -1000; cy <= 1000; cy++) {
            for (var cx = -1000; cx <= 1000; cx++) {
               var ground = Math.Atan2(cy / 1000.0, cx / 1000.0);
               var fast = MathUtils.FastAtan2(cy / 1000.0, cx / 1000.0);
               
               if (cx == 0 && cy == 0) {
                  Console.WriteLine("At origin ground: " + ground + " fast: " + fast);
               } else {
                  data.Add(ground - fast);
               }
            }
         }
         Console.WriteLine(data.MinBy(x => x) + " " + data.MaxBy(x => x));
      }
   }
}
