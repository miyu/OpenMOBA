using System;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.ECS {
   public struct ForceContribution {
      public DoubleVector2 SumForces;
      public Double SumWeights;
   }
}