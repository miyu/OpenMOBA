using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas3D.LowLevel.Helpers {
   public static unsafe class UnmanagedCollections {
      // See: https://en.wikipedia.org/wiki/Quicksort#Algorithm
      public static void IndirectSort(int* arr, int* indexMapper, int offset, int length) {
//         void QuickSort(int lo, int hi) {
//            if (lo < hi) {
//               var p = partition(lo, hi);
//               QuickSort(lo, p - 1);
//               QuickSort(p + 1, hi);
//            }
//         }

         int partition(int lo, int hi) {
            var pivot = arr[indexMapper[hi]];
            var i = lo - 1;
            for (var j = lo; j <= hi - 1; j++) {
               if (arr[indexMapper[j]] < pivot) {
                  i++;
                  (indexMapper[i], indexMapper[j]) = (indexMapper[j], indexMapper[i]);
               }
            }
            (indexMapper[i + 1], indexMapper[hi]) = (indexMapper[hi], indexMapper[i + 1]);
            return i + 1;
         }

         var s = new Stack<(int, int)>();
         s.Push((offset, offset + length - 1));
         while (s.Count != 0) {
            var (lo, hi) = s.Pop();
            if (lo < hi) {
               var p = partition(lo, hi);
               s.Push((p + 1, hi));
               s.Push((lo, p - 1));
            }
         }
      }
   }
}
