using System;
using System.Collections.Generic;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.PlayOn.DataStructures;

namespace Dargon.PlayOn.Foundation.ECS.Utils {
   public class AssociatedStateContainer<T> {
      private readonly ExposedArrayList<T> items = new ExposedArrayList<T>();
      private readonly Func<T> itemFactory;

      public AssociatedStateContainer(Func<T> itemFactory = null) {
         this.itemFactory = itemFactory ?? (() => default);
      }

      public void Add(Entity entity) {
         items.Add(itemFactory());
      }

      public void Remove(Entity entity, int removedIndex, int replacementIndex) {
         var lastIndex = items.Count - 1;
         Assert.Equals(lastIndex, replacementIndex);

         items[removedIndex] = items[lastIndex];
         items.RemoveAt(lastIndex);
      }

      public ExposedArrayList<T> Items => items;
   }
}
