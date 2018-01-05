using System.Collections;
using System.Collections.Generic;

// Duplicated with OpenMoba's AddOnlyOrderedHashSet and ItzWarty.Commons
namespace Canvas3D.LowLevel.Helpers {
   public class AddOnlyOrderedHashSet<T> : IReadOnlyList<T> {
      private readonly ExposedArrayList<T> list = new ExposedArrayList<T>();
      private readonly Dictionary<T, int> dict = new Dictionary<T, int>();

      public ExposedArrayList<T> List => list;
      public int Count => list.Count;
      public T this[int idx] => list[idx];

      public bool TryAdd(T val, out int index) {
         if (dict.TryGetValue(val, out index)) return false;
         dict[val] = index = list.Count;
         list.Add(val);
         return true;
      }

      public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public void Clear() {
         list.Clear();
         dict.Clear();
      }
   }
}
