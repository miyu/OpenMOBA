using System;
using System.Collections.Generic;

namespace OpenMOBA {
   public static class LinqExtensions {
      public static bool TryFindFirst<T>(this IEnumerable<T> enumerable, Predicate<T> predicate, out T firstMatch) {
         foreach (var x in enumerable) {
            if (predicate(x)) {
               firstMatch = x;
               return true;
            }
         }
         firstMatch = default(T);
         return false;
      }

      public static IReadOnlyList<T> CastToReadOnlyList<T>(this IList<T> list) {
         return (IReadOnlyList<T>)list;
      }
   }
}