using System.Collections.Generic;

namespace OpenMOBA.Foundation.Terrain.Snapshots {
   public class LocalGeometryViewManager {
      public readonly LocalGeometryJob Job;
      public readonly LocalGeometryViewManager PreviewViewManager;
      private readonly Dictionary<double, LocalGeometryView> views = new Dictionary<double, LocalGeometryView>();

      public LocalGeometryViewManager(LocalGeometryJob job, LocalGeometryViewManager previewViewManager = null) {
         Job = job;
         PreviewViewManager = previewViewManager ?? this;
      }

      public void InvalidateCaches() {
         views.Clear();
      }

      public LocalGeometryView GetErodedView(double actorRadius) {
         if (views.TryGetValue(actorRadius, out LocalGeometryView cachedView)) return cachedView;
         var preview = PreviewViewManager == this ? null : PreviewViewManager.GetErodedView(actorRadius);
         return views[actorRadius] = new LocalGeometryView(this, actorRadius, preview);
      }
   }
}