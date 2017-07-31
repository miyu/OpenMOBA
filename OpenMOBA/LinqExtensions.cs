using System;
using System.Collections.Generic;
using System.Linq;

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

      //http://stackoverflow.com/questions/4681949/use-linq-to-group-a-sequence-of-numbers-with-no-gaps
      public static IEnumerable<IEnumerable<T>> GroupAdjacentBy<T>(
         this IEnumerable<T> source, Func<T, T, bool> predicate) {
         using (var e = source.GetEnumerator()) {
            if (e.MoveNext()) {
               var list = new List<T> { e.Current };
               var pred = e.Current;
               while (e.MoveNext()) {
                  if (predicate(pred, e.Current)) {
                     list.Add(e.Current);
                  } else {
                     yield return list;
                     list = new List<T> { e.Current };
                  }
                  pred = e.Current;
               }
               yield return list;
            }
         }
      }

      public static bool Add<K, V>(this Dictionary<K, HashSet<V>> dict, K key, V value) {
         HashSet<V> set;
         if (!dict.TryGetValue(key, out set)) {
            set = new HashSet<V>();
            dict[key] = set;
         }
         return set.Add(value);
      }

      public static bool Remove<K, V>(this Dictionary<K, HashSet<V>> dict, K key, V value) {
         HashSet<V> set;
         if (!dict.TryGetValue(key, out set)) {
            return false;
         }
         var res = set.Remove(value);
         if (set.Count == 0) {
            dict.Remove(key);
         }
         return res;
      }

      public static IEnumerable<KeyValuePair<int, T>> Enumerate<T>(this IEnumerable<T> items) {
         return items.Select((item, key) => new KeyValuePair<int, T>(key, item));
      }

      public static U[] Map<T, U>(this IReadOnlyList<T> arr, Func<T, U> map) {
         var result = new U[arr.Count];
         for (int i = 0; i < arr.Count; i++) {
            result[i] = map(arr[i]);
         }
         return result;
      }

      public static U[] Map<T, U>(this T[] arr, Func<T, U> map) {
         var result = new U[arr.Length];
         for (int i = 0; i < arr.Length; i++) {
            result[i] = map(arr[i]);
         }
         return result;
      }

      public static U[] MapMany<T, U>(this T[] arr, Func<T, IReadOnlyList<U>> cheapMap) {
         var result = new U[arr.Sum(x => cheapMap(x).Count)];
         var nextIndex = 0;
         for (var i = 0; i < arr.Length; i++) {
            var x = cheapMap(arr[i]);
            for (var j = 0; j < x.Count; j++) {
               result[nextIndex] = x[j];
               nextIndex++;
            }
         }
         return result;
      }

      public static V Get<K, V>(this Dictionary<K, V> dict, K key) => dict[key];

      public static T[] ToArray<T>(this IEnumerable<T> e, int len) {
         var enumerator = e.GetEnumerator();
         var result = new T[len];
         for (var i = 0; i < len; i++) {
            if (!enumerator.MoveNext()) {
               throw new IndexOutOfRangeException($"Enumerator didn't yield enough items. Stopped at i={i} of len={len}.");
            }
            result[i] = enumerator.Current;
         }
         return result;
      }
   }
}