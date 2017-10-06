using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenMOBA.DataStructures {
   public class AddOnlyOrderedHashSet<T> : IReadOnlyList<T> {
      private readonly List<T> list = new List<T>();
      private readonly Dictionary<T, int> set = new Dictionary<T, int>();

      public int Count => list.Count;
      public T this[int idx] => list[idx];

      public bool TryAdd(T val, out int index) {
         if (set.TryGetValue(val, out index)) return false;
         set[val] = list.Count;
         list.Add(val);
         return true;
      }

      public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}
