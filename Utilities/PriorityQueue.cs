using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenMOBA.Utilities {
   /// <summary>
   /// Because no other implementation that doesn't suck exists.
   /// Seriously, C#?
   /// 
   /// Traditional minheap-based pq. Allows duplicate entry,
   /// supports resizing.
   /// </summary>
   public class PriorityQueue<TItem> : IReadOnlyCollection<TItem> {
      // 1 + 4 + 16 + 64 = 5 + 16 + 64 = 21 + 64 = 85
      private const int kNodesPerLevel = 4;
      private const int kInitialCapacity = 1 + kNodesPerLevel;

      private TItem[] items = new TItem[kInitialCapacity];
      private readonly Comparison<TItem> itemComparer;
      private int size;
      private int version;

      public PriorityQueue() : this(Comparer<TItem>.Default.Compare) { }

      public PriorityQueue(Comparison<TItem> itemComparer) {
         this.itemComparer = itemComparer;
      }

      public int Count => size;
      public int Capacity => items.Length;
      public bool IsEmpty => Count == 0;

      public TItem Peek() {
         if (size == 0) {
            throw new InvalidOperationException("The queue is empty");
         }
         return items[0];
      }

      public TItem Dequeue() {
         if (size == 0) {
            throw new InvalidOperationException("The queue is empty");
         }

         var result = items[0];

         size--;
         var tail = items[size];
         items[size] = default(TItem);

         PercolateDown(0, tail);
         return result;
      }

      private void PercolateDown(int currentIndex, TItem item) {
         var childrenStartIndexInclusive = Math.Min(size, currentIndex * kNodesPerLevel + 1);
         var childrenEndIndexExclusive = Math.Min(size, currentIndex * kNodesPerLevel + kNodesPerLevel);
         
         // handle childless case
         if (childrenStartIndexInclusive == childrenEndIndexExclusive) {
            items[currentIndex] = item;
            return;
         }

         // select least child for replacement
         var leastChildIndex = childrenStartIndexInclusive;
         for (int i = leastChildIndex + 1; i < childrenEndIndexExclusive; i++) {
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
         EnsureCapacity(size + 1);

         PercolateUp(size, item);
         size++;
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
            int newCapacity = items.Length;
            while (newCapacity < desiredCapacity) {
               newCapacity = newCapacity * kNodesPerLevel + 1;
            }

            var newPriorities = new TItem[newCapacity];
            for (int i = 0; i < items.Length; i++) {
               newPriorities[i] = items[i];
            }
            items = newPriorities;
         }
      }

      public IEnumerator<TItem> GetEnumerator() {
         var versionCapture = version;
         for (int i = 0; i < size; i++) {
            if (version != versionCapture) {
               throw new InvalidOperationException("PriorityQueue modified during iteration.");
            }
            yield return items[i];
         }
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}
