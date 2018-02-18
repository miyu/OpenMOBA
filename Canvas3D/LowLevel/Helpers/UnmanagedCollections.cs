using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas3D.LowLevel.Helpers {
   public static unsafe class UnmanagedCollections {
      public static void IndirectSort(int* arr, int* indexMapper, int offset, int length) {
         IndirectSortInternal(arr, indexMapper, offset, offset + length - 1);
      }

      // via https://stackoverflow.com/questions/3719719/fastest-safe-sorting-algorithm-implementation
      // bounds are inclusive
      private static void IndirectSortInternal(int* arr, int* indexMapper, int left, int right) {
         int i = left - 1;
         int j = right;

         while (true) {
            int d = arr[indexMapper[left]];
            do i++; while (arr[indexMapper[i]] < d);
            do j--; while (arr[indexMapper[j]] > d);

            if (i < j) {
               int tmp = arr[indexMapper[i]];
               arr[indexMapper[i]] = arr[indexMapper[j]];
               arr[indexMapper[j]] = tmp;
            } else {
               if (left < j) IndirectSortInternal(arr, indexMapper, left, j);
               if (++j < right) IndirectSortInternal(arr, indexMapper, j, right);
               return;
            }
         }
      }
   }
}
