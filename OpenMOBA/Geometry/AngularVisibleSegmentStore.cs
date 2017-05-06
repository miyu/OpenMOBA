using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenMOBA.Geometry {
   // Everything's in radians, all geometry is reasoned as if relative to _origin.
   // This can be efficiently implemented with an interval tree = O(logN) runtime.
   // For now, just using a flat unordered list, so quite inefficient.
   public class AngularVisibleSegmentStore {
      private const double TwoPi = 2.0 * Math.PI;
      private const double PiDiv2 = Math.PI / 2.0;
      private readonly DoubleVector2 _origin;
      private List<IntervalRange> _intervalRanges;

      public AngularVisibleSegmentStore(DoubleVector2 origin) {
         _origin = origin;
         _intervalRanges = new List<IntervalRange> {
            new IntervalRange {
               Segment = null,
               ThetaStart = 0.0,
               ThetaEnd = TwoPi
            }
         };
      }

      public void Insert(IntLineSegment3 s) {
         var p1 = s.First.XY.ToDoubleVector2();
         var p2 = s.Second.XY.ToDoubleVector2();

         var theta1 = FindRadiansRelativeToOrigin(p1);
         var theta2 = FindRadiansRelativeToOrigin(p2);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;
         
         if (Math.Abs(theta1 - theta2) > Math.PI) {
            // covered angle range wraps around through theta=0.
            InsertInternal(s, 0.0, thetaLower);
            InsertInternal(s, thetaUpper, TwoPi);
         } else {
            InsertInternal(s, thetaLower, thetaUpper);
         }
      }

      private void InsertInternal(IntLineSegment3 s, double thetaLower, double thetaUpper) {
         Console.WriteLine($"InsertInternal: {s}, {thetaLower} {thetaUpper}");
         var sxy = new IntLineSegment2(s.First.XY, s.Second.XY);
         var srange = new IntervalRange { ThetaStart = thetaLower, ThetaEnd = thetaUpper, Segment = s };

         // where farRange contains the angle range we need to cover
         IEnumerable<IntervalRange> HandleNearFarSplit(IntervalRange nearRange, IntervalRange farRange) {
            // case: near contains far
            if (nearRange.ThetaStart <= farRange.ThetaStart && farRange.ThetaEnd <= nearRange.ThetaEnd) {
               return new[] { new IntervalRange { ThetaStart = farRange.ThetaStart, ThetaEnd = farRange.ThetaEnd, Segment = nearRange.Segment } };
            }

            // case: near angles exclusively within far angles
            if (farRange.ThetaStart < nearRange.ThetaStart && nearRange.ThetaEnd < farRange.ThetaEnd) {
               return new[] {
                  new IntervalRange { ThetaStart = farRange.ThetaStart, ThetaEnd = nearRange.ThetaStart, Segment = farRange.Segment},
                  new IntervalRange { ThetaStart = nearRange.ThetaStart, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
                  new IntervalRange { ThetaStart = nearRange.ThetaEnd, ThetaEnd = farRange.ThetaEnd, Segment = farRange.Segment}
               };
            }

            // case: near covers left of far
            if (nearRange.ThetaStart <= farRange.ThetaStart && farRange.ThetaStart <= nearRange.ThetaEnd) {
               return new[] {
                  new IntervalRange { ThetaStart = farRange.ThetaStart, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
                  new IntervalRange { ThetaStart = nearRange.ThetaEnd, ThetaEnd = farRange.ThetaEnd, Segment = farRange.Segment }
               };
            }

            // case: insertee covers right of range
            if (nearRange.ThetaStart <= farRange.ThetaEnd && farRange.ThetaEnd <= nearRange.ThetaEnd) {
               return new[] {
                  new IntervalRange { ThetaStart = farRange.ThetaStart, ThetaEnd = nearRange.ThetaStart, Segment = farRange.Segment },
                  new IntervalRange { ThetaStart = nearRange.ThetaStart, ThetaEnd = farRange.ThetaEnd, Segment = nearRange.Segment }
               };
            }

            // impossible to reach here
            throw new Exception($"Impossible state at null split of {nameof(HandleSplit)}.");
         }

         IEnumerable<IntervalRange> HandleSplit(IntervalRange range) {
            // case: range and insertee don't overlap
            if (thetaUpper < range.ThetaStart || range.ThetaEnd < thetaLower) {
               return new[] { range };
            }

            if (range.Segment == null) {
               return HandleNearFarSplit(srange, range);
            }

            var rsxy = new IntLineSegment2(range.Segment.Value.First.XY, range.Segment.Value.Second.XY);

            DoubleVector2 intersection;
            // HACK: No segment-segment intersect point implemented
            if (sxy.Intersects(rsxy) && GeometryOperations.TryFindLineLineIntersection(sxy, rsxy, out intersection)) {
               // conceptually a ray from _origin to intersection hits s and rs at the same time.
               // If shifted perpendicular to angle of intersection, then the near segment emerges.
               var thetaIntersect = FindRadiansRelativeToOrigin(intersection);
               if (range.ThetaStart <= thetaIntersect && thetaIntersect <= range.ThetaEnd) {
                  var directionToLower = DoubleVector2.FromRadiusAngle(1.0, thetaIntersect - PiDiv2);
                  var vsxy = sxy.First.To(sxy.Second).ToDoubleVector2().ToUnit();
                  var vrsxy = rsxy.First.To(rsxy.Second).ToDoubleVector2().ToUnit();
                  var lvsxy = vsxy.ProjectOntoComponentD(directionToLower) > 0 ? vsxy : -1.0 * vsxy;
                  var lvrsxy = vrsxy.ProjectOntoComponentD(directionToLower) > 0 ? vrsxy : -1.0 * vrsxy;
                  var originToIntersect = _origin.To(intersection);
                  var clvsxy = lvsxy.ProjectOntoComponentD(originToIntersect);
                  var clvrsxy = lvrsxy.ProjectOntoComponentD(originToIntersect);
                  var lowerNearer = clvsxy < clvrsxy ? s : range.Segment;
                  var upperNearer = clvsxy < clvrsxy ? range.Segment : s;
                  return new[] {
                     new IntervalRange { ThetaStart = range.ThetaStart, ThetaEnd = thetaIntersect, Segment = lowerNearer },
                     new IntervalRange { ThetaStart = thetaIntersect, ThetaEnd = range.ThetaEnd, Segment = upperNearer }
                  };
               }
            }

            // At here, one segment completely overlaps the other for the theta range
            // Either that, or inserted segment in front of (but not totally covering) range
            var sxy12 = sxy.First.To(sxy.Second);
            var psxy12 = new DoubleVector2(sxy12.Y, -sxy12.X);

            var rsxy12 = rsxy.First.To(rsxy.Second);
            var prsxy12 = new DoubleVector2(rsxy12.Y, -rsxy12.X);

            var osxy1 = _origin.To(sxy.First.ToDoubleVector2());
            var orsxy1 = _origin.To(rsxy.First.ToDoubleVector2());

            var distsxy = osxy1.ProjectOnto(psxy12).SquaredNorm2D();
            var distrsxy = orsxy1.ProjectOnto(prsxy12).SquaredNorm2D();
            bool inserteeNearer = distsxy < distrsxy;
            var nearRange = inserteeNearer ? srange : range;
            var farRange = inserteeNearer ? range : srange;
            return HandleNearFarSplit(nearRange, farRange);

            // case: insertee inside range of 
//            if (range.ThetaStart < thetaLower && thetaUpper < range.ThetaEnd) {
//               return new[] {
//                  new IntervalRange { ThetaStart = range.ThetaStart, ThetaEnd = thetaLower },
//                  new IntervalRange { ThetaStart = thetaLower, ThetaEnd = thetaUpper, Segment = s },
//                  new IntervalRange { ThetaStart = thetaUpper, ThetaEnd = range.ThetaEnd }
//               };
//            }
//            if (range.ThetaStart <= thetaIntersect && thetaIntersect <= range.ThetaEnd) {
//               return new[] {
//               new IntervalRange { ThetaStart = range.ThetaStart, ThetaEnd = range.ThetaEnd, Segment = nearer }
//            };
         }

         _intervalRanges = _intervalRanges.SelectMany(HandleSplit).ToList();
      }

      private double FindRadiansRelativeToOrigin(DoubleVector2 x) {
         var delta = _origin.To(x);
         var r = Math.Atan2(delta.Y, delta.X);
         return r >= 0 ? r : r + TwoPi;
      }

      public List<IntervalRange> Get() => _intervalRanges.ToList();

      public class IntervalRange {
         public IntLineSegment3? Segment { get; set; }
         public double ThetaStart { get; set; }
         public double ThetaEnd { get; set; }
      }
   }
}
