using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Dargon.PlayOn.DataStructures;

namespace Dargon.PlayOn {
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

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static U[] Map<T, U>(this IReadOnlyList<T> arr, Func<T, U> map) {
         var result = new U[arr.Count];
         for (int i = 0; i < arr.Count; i++) {
            result[i] = map(arr[i]);
         }
         return result;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static U[] Map<T, U>(this IReadOnlyList<T> arr, Func<T, int, U> map) {
         var result = new U[arr.Count];
         for (int i = 0; i < arr.Count; i++) {
            result[i] = map(arr[i], i);
         }
         return result;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static U[] Map<T, U>(this T[] arr, Func<T, U> map) {
         var result = new U[arr.Length];
         for (int i = 0; i < arr.Length; i++) {
            result[i] = map(arr[i]);
         }
         return result;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static U[] Map<T, U>(this T[] arr, Func<T, int, U> map) {
         var result = new U[arr.Length];
         for (int i = 0; i < arr.Length; i++) {
            result[i] = map(arr[i], i);
         }
         return result;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Dictionary<K, R> Map<K, V, R>(this IReadOnlyDictionary<K, V> dict, Func<K, V, R> map) {
         if (dict is Dictionary<K, V> d) {
            return d.Map(map);
         }
         return dict.ToDictionary(kvp => kvp.Key, kvp => map(kvp.Key, kvp.Value));
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

      public static MultiValueDictionary<K, V> ToMultiValueDictionary<I, K, V>(this IReadOnlyList<I> coll, Func<I, K> keyMapper, Func<I, V> valueMapper) {
         var dict = new MultiValueDictionary<K, V>();
         for (var i = 0; i < coll.Count; i++) {
            var x = coll[i];
            dict.Add(keyMapper(x), valueMapper(x));
         }
         return dict;
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

      public static void Resize<T>(this List<T> list, int size) {
         if (size < list.Count) {
            list.RemoveRange(size, list.Count - size);
         } else if (size > list.Count) {
            list.AddRange(new T[size - list.Count]);
         }
      }

      //      public static Dictionary<K, V1> Map<K, V1, V2>(this Dictionary<K, V1> dict, Func<V1, V2> mapper) {
      //         var result = new Dictionary<K, V2>();
      //         return result;
      //      }
   }

   public static class DictionaryMapperExtensions {
      public static Dictionary<K, V2> Map<K, V1, V2>(this Dictionary<K, V1> dict, Func<K, V1, V2> mapper) {
         return DictionaryMapperInternals<K, V1, V2>.dictionaryMapper(dict, mapper);
      }

      public static Dictionary<VIn, VOut> MapByValue<KIn, VIn, VOut>(this Dictionary<KIn, VIn> dict, Func<VIn, VOut> mapper) {
         var result = new Dictionary<VIn, VOut>(dict.Count);
         foreach (var (k, v) in dict) {
            result[v] = mapper(v);
         }
         return result;
      }

      public static Dictionary<T, V> UniqueMap<T, V>(this IReadOnlyList<T> list, Func<T, V> mapper) {
         var result = new Dictionary<T, V>();
         for (var i = 0 ; i < list.Count; i++) {
            var x = list[i];
            result[x] = mapper(x);
         }
         return result;
      }
   }

   public static class DictionaryMapperInternals<K, V1, V2> {
      public delegate Dictionary<K, V2> DictionaryMapFunc(Dictionary<K, V1> dict, Func<K, V1, V2> mapper);

      public static readonly DictionaryMapFunc dictionaryMapper;

      static DictionaryMapperInternals() {
         FieldInfo FindField(Type t, string name) => t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
         PropertyInfo FindProperty(Type t, string name) => t.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

         var tInputDict = typeof(Dictionary<K, V1>);
         var tInputEntry = tInputDict.GetNestedType("Entry", BindingFlags.NonPublic).MakeGenericType(typeof(K), typeof(V1));
         var tOutputDict = typeof(Dictionary<K, V2>);
         var tOutputEntry = tOutputDict.GetNestedType("Entry", BindingFlags.NonPublic).MakeGenericType(typeof(K), typeof(V2));
         var tMapper = typeof(Func<K, V1, V2>);

         var method = new DynamicMethod("", tOutputDict, new[] { tInputDict, tMapper }, true);
         var emitter = method.GetILGenerator();
         var outputDictLocal = emitter.DeclareLocal(tOutputDict);
         var inputDictEntriesLocal = emitter.DeclareLocal(tInputEntry.MakeArrayType());
         var outputDictEntriesLocal = emitter.DeclareLocal(tOutputEntry.MakeArrayType());
         var iLocal = emitter.DeclareLocal(typeof(int));

         //-------------------------------------------------------------------------------------
         // alloc output dict, store to loc 0 (this allocs w/ capacity 0, null internal store)
         // the reason we don't call ctor with size overhead is that runs a zeroing init loop.
         //-------------------------------------------------------------------------------------
         var tOutputDictConstructor = tOutputDict.GetConstructors().First(c => c.GetParameters().Length == 0);
         emitter.Emit(OpCodes.Newobj, tOutputDictConstructor);
         emitter.Emit(OpCodes.Stloc, outputDictLocal);

         emitter.Emit(OpCodes.Ldarg_0);
         emitter.Emit(OpCodes.Call, FindProperty(tInputDict, "Count").GetMethod);
         emitter.Emit(OpCodes.Ldc_I4_0);
         emitter.Emit(OpCodes.Ceq);
         var exitLabel = emitter.DefineLabel();
         emitter.Emit(OpCodes.Brtrue, exitLabel);

         //-------------------------------------------------------------------------------------
         // clone buckets (int[]), store into output dict
         //-------------------------------------------------------------------------------------
         emitter.Emit(OpCodes.Ldloc, outputDictLocal); // push this onto stack, used later in store.

         // load buckets
         emitter.Emit(OpCodes.Ldarg_0);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputDict, "buckets"));

         // clone
         var cloneIntArrayOrNull = typeof(DictionaryMapperInternals<K, V1, V2>).GetMethod(nameof(CloneIntArrayOrNull), BindingFlags.NonPublic | BindingFlags.Static);
         emitter.Emit(OpCodes.Call, cloneIntArrayOrNull);

         // store
         emitter.Emit(OpCodes.Stfld, FindField(tOutputDict, "buckets"));

         //-------------------------------------------------------------------------------------
         // alloc clone of Dictionary<TKey, TValue>.Entry[] entries, store into output dict
         //-------------------------------------------------------------------------------------
         // find input dict entries local
         emitter.Emit(OpCodes.Ldarg_0);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputDict, "entries"));
         emitter.Emit(OpCodes.Stloc, inputDictEntriesLocal);

         // allocate output dict entries local
         emitter.Emit(OpCodes.Ldloc, outputDictLocal); // push this onto stack, used later in store.
         emitter.Emit(OpCodes.Ldloc, inputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldlen);
         emitter.Emit(OpCodes.Conv_I4);
         emitter.Emit(OpCodes.Newarr, tOutputEntry);
         emitter.Emit(OpCodes.Stloc, outputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldloc, outputDictEntriesLocal);
         emitter.Emit(OpCodes.Stfld, FindField(tOutputDict, "entries"));

         // init i = 0
         var forLoopConditional = emitter.DefineLabel();
         var forLoopBody = emitter.DefineLabel();
         var forLoopIncrementor = emitter.DefineLabel();
         emitter.Emit(OpCodes.Ldc_I4_0);
         emitter.Emit(OpCodes.Stloc, iLocal);

         // jump to conditional of for loop (the bounds check)
         emitter.Emit(OpCodes.Br, forLoopConditional);

         // for loop body
         emitter.MarkLabel(forLoopBody);

         // Load TOutDict.Entry* 4x on stack
         emitter.Emit(OpCodes.Ldloc, outputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldelema, tOutputEntry);
         emitter.Emit(OpCodes.Dup);
         emitter.Emit(OpCodes.Dup);
         emitter.Emit(OpCodes.Dup);

         // copy hashcode
         emitter.Emit(OpCodes.Ldloc, inputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldelema, tInputEntry);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputEntry, "hashCode"));
         emitter.Emit(OpCodes.Stfld, FindField(tOutputEntry, "hashCode"));

         // copy next
         emitter.Emit(OpCodes.Ldloc, inputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldelema, tInputEntry);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputEntry, "next"));
         emitter.Emit(OpCodes.Stfld, FindField(tOutputEntry, "next"));

         // copy key
         emitter.Emit(OpCodes.Ldloc, inputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldelema, tInputEntry);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputEntry, "key"));
         emitter.Emit(OpCodes.Stfld, FindField(tOutputEntry, "key"));


         //-------------------------------------------------------------------------------------
         // map (key, value) => new value if i < dict.count && entries[i].hashCode >= 0
         //-------------------------------------------------------------------------------------
         var cleanupAndJumpToIncrementor = emitter.DefineLabel();

         // verify mapping conditions
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldarg_0);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputDict, "count"));
         emitter.Emit(OpCodes.Clt);
         emitter.Emit(OpCodes.Brfalse, cleanupAndJumpToIncrementor);

         // dup TOutputDict.Entry*, then get ith element hashCode field
         emitter.Emit(OpCodes.Dup);
         emitter.Emit(OpCodes.Ldfld, FindField(tOutputEntry, "hashCode"));
         emitter.Emit(OpCodes.Ldc_I4_0);
         emitter.Emit(OpCodes.Clt);
         emitter.Emit(OpCodes.Brtrue, cleanupAndJumpToIncrementor);

         // the actual mapping code
         emitter.Emit(OpCodes.Ldarg_1);

         emitter.Emit(OpCodes.Ldloc, inputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldelema, tInputEntry);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputEntry, "key"));

         emitter.Emit(OpCodes.Ldloc, inputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldelema, tInputEntry);
         emitter.Emit(OpCodes.Ldfld, FindField(tInputEntry, "value"));

         var invokeMethod = tMapper.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
         emitter.Emit(OpCodes.Callvirt, invokeMethod);
         emitter.Emit(OpCodes.Stfld, FindField(tOutputEntry, "value"));

         emitter.Emit(OpCodes.Br, forLoopIncrementor);

         // cleanup if decided to skip map value
         emitter.MarkLabel(cleanupAndJumpToIncrementor);
         emitter.Emit(OpCodes.Pop);

         // i++
         emitter.MarkLabel(forLoopIncrementor);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldc_I4_1);
         emitter.Emit(OpCodes.Add);
         emitter.Emit(OpCodes.Stloc, iLocal);

         // for loop conditional
         emitter.MarkLabel(forLoopConditional);
         emitter.Emit(OpCodes.Ldloc, iLocal);
         emitter.Emit(OpCodes.Ldloc, inputDictEntriesLocal);
         emitter.Emit(OpCodes.Ldlen);
         emitter.Emit(OpCodes.Conv_I4);
         emitter.Emit(OpCodes.Clt);
         emitter.Emit(OpCodes.Brtrue, forLoopBody);

         //-------------------------------------------------------------------------------------
         // clone count, version, freeList, freeCount
         //-------------------------------------------------------------------------------------
         foreach (var fieldName in new[] { "count", "version", "freeList", "freeCount", "comparer" }) {
            emitter.Emit(OpCodes.Ldloc, outputDictLocal);
            emitter.Emit(OpCodes.Ldarg_0);
            emitter.Emit(OpCodes.Ldfld, FindField(tInputDict, fieldName));
            emitter.Emit(OpCodes.Stfld, FindField(tOutputDict, fieldName));
         }

         emitter.MarkLabel(exitLabel);
         emitter.Emit(OpCodes.Ldloc, outputDictLocal);
         emitter.Emit(OpCodes.Ret);

         dictionaryMapper = (DictionaryMapFunc)method.CreateDelegate(typeof(DictionaryMapFunc));
      }

      private static int[] CloneIntArrayOrNull(int[] input) {
         if (input == null) return null;
         var result = new int[input.Length];
         Buffer.BlockCopy(input, 0, result, 0, input.Length * sizeof(int));
         return result;
      }
   }
}
 