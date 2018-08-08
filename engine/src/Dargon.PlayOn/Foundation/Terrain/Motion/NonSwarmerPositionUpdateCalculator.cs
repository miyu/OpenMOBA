using System;
using System.Diagnostics;
using System.Numerics;
using Dargon.PlayOn.Foundation.Terrain.Pathfinding;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.ECS {
   public class NonSwarmerPositionUpdateCalculator {
      public (MotionPose nextPose, int nextRoadmapProgressIndex) CalculateRoadmapPositionUpdate(MotionPose pose, MotionRoadmap roadmap, int roadmapProgressIndex, Double worldDistanceRemaining) {
         var plan = roadmap.Plan;
         while (worldDistanceRemaining > CDoubleMath.c0 && roadmapProgressIndex < plan.Count) {
            var action = plan[roadmapProgressIndex];
            switch (action) {
               case MotionRoadmapWalkAction wa:
                  (pose, roadmapProgressIndex, worldDistanceRemaining) = CalculateWalkActionPositionUpdate(pose, roadmapProgressIndex, worldDistanceRemaining, wa);
                  break;
               default:
                  throw new NotImplementedException();
            }
         }
         return (pose, roadmapProgressIndex);
      }

      private static (MotionPose pose, int roadmapProgressIndex, Double nextWorldDistanceRemaining) CalculateWalkActionPositionUpdate(MotionPose pose, int roadmapProgressIndex, Double worldDistanceRemaining, MotionRoadmapWalkAction motionRoadmapWalkAction) {
         var currentSectorLocalPositionDotNet = Vector3.Transform(pose.WorldPosition.ToDotNetVector(), motionRoadmapWalkAction.Node.SectorNodeDescription.WorldTransformInv).ToOpenMobaVector();
         var currentSectorLocalPosition = new DoubleVector2(currentSectorLocalPositionDotNet.X, currentSectorLocalPositionDotNet.Y);
         Trace.Assert(CDoubleMath.Abs((Double)currentSectorLocalPositionDotNet.Z) < (Double)1E-3);

         // vect from position to next pathing breadcrumb (in local space)
         // todo: set lookat
         var pb = currentSectorLocalPosition.To(motionRoadmapWalkAction.Destination.ToDoubleVector2());

         // |pb| - distance to next pathing breadcrumb
         var localDistance = pb.Norm2D();
         var worldDistance = localDistance * motionRoadmapWalkAction.Node.SectorNodeDescription.LocalToWorldScalingFactor;

         DoubleVector2 nextSectorLocalPosition;
         if (worldDistance <= CDoubleMath.Epsilon || worldDistance <= worldDistanceRemaining) {
            nextSectorLocalPosition = motionRoadmapWalkAction.Destination.ToDoubleVector2();
            roadmapProgressIndex++;
            worldDistanceRemaining -= worldDistance;
         } else {
            nextSectorLocalPosition = currentSectorLocalPosition + pb * worldDistanceRemaining / worldDistance;
            worldDistanceRemaining = CDoubleMath.c0;
         }

         pose.WorldPosition = Vector3.Transform(
            new Vector3(nextSectorLocalPosition.ToDotNetVector(), 0),
            motionRoadmapWalkAction.Node.SectorNodeDescription.WorldTransform).ToOpenMobaVector();

         return (pose, roadmapProgressIndex, worldDistanceRemaining);
      }
   }
}