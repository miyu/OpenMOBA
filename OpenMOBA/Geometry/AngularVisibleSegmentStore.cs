using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace OpenMOBA.Geometry {
   // Everything's in radians, all geometry is reasoned as if relative to _origin.
   // This can be efficiently implemented with an interval tree = O(logN) runtime.
   // For now, just using a flat unordered list, so quite inefficient.
   public class AngularVisibleSegmentStore {
      public const int RANGE_ID_NULL = 0;
      private const double TwoPi = 2.0 * Math.PI;
      private const double PiDiv2 = Math.PI / 2.0;
      private readonly DoubleVector2 _origin;
      private IntervalRange[] _intervalRanges;
      private int rangeIdCounter = RANGE_ID_NULL;

      public AngularVisibleSegmentStore(DoubleVector2 origin) {
         _origin = origin;
         _intervalRanges = new [] {
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

      private void InsertInternal(ref IntLineSegment3 s, double insertionThetaLower, double insertionThetaUpper) {
//         Console.WriteLine($"InsertInternal: {s}, {thetaLower} {thetaUpper}");
         var sxy = new IntLineSegment2(s.First.XY, s.Second.XY);
         var srange = new IntervalRange { Id = rangeIdCounter++, ThetaStart = insertionThetaLower, ThetaEnd = insertionThetaUpper, Segment = s };

         var splittableBeginIndexInclusive = FindOverlappingRangeIndex(insertionThetaLower, 0, true);
         var splittableEndIndexInclusive = FindOverlappingRangeIndex(insertionThetaUpper, splittableBeginIndexInclusive, false);

         // a given segment can be split into 3 at max - technically this overallocates because it's impossible
         // for two 3-splits to happen in a row. Actually, assuming no overlaps one can only really produce
         // # splittables + 2 total new segments (new segments on left/right side).
         var n = new IntervalRange[(splittableEndIndexInclusive - splittableBeginIndexInclusive + 1) + 2];
         var nSize = 0;
         IntervalRange lastRange = null;

         void EmitRange(int rangeId, ref IntLineSegment3 segment, double thetaStart, double thetaEnd) {
            if (thetaStart == thetaEnd) {
               return;
            }
            if (lastRange != null && lastRange.Id == rangeId) {
               lastRange.ThetaEnd = thetaEnd;
            } else {
               lastRange = new IntervalRange { Id = rangeId, Segment = segment, ThetaStart = thetaStart, ThetaEnd = thetaEnd };
               n[nSize] = lastRange;
               nSize++;
            }
         }

         // near and far unioned must cover thetaUpper
         void HandleNearFarSplit(IntervalRange nearRange, IntervalRange farRange, double thetaLower, double thetaUpper) {
            // case: near covers range
            if (nearRange.ThetaStart <= thetaLower && thetaUpper <= nearRange.ThetaEnd) {
               EmitRange(nearRange.Id, ref nearRange.Segment, thetaLower, thetaUpper);
               return;
               //               return new[] { new IntervalRange { Id = nearRange.Id, ThetaStart = thetaLower, ThetaEnd = thetaUpper, Segment = nearRange.Segment } };
            }

            // case: near exclusively within range
            if (thetaLower < nearRange.ThetaStart && nearRange.ThetaEnd < thetaUpper) {
               EmitRange(farRange.Id, ref farRange.Segment, thetaLower, nearRange.ThetaStart);
               EmitRange(nearRange.Id, ref nearRange.Segment, nearRange.ThetaStart, nearRange.ThetaEnd);
               EmitRange(farRange.Id, ref farRange.Segment, nearRange.ThetaEnd, thetaUpper);
               return;
               //               return new[] {
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaStart, Segment = farRange.Segment},
               //                  new IntervalRange { Id = nearRange.Id, ThetaStart = nearRange.ThetaStart, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = nearRange.ThetaEnd, ThetaEnd = thetaUpper, Segment = farRange.Segment}
               //               };
            }

            // case: near covers left of range
            if (nearRange.ThetaStart <= thetaLower && thetaLower < nearRange.ThetaEnd) {
               EmitRange(nearRange.Id, ref nearRange.Segment, thetaLower, nearRange.ThetaEnd);
               EmitRange(farRange.Id, ref farRange.Segment, nearRange.ThetaEnd, thetaUpper);
               return;
               //               return new[] {
               //                  new IntervalRange { Id = nearRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = nearRange.ThetaEnd, ThetaEnd = thetaUpper, Segment = farRange.Segment }
               //               };
            }

            // case: near covers right of range
            if (nearRange.ThetaStart < thetaUpper && thetaUpper <= nearRange.ThetaEnd) {
               EmitRange(farRange.Id, ref farRange.Segment, thetaLower, nearRange.ThetaStart);
               EmitRange(nearRange.Id, ref nearRange.Segment, nearRange.ThetaStart, thetaUpper);
               return;
               //               return new[] {
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaStart, Segment = farRange.Segment },
               //                  new IntervalRange { Id = nearRange.Id, ThetaStart = nearRange.ThetaStart, ThetaEnd = thetaUpper, Segment = nearRange.Segment }
               //               };
            }

            // impossible to reach here
            throw new Exception($"Impossible state at null split of {nameof(HandleNearFarSplit)}.");
         }

         void HandleSplit(IntervalRange range) {
            Debug.Assert(IsRangeOverlap(insertionThetaLower, insertionThetaUpper, range.ThetaStart, range.ThetaEnd));

            if (range.Id == RANGE_ID_NULL) {
               HandleNearFarSplit(srange, range, range.ThetaStart, range.ThetaEnd);
               return;
            }

            var rsxy = new IntLineSegment2(range.Segment.First.XY, range.Segment.Second.XY);

//            // is this code necessary? Seems like not... though not sure why. We do have intersecting segments
//            // but the intersect is quite minor (just at corners)...
//            DoubleVector2 intersection;
//            // HACK: No segment-segment intersect point implemented
//            // sxy.Intersects(rsxy) && GeometryOperations.TryFindLineLineIntersection(sxy, rsxy, out intersection)
//            if (GeometryOperations.TryFindSegmentSegmentIntersection(ref sxy, ref rsxy, out intersection)) {
//               // conceptually a ray from _origin to intersection hits s and rs at the same time.
//               // If shifted perpendicular to angle of intersection, then the near segment emerges.
//               var thetaIntersect = FindXYRadiansRelativeToOrigin(intersection.X, intersection.Y);
//               if (range.ThetaStart <= thetaIntersect && thetaIntersect <= range.ThetaEnd) {
//                  var directionToLower = DoubleVector2.FromRadiusAngle(1.0, thetaIntersect - PiDiv2);
//                  var vsxy = sxy.First.To(sxy.Second).ToDoubleVector2().ToUnit();
//                  var vrsxy = rsxy.First.To(rsxy.Second).ToDoubleVector2().ToUnit();
//                  var lvsxy = vsxy.ProjectOntoComponentD(directionToLower) > 0 ? vsxy : -1.0 * vsxy;
//                  var lvrsxy = vrsxy.ProjectOntoComponentD(directionToLower) > 0 ? vrsxy : -1.0 * vrsxy;
//                  var originToIntersect = _origin.To(intersection);
//                  var clvsxy = lvsxy.ProjectOntoComponentD(originToIntersect);
//                  var clvrsxy = lvrsxy.ProjectOntoComponentD(originToIntersect);
//                  var isInserteeNearerAtLower = clvsxy < clvrsxy;
////                  Console.WriteLine("IINAL: " + isInserteeNearerAtLower);
//                  if (isInserteeNearerAtLower) {
//                     return HandleNearFarSplit(srange, range, range.ThetaStart, thetaIntersect)
//                        .Concat(HandleNearFarSplit(range, srange, thetaIntersect, range.ThetaEnd));
//                  } else {
//                     return HandleNearFarSplit(range, srange, range.ThetaStart, thetaIntersect)
//                        .Concat(HandleNearFarSplit(srange, range, thetaIntersect, range.ThetaEnd));
//                  }
//               }
//            }

            // At here, one segment completely overlaps the other for the theta range
            // Either that, or inserted segment in front of (but not totally covering) range
            // Either way, it will always be the case that any point on the "near" segment is closer
            // to _origin than any point on the "far" segment assuming within correct theta.
            // I take center of segments as their endpoints are ambiguous between neighboring segments
            // of a polygon.

            var distsxy = _origin.To((sxy.First + sxy.Second).ToDoubleVector2() / 2.0).SquaredNorm2D();
            var distrsxy = _origin.To((rsxy.First + rsxy.Second).ToDoubleVector2() / 2.0).SquaredNorm2D();
            bool inserteeNearer = distsxy < distrsxy;
            var nearRange = inserteeNearer ? srange : range;
            var farRange = inserteeNearer ? range : srange;
            HandleNearFarSplit(nearRange, farRange, range.ThetaStart, range.ThetaEnd);
         }

//         n.AddRange(_intervalRanges.Take(ibegin));
         for (int it = splittableBeginIndexInclusive; it <= splittableEndIndexInclusive; it++) {
            HandleSplit(_intervalRanges[it]);
         }
         //         n.AddRange(_intervalRanges.Skip(iend + 1));

         bool segmentInserted = false;
         for (int i = 0; i < nSize && !segmentInserted; i++) {
            if (n[i].Id == srange.Id) {
               segmentInserted = true;
            }
         }
         if (!segmentInserted) {
            return;
         }

         var nhead = splittableBeginIndexInclusive;
         var ntail = _intervalRanges.Length - splittableEndIndexInclusive - 1;
         var result = new IntervalRange[nhead + nSize + ntail];
         Array.Copy(_intervalRanges, 0, result, 0, nhead);
         Array.Copy(n, 0, result, nhead, nSize);
         Array.Copy(_intervalRanges, _intervalRanges.Length - ntail, result, nhead + nSize, ntail);
         _intervalRanges = result;
      }

      // inclusiveRangeStart: given insertion range r, its start is inclusive while its end is exclusive,
      // so if insertee start/end == split candidate range start, varying behavior.
      private int FindOverlappingRangeIndex(double theta, int lowerInitInclusive, bool inclusiveRangeStart) {
         if (theta == 0.0) {
            Debug.Assert(inclusiveRangeStart);
            return 0;
         } else if (theta == TwoPi) {
            return _intervalRanges.Length - 1;
         }

         var lowerInclusive = lowerInitInclusive;
         var upperExclusive = _intervalRanges.Length;
         while (lowerInclusive != upperExclusive) {
            var mid = lowerInclusive + (upperExclusive - lowerInclusive) / 2;
            var item = _intervalRanges[mid];
            if (item.ThetaStart == theta) {
               if (inclusiveRangeStart) {
                  return mid;
               } else {
                  return mid - 1;
               }
            } else if (item.ThetaStart < theta && theta < item.ThetaEnd) {
               return mid;
            } else if (theta < item.ThetaStart) {
               upperExclusive = mid;
            } else {
               lowerInclusive = mid + 1;
            }
         }
         throw new Exception("Impossible state - bad theta? " + theta);
      }

      private bool IsRangeOverlap(double aStart, double aEnd, double bStart, double bEnd) {
         return !(bEnd < aStart || aEnd < bStart);
      }

      private double FindXYRadiansRelativeToOrigin(double x, double y) {
         var dx = x - _origin.X;
         var dy = y - _origin.Y;
         var r = Math.Atan2(dy, dx);
         return r >= 0 ? r : r + TwoPi;
      }

      public List<IntervalRange> Get() => _intervalRanges.ToList();

      public class IntervalRange {
         public int Id;
         public IntLineSegment3 Segment;
         public double ThetaStart; // inclusive
         public double ThetaEnd; // exclusive
      }
   }
}
