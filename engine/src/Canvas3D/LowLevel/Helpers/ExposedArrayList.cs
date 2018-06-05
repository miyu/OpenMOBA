using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas3D.LowLevel.Helpers {
   // Doesn't zero free/cleared slots, exposes internal storage.
   public class ExposedArrayList<T> : IList<T> {
      public int size = 0;
      public int version = 0;
      public T[] store;

      public ExposedArrayList(int capacity = 16) {
         store = new T[capacity];
      }

      public ExposedArrayList<T>.Enumerator GetEnumerator() => new ExposedArrayList<T>.Enumerator(this);

      IEnumerator<T> IEnumerable<T>.GetEnumerator() => store.Take(size).GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public void Add(T item) {
         EnsureCapacity(size + 1);
         store[size++] = item;
         version++;
      }

      public void Add(ref T item) {
         EnsureCapacity(size + 1);
         store[size++] = item;
         version++;
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
            version++;
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

      // basically copied from bcl
      public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator {
         private ExposedArrayList<T> list;
         private int index;
         private int version;
         private T current;

         public T Current => this.current;

         object IEnumerator.Current
         {
            get
            {
               if (this.index == 0 || this.index == this.list.Count + 1)
                  throw new IndexOutOfRangeException();
               return (object)this.Current;
            }
         }

         internal Enumerator(ExposedArrayList<T> list) {
            this.list = list;
            this.index = 0;
            this.version = list.version;
            this.current = default(T);
         }

         public void Dispose() {
         }

         public bool MoveNext() {
            if (this.version != list.version || (uint)this.index >= (uint)list.Count)
               return this.MoveNextRare();
            this.current = list.store[this.index];
            this.index = this.index + 1;
            return true;
         }

         private bool MoveNextRare() {
            if (this.version != this.list.version)
               throw new InvalidOperationException("Exposed Array List modified while reading.");
            this.index = this.list.Count + 1;
            this.current = default(T);
            return false;
         }

         void IEnumerator.Reset() {
            if (this.version != this.list.version)
               throw new InvalidOperationException("Exposed Array List modified while reading.");
            this.index = 0;
            this.current = default(T);
         }
      }
   }
}
