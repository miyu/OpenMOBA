using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Duplicated with OpenMoba's AddOnlyOrderedHashSet and ItzWarty.Commons
namespace Canvas3D.LowLevel.Helpers {
   public class AddOnlyOrderedHashSet<T> : IReadOnlyList<T> {
      private readonly ExposedArrayList<T> list = new ExposedArrayList<T>();
      private readonly Dictionary<T, int> dict = new Dictionary<T, int>();

      public ExposedArrayList<T> List => list;
      public int Count => list.Count;
      public T this[int idx] => list[idx];

      public int TryAdd(T val) {
         TryAdd(val, out int x);
         return x;
      }

      public bool TryAdd(T val, out int index) {
         if (dict.TryGetValue(val, out index)) return false;
         dict[val] = index = list.Count;
         list.Add(val);
         return true;
      }

      public ExposedArrayList<T>.Enumerator GetEnumerator() => list.GetEnumerator();
      IEnumerator<T> IEnumerable<T>.GetEnumerator() => list.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public void Clear() {
         list.Clear();
         dict.Clear();
      }
   }
}
