using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dargon.PlayOn.DataStructures {
   public class RemovalPermittingOrderedHashSet<T> : IEnumerable<T>, IReadOnlyList<T> {
      private readonly List<T> list = new List<T>();
      private readonly Dictionary<T, int> dict = new Dictionary<T, int>();

      public bool TryRemove(T item, out int removedIndex, out int replacementIndex) {
         if (dict.TryGetValue(item, out removedIndex)) {
            RemoveAt(removedIndex, out replacementIndex);
            return true;
         } else {
            // case 3: not found
            removedIndex = -1;
            replacementIndex = -1;
            return false;
         }
      }

      public int Count => list.Count;

      public bool IsReadOnly => true;

      public int IndexOf(T item) => dict.TryGetValue(item, out var i) ? i : -1;

      public void Insert(int index, T item) => throw new InvalidOperationException();

      public void RemoveAt(int removedIndex, out int replacementIndex) {
         var item = list[removedIndex];
         var lastIndex = list.Count - 1;

         if (removedIndex == lastIndex) {
            // case 1: end of list.
            dict.Remove(item);
            list.RemoveAt(lastIndex);
            replacementIndex = -1;
         } else {
            // case 2: replace start or middle of list with last element.
            var replacement = list[lastIndex];
            list.RemoveAt(lastIndex);
            list[removedIndex] = replacement;
            dict[replacement] = removedIndex;
            dict.Remove(item);
            replacementIndex = lastIndex;
         }
      }

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
