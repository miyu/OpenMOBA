using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenMOBA.DataStructures
{
   /// <summary>
   /// Minheap-based PQ supporting removal.
   /// Items must be class instances, does not support null items. Equality
   /// is reference-based, instances' hashcodes must not change after insertion.
   /// 
   /// log(N) deletion, does not support inserting an instance more than once.
   /// </summary>
   public class NonTrackingRemovablePriorityQueue<TItem> : IReadOnlyCollection<TItem> {
      // 1 + 4 + 16 + 64 = 5 + 16 + 64 = 21 + 64 = 85
      private const int kNodesPerLevel = 4;
      private const int kInitialCapacity = 1 + kNodesPerLevel;
      private readonly Comparison<TItem> itemComparer;
      private static readonly IEqualityComparer<TItem> equalityComparer = typeof(TItem).IsValueType ? EqualityComparer<TItem>.Default : ReferenceEqualityComparer<TItem>.Instance;

      private TItem[] items = new TItem[kInitialCapacity];

      public NonTrackingRemovablePriorityQueue() : this(Comparer<TItem>.Default.Compare) { }

      public NonTrackingRemovablePriorityQueue(Comparison<TItem> itemComparer) {
         this.itemComparer = itemComparer;
      }

      public int Capacity => items.Length;
      public bool IsEmpty => Count == 0;

      public int Count { get; private set; }

      public IEnumerator<TItem> GetEnumerator() {
         var clone = new NonTrackingRemovablePriorityQueue<TItem>(itemComparer);
         clone.items = new TItem[items.Length];
         for (int i = 0; i < Count; i++) {
            clone.items[i] = items[i];
         }
         clone.Count = Count;
         while (!clone.IsEmpty) {
            yield return clone.Dequeue();
         }
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public TItem Peek() {
         if (Count == 0) {
            throw new InvalidOperationException("The queue is empty");
         }
         return items[0];
      }

      public TItem Dequeue() {
         if (Count == 0) {
            throw new InvalidOperationException("The queue is empty");
         }

         return RemoveAtIndex(0);
      }

      private void PercolateDown(int currentIndex, TItem item) {
         int childrenStartIndexInclusive, childrenEndIndexExclusive;
         ComputeChildrenIndices(currentIndex, out childrenStartIndexInclusive, out childrenEndIndexExclusive);

         // handle childless case
         if (childrenStartIndexInclusive == childrenEndIndexExclusive) {
            items[currentIndex] = item;
            return;
         }

         // select least child for replacement
         var leastChildIndex = childrenStartIndexInclusive;
         for (var i = leastChildIndex + 1; i < childrenEndIndexExclusive; i++) {
            if (itemComparer(items[i], items[leastChildIndex]) < 0) {
               leastChildIndex = i;
            }
         }

         if (itemComparer(items[leastChildIndex], item) < 0) {
            // Our least child is smaller than item, move it up the heap and percolate further.
            items[currentIndex] = items[leastChildIndex];
            PercolateDown(leastChildIndex, item);
         } else {
            // Our item is greater than its descendents, store.
            items[currentIndex] = item;
         }
      }

      public void Enqueue(TItem item) {
         EnsureCapacity(Count + 1);

         PercolateUp(Count, item);
         Count++;
      }

      private void PercolateUp(int currentIndex, TItem item) {
         if (currentIndex == 0) {
            items[0] = item;
            return;
         }

         var parentIndex = (currentIndex - 1) / kNodesPerLevel;
         if (itemComparer(item, items[parentIndex]) < 0) {
            items[currentIndex] = items[parentIndex];
            PercolateUp(parentIndex, item);
         } else {
            items[currentIndex] = item;
         }
      }

      private void EnsureCapacity(int desiredCapacity) {
         if (items.Length < desiredCapacity) {
            var newCapacity = items.Length;
            while (newCapacity < desiredCapacity) {
               newCapacity = newCapacity * kNodesPerLevel + 1;
            }

            var newPriorities = new TItem[newCapacity];
            for (var i = 0; i < items.Length; i++) {
               newPriorities[i] = items[i];
            }
            items = newPriorities;
         }
      }

      public bool Remove(TItem item) {
         for (var i = 0; i < Count; i++) {
            if (equalityComparer.Equals(item, items[i])) {
               RemoveAtIndex(i);
               return true;
            }
         }
         return false;
      }

      private TItem RemoveAtIndex(int i) {
         var result = items[i];

         Count--;
         var tail = items[Count];
         items[Count] = default(TItem);

         PercolateDown(i, tail);
         return result;
      }

      private void ComputeChildrenIndices(int currentIndex, out int childrenStartIndexInclusive, out int childrenEndIndexExclusive) {
         childrenStartIndexInclusive = Math.Min(Count, currentIndex * kNodesPerLevel + 1);
         childrenEndIndexExclusive = Math.Min(Count, currentIndex * kNodesPerLevel + kNodesPerLevel + 1);
      }

      private class ReferenceEqualityComparer<T> : IEqualityComparer<T>
      {
         public static IEqualityComparer<T> Instance { get; } = new ReferenceEqualityComparer<T>();

         public bool Equals(T x, T y)
         {
            return ReferenceEquals(x, y);
         }

         public int GetHashCode(T obj)
         {
            return obj.GetHashCode();
         }
      }
   }
}