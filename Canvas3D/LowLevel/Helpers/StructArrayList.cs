using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas3D.LowLevel.Helpers {
   public class StructArrayList<T> : IList<T> where T : struct {
      public int size = 0;
      public T[] store;

      public StructArrayList(int capacity = 16) {
         store = new T[capacity];
      }

      public IEnumerator<T> GetEnumerator() => store.Take(size).GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public void Add(T item) {
         EnsureCapacity(size + 1);
         store[size++] = item;
      }

      public void Add(ref T item) {
         EnsureCapacity(size + 1);
         store[size++] = item;
      }

      private void EnsureCapacity(int sz) {
         if (sz > store.Length) {
            var capacity = store.Length;
            while (capacity < sz) {
               capacity <<= 1;
            }
            var buff = new T[capacity];
            Array.Copy(store, 0, buff, 0, size);
            store = buff;
         }
      }

      public void Clear() => size = 0;

      public bool Contains(T item) => throw new NotSupportedException();
      public void CopyTo(T[] array, int arrayIndex) {
         Buffer.BlockCopy(store, 0, array, arrayIndex, array.Length - arrayIndex);
      }

      public bool Remove(T item) => throw new NotSupportedException();

      public int Count => size;
      public bool IsReadOnly => false;

      public int IndexOf(T item) => throw new NotSupportedException();

      public void Insert(int index, T item) => throw new NotSupportedException();

      public void RemoveAt(int index) => throw new NotSupportedException();

      public T this[int index] {
         get {
            if (index < 0 || index >= size) {
               throw new ArgumentOutOfRangeException();
            }
            return store[index];
         }
         set => throw new NotSupportedException();
      }
   }
}
