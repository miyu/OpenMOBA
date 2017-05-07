using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenMOBA.Geometry {
   // Everything's in radians, all geometry is reasoned as if relative to _origin.
   // This can be efficiently implemented with an interval tree = O(logN) runtime.
   // For now, just using a flat unordered list, so quite inefficient.
   public class AngularVisibleSegmentStore {
      public const int RANGE_ID_NULL = 0;
      private const double TwoPi = 2.0 * Math.PI;
      private const double PiDiv2 = Math.PI / 2.0;
      private readonly DoubleVector2 _origin;
      private List<IntervalRange> _intervalRanges;
      private int rangeIdCounter = RANGE_ID_NULL;

      public AngularVisibleSegmentStore(DoubleVector2 origin) {
         _origin = origin;
         _intervalRanges = new List<IntervalRange> {
            new IntervalRange {
               Id = rangeIdCounter++,
               ThetaStart = 0.0,
               ThetaEnd = TwoPi
            }
         };
      }
      
      public void Insert(IntLineSegment3 s) {
         var theta1 = FindXYRadiansRelativeToOrigin(s.First.X, s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin(s.Second.X, s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;
         
         if (Math.Abs(theta1 - theta2) > Math.PI) {
            // covered angle range wraps around through theta=0.
            InsertInternal(ref s, 0.0, thetaLower);
            InsertInternal(ref s, thetaUpper, TwoPi);
         } else {
            InsertInternal(ref s, thetaLower, thetaUpper);
         }
      }
      
      // near and far unioned must cover thetaUpper
      IEnumerable<IntervalRange> HandleNearFarSplit(IntervalRange nearRange, IntervalRange farRange, double thetaLower, double thetaUpper) {
         // case: near covers range
         if (nearRange.ThetaStart <= thetaLower && thetaUpper <= nearRange.ThetaEnd) {
            return new[] { new IntervalRange { Id = nearRange.Id, ThetaStart = thetaLower, ThetaEnd = thetaUpper, Segment = nearRange.Segment } };
         }

         // case: near exclusively within range
         if (thetaLower < nearRange.ThetaStart && nearRange.ThetaEnd < thetaUpper) {
            return new[] {
               new IntervalRange { Id = farRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaStart, Segment = farRange.Segment},
               new IntervalRange { Id = nearRange.Id, ThetaStart = nearRange.ThetaStart, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
               new IntervalRange { Id = farRange.Id, ThetaStart = nearRange.ThetaEnd, ThetaEnd = thetaUpper, Segment = farRange.Segment}
            };
         }

         // case: near covers left of range
         if (nearRange.ThetaStart <= thetaLower && thetaLower <= nearRange.ThetaEnd) {
            return new[] {
               new IntervalRange { Id = nearRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
               new IntervalRange { Id = farRange.Id, ThetaStart = nearRange.ThetaEnd, ThetaEnd = thetaUpper, Segment = farRange.Segment }
            };
         }

         // case: near covers right of range
         if (nearRange.ThetaStart <= thetaUpper && thetaUpper <= nearRange.ThetaEnd) {
            return new[] {
               new IntervalRange { Id = farRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaStart, Segment = farRange.Segment },
               new IntervalRange { Id = nearRange.Id, ThetaStart = nearRange.ThetaStart, ThetaEnd = thetaUpper, Segment = nearRange.Segment }
            };
         }

         // impossible to reach here
         throw new Exception($"Impossible state at null split of {nameof(HandleNearFarSplit)}.");
      }

      private void InsertInternal(ref IntLineSegment3 s, double thetaLower, double thetaUpper) {
//         Console.WriteLine($"InsertInternal: {s}, {thetaLower} {thetaUpper}");
         var sxy = new IntLineSegment2(s.First.XY, s.Second.XY);
         var srange = new IntervalRange { Id = rangeIdCounter++, ThetaStart = thetaLower, ThetaEnd = thetaUpper, Segment = s };

         IEnumerable<IntervalRange> HandleSplit(IntervalRange range) {
            // case: range and insertee don't overlap
            if (thetaUpper < range.ThetaStart || range.ThetaEnd < thetaLower) {
               return new[] { range };
            }

            if (range.Id == RANGE_ID_NULL) {
               return HandleNearFarSplit(srange, range, range.ThetaStart, range.ThetaEnd);
            }

            var rsxy = new IntLineSegment2(range.Segment.First.XY, range.Segment.Second.XY);

            DoubleVector2 intersection;
            // HACK: No segment-segment intersect point implemented
            if (sxy.Intersects(rsxy) && GeometryOperations.TryFindLineLineIntersection(sxy, rsxy, out intersection)) {
               // conceptually a ray from _origin to intersection hits s and rs at the same time.
               // If shifted perpendicular to angle of intersection, then the near segment emerges.
               var thetaIntersect = FindXYRadiansRelativeToOrigin(intersection.X, intersection.Y);
               if (range.ThetaStart <= thetaIntersect && thetaIntersect <= range.ThetaEnd) {
                  var directionToLower = DoubleVector2.FromRadiusAngle(1.0, thetaIntersect - PiDiv2);
                  var vsxy = sxy.First.To(sxy.Second).ToDoubleVector2().ToUnit();
                  var vrsxy = rsxy.First.To(rsxy.Second).ToDoubleVector2().ToUnit();
                  var lvsxy = vsxy.ProjectOntoComponentD(directionToLower) > 0 ? vsxy : -1.0 * vsxy;
                  var lvrsxy = vrsxy.ProjectOntoComponentD(directionToLower) > 0 ? vrsxy : -1.0 * vrsxy;
                  var originToIntersect = _origin.To(intersection);
                  var clvsxy = lvsxy.ProjectOntoComponentD(originToIntersect);
                  var clvrsxy = lvrsxy.ProjectOntoComponentD(originToIntersect);
                  var isInserteeNearerAtLower = clvsxy < clvrsxy;
//                  Console.WriteLine("IINAL: " + isInserteeNearerAtLower);
                  if (isInserteeNearerAtLower) {
                     return HandleNearFarSplit(srange, range, range.ThetaStart, thetaIntersect)
                        .Concat(HandleNearFarSplit(range, srange, thetaIntersect, range.ThetaEnd));
                  } else {
                     return HandleNearFarSplit(range, srange, range.ThetaStart, thetaIntersect)
                        .Concat(HandleNearFarSplit(srange, range, thetaIntersect, range.ThetaEnd));
                  }
               }
            }

            // At here, one segment completely overlaps the other for the theta range
            // Either that, or inserted segment in front of (but not totally covering) range

            var distsxy = _origin.To(GeometryOperations.FindNearestPoint(sxy, _origin)).SquaredNorm2D();
            var distrsxy = _origin.To(GeometryOperations.FindNearestPoint(rsxy, _origin)).SquaredNorm2D();
            bool inserteeNearer = distsxy < distrsxy;
            var nearRange = inserteeNearer ? srange : range;
            var farRange = inserteeNearer ? range : srange;
            return HandleNearFarSplit(nearRange, farRange, range.ThetaStart, range.ThetaEnd);
         }

         var n = new List<IntervalRange>();
         var lastRangeId = RANGE_ID_NULL - 1;
         IntervalRange lastRange = null;
         foreach (var splittee in _intervalRanges) {
            foreach (var range in HandleSplit(splittee)) {
               if (lastRangeId == range.Id) {
                  lastRange.ThetaEnd = range.ThetaEnd;
               } else {
                  lastRange = range;
                  lastRangeId = range.Id;
                  n.Add(range);
               }
            }
         }
         _intervalRanges = n;
      }

      private double FindXYRadiansRelativeToOrigin(double x, double y) {
         var dx = x - _origin.X;
         var dy = y - _origin.Y;
         var r = Math.Atan2(dy, dx);
         return r >= 0 ? r : r + TwoPi;
      }

      public List<IntervalRange> Get() => _intervalRanges.ToList();

      public class IntervalRange {
         public int Id { get; set; }
         public IntLineSegment3 Segment { get; set; }
         public double ThetaStart { get; set; }
         public double ThetaEnd { get; set; }
      }
   }
}
