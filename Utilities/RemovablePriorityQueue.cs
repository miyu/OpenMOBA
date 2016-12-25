using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenMOBA.Utilities
{
   /// <summary>
   /// Because no other implementation that doesn't suck exists.
   /// Seriously, C#?
   /// Traditional minheap-based pq. Allows duplicate entry,
   /// supports resizing. Unlike PriorityQueue, supports deletion
   /// at the cost of log(N) hashmap operations per insert,
   /// log(N) hashmap operations (and a percolateDown) per deletion.
   /// Items are thrown into a hashmap and compared via ReferenceEquals.
   /// </summary>
   public class RemovablePriorityQueue<TItem> : IReadOnlyCollection<TItem> {
      // 1 + 4 + 16 + 64 = 5 + 16 + 64 = 21 + 64 = 85
      private const int kNodesPerLevel = 4;
      private const int kInitialCapacity = 1 + kNodesPerLevel;
      private readonly Comparison<TItem> itemComparer;

      private TItem[] items = new TItem[kInitialCapacity];
      private Dictionary<TItem, int> itemIndices = new Dictionary<TItem, int>(ReferenceEqualityComparer<TItem>.Instance);
      private int version;

      public RemovablePriorityQueue() : this(Comparer<TItem>.Default.Compare) { }

      public RemovablePriorityQueue(Comparison<TItem> itemComparer) {
         this.itemComparer = itemComparer;
      }

      public int Capacity => items.Length;
      public bool IsEmpty => Count == 0;

      public int Count { get; private set; }

      public IEnumerator<TItem> GetEnumerator() {
         var versionCapture = version;
         for (var i = 0; i < Count; i++) {
            if (version != versionCapture) {
               throw new InvalidOperationException("PriorityQueue modified during iteration.");
            }
            yield return items[i];
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

         var result = items[0];

         Count--;
         var tail = items[Count];
         items[Count] = default(TItem);

         PercolateDown(0, tail);
         return result;
      }

      private void PercolateDown(int currentIndex, TItem item) {
         var childrenStartIndexInclusive = Math.Min(Count, currentIndex * kNodesPerLevel + 1);
         var childrenEndIndexExclusive = Math.Min(Count, currentIndex * kNodesPerLevel + kNodesPerLevel);

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
         version++;
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