using System;
using System.Collections;
using System.Collections.Generic;

namespace Dargon.Luna.Transpiler {
   public class AddOnlyOrderedHashSet<T> : IList<T>, IReadOnlyList<T> {
      private readonly List<T> list = new List<T>();
      private readonly Dictionary<T, int> dict = new Dictionary<T, int>();

      public bool Remove(T item) => throw new InvalidOperationException();

      public int Count => list.Count;

      public bool IsReadOnly => true;

      public int IndexOf(T item) => dict.TryGetValue(item, out var i) ? i : -1;

      public void Insert(int index, T item) => throw new InvalidOperationException();

      public void RemoveAt(int index) => throw new InvalidOperationException();

      public T this[int idx] { get => list[idx]; set => throw new NotImplementedException(); }

      public bool TryAdd(T val, out int index) {
         if (dict.TryGetValue(val, out index)) return false;
         dict[val] = index = list.Count;
         list.Add(val);
         return true;
      }

      public void Add(T item) {
         TryAdd(item, out _);
      }

      public void Clear() {
         list.Clear();
         dict.Clear();
      }

      public bool Contains(T item) => dict.ContainsKey(item);

      public void CopyTo(T[] array, int arrayIndex) {
         list.CopyTo(array, arrayIndex);
      }

      public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public T[] ToArray() => list.ToArray();
      public List<T> ToList() => new List<T>(list);
   }
}
