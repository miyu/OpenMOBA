using System.Collections.Generic;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Foundation.Terrain.CompilationResults.Local {
   public class LocalGeometryViewManager {
      public readonly LocalGeometryJob Job;
      public readonly LocalGeometryViewManager PreviewViewManager;
      private readonly Dictionary<cDouble, LocalGeometryView> views = new Dictionary<cDouble, LocalGeometryView>();

      public LocalGeometryViewManager(LocalGeometryJob job, LocalGeometryViewManager previewViewManager = null) {
         Job = job;
         PreviewViewManager = previewViewManager ?? this;
      }

      public void InvalidateCaches() {
         views.Clear();
      }

      public LocalGeometryView GetErodedView(cDouble actorRadius) {
         if (views.TryGetValue(actorRadius, out LocalGeometryView cachedView)) return cachedView;
         var preview = PreviewViewManager == this ? null : PreviewViewManager.GetErodedView(actorRadius);
         return views[actorRadius] = new LocalGeometryView(this, actorRadius, preview);
      }
   }
}