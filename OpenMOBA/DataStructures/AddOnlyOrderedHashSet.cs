using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenMOBA.DataStructures {
   public class AddOnlyOrderedHashSet<T> : IEnumerable<T> {
      private readonly List<T> list = new List<T>();
      private readonly HashSet<T> set = new HashSet<T>();

      public int Count => list.Count;
      public T this[int idx] => list[idx];

      public bool Add(T val) {
         if (!set.Add(val)) return false;
         list.Add(val);
         return true;
      }

      public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}
