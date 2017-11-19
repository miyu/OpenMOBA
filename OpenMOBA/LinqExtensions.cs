   using System;
using System.Collections.Generic;
   using System.Diagnostics;
   using System.Linq;
   using System.Runtime.CompilerServices;

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

      public static U[] Map<T, U>(this IReadOnlyList<T> arr, Func<T, int, U> map) {
         var result = new U[arr.Count];
         for (int i = 0; i < arr.Count; i++) {
            result[i] = map(arr[i], i);
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

      public static U[] Map<T, U>(this T[] arr, Func<T, int, U> map) {
         var result = new U[arr.Length];
         for (int i = 0; i < arr.Length; i++) {
            result[i] = map(arr[i], i);
         }
         return result;
      }

      public static Dictionary<K, R> Map<K, V, R>(this IReadOnlyDictionary<K, V> dict, Func<K, V, R> map) {
         return dict.ToDictionary(kvp => kvp.Key, kvp => map(kvp.Key, kvp.Value));
      }

      public static Dictionary<RK, RV> Map<K, V, RK, RV>(this IReadOnlyDictionary<K, V> dict, Func<K, V, RK> kmap, Func<K, V, RV> vmap) {
         return dict.ToDictionary(kvp => kmap(kvp.Key, kvp.Value), kvp => vmap(kvp.Key, kvp.Value));
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

      /// <summary>                                                                                              
      /// Checks whether argument is <see langword="null"/> and throws <see cref="ArgumentNullException"/> if so.
      /// </summary>                                                                                             
      /// <param name="argument">Argument to check on <see langword="null"/>.</param>                            
      /// <param name="argumentName">Argument name to pass to Exception constructor.</param>                     
      /// <returns>Specified argument.</returns>                                                                 
      /// <exception cref="ArgumentNullException"/>                                                              
      [DebuggerStepThrough]
      public static T ThrowIfNull<T>(this T argument, string argumentName)
         where T : class {
         if (argument == null) {
            throw new ArgumentNullException(argumentName);
         } else {
            return argument;
         }
      }

      public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source,
         Func<TSource, TKey> selector) {
         return source.MinBy(selector, Comparer<TKey>.Default);
      }

      public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source,
         Func<TSource, TKey> selector, IComparer<TKey> comparer) {
         source.ThrowIfNull("source");
         selector.ThrowIfNull("selector");
         comparer.ThrowIfNull("comparer");
         using (IEnumerator<TSource> sourceIterator = source.GetEnumerator()) {
            if (!sourceIterator.MoveNext()) {
               throw new InvalidOperationException("Sequence was empty");
            }
            TSource min = sourceIterator.Current;
            TKey minKey = selector(min);
            while (sourceIterator.MoveNext()) {
               TSource candidate = sourceIterator.Current;
               TKey candidateProjected = selector(candidate);
               if (comparer.Compare(candidateProjected, minKey) < 0) {
                  min = candidate;
                  minKey = candidateProjected;
               }
            }
            return min;
         }
      }

      public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source,
         Func<TSource, TKey> selector) {
         return source.MaxBy(selector, Comparer<TKey>.Default);
      }

      public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source,
         Func<TSource, TKey> selector, IComparer<TKey> comparer) {
         source.ThrowIfNull("source");
         selector.ThrowIfNull("selector");
         comparer.ThrowIfNull("comparer");
         using (IEnumerator<TSource> sourceIterator = source.GetEnumerator()) {
            if (!sourceIterator.MoveNext()) {
               throw new InvalidOperationException("Sequence was empty");
            }
            TSource max = sourceIterator.Current;
            TKey maxKey = selector(max);
            while (sourceIterator.MoveNext()) {
               TSource candidate = sourceIterator.Current;
               TKey candidateProjected = selector(candidate);
               if (comparer.Compare(candidateProjected, maxKey) > 0) {
                  max = candidate;
                  maxKey = candidateProjected;
               }
            }
            return max;
         }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static V GetValueOrDefault<K, V>(this Dictionary<K, V> dict, K key) {
         return ((IDictionary<K, V>)dict).GetValueOrDefault(key);
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dict, K key) {
         V result;
         dict.TryGetValue(key, out result);
         return result;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static V GetValueOrDefault<K, V>(this IReadOnlyDictionary<K, V> dict, K key) {
         V result;
         dict.TryGetValue(key, out result);
         return result;
      }

      public static HashSet<T> ToHashSet<T>(this IEnumerable<T> e) => new HashSet<T>(e);

      public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> source, out TKey Key, out TValue Value) {
         Key = source.Key;
         Value = source.Value;
      }

      public static void Deconstruct<TKey, TValue>(this IGrouping<TKey, TValue> source, out TKey Key, out IEnumerable<TValue> Value) {
         Key = source.Key;
         Value = source;
      }

      public static KeyValuePair<TKey, TValue> PairValue<TKey, TValue>(this TKey key, TValue value) {
         return new KeyValuePair<TKey, TValue>(key, value);
      }

      public static KeyValuePair<TKey, TValue> PairKey<TKey, TValue>(this TValue value, TKey key) {
         return key.PairValue(value);
      }

      public static IEnumerable<T> RotateLeft<T>(this IEnumerable<T> e) {
         var it = e.GetEnumerator();
         if (!it.MoveNext()) {
            yield break;
         }
         var first = it.Current;
         while (it.MoveNext()) {
            yield return it.Current;
         }
         yield return first;
      }

      public static IEnumerable<Tuple<T, U>> Zip<T, U>(this IEnumerable<T> e1, IEnumerable<U> e2) => e1.Zip(e2, Tuple.Create);
   }
}