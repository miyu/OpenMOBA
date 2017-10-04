using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace OpenMOBA.Geometry {
   // Everything's in radians, all geometry is reasoned as if relative to _origin.
   // This can be efficiently implemented with an interval tree = O(logN) runtime.
   // For now, just using a flat unordered list, so quite inefficient.
   public class VisibilityPolygon {
      public const int RANGE_ID_NULL = 0;
      private const double TwoPi = 2.0 * Math.PI;
      private const double PiDiv2 = Math.PI / 2.0;
      private readonly DoubleVector2 _origin;
      private IntervalRange[] _intervalRanges;
      private int rangeIdCounter = RANGE_ID_NULL;

      public VisibilityPolygon(DoubleVector2 origin) {
         _origin = origin;
         _intervalRanges = new [] {
            new IntervalRange {
               Id = rangeIdCounter++,
               ThetaStart = 0.0,
               ThetaEnd = TwoPi
            }
         };
      }

      public DoubleVector2 Origin => _origin;

      public void Insert(IntLineSegment2 s, bool supportOverlappingLines = false) {
         var theta1 = FindXYRadiansRelativeToOrigin(s.First.X, s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin(s.Second.X, s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;

         if (Math.Abs(theta1 - theta2) > Math.PI) {
            // covered angle range wraps around through theta=0.
            InsertInternal(ref s, 0.0, thetaLower, supportOverlappingLines);
            InsertInternal(ref s, thetaUpper, TwoPi, supportOverlappingLines);
         } else {
            InsertInternal(ref s, thetaLower, thetaUpper, supportOverlappingLines);
         }
      }

      public void ClearBeyond(IntLineSegment2 s, bool supportOverlappingLines = false) {
         var theta1 = FindXYRadiansRelativeToOrigin(s.First.X, s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin(s.Second.X, s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;

         if (Math.Abs(theta1 - theta2) > Math.PI) {
            // covered angle range wraps around through theta=0.
            InsertInternalInternal(s, RANGE_ID_NULL, 0.0, thetaLower, supportOverlappingLines);
            InsertInternalInternal(s, RANGE_ID_NULL, thetaUpper, TwoPi, supportOverlappingLines);
         } else {
            InsertInternalInternal(s, RANGE_ID_NULL, thetaLower, thetaUpper, supportOverlappingLines);
         }
      }

      private void InsertInternal(ref IntLineSegment2 s, double insertionThetaLower, double insertionThetaUpper, bool supportOverlappingLines) {
         if (insertionThetaLower == insertionThetaUpper) {
            return;
         }

         //         Console.WriteLine($"InsertInternal: {s}, {thetaLower} {thetaUpper}");

         // cull if wall faces away from origin
         var sperp = new DoubleVector2(s.Y2 - s.Y1, -(s.X2 - s.X1));
         var os1 = _origin.To(s.First.ToDoubleVector2());
         if (sperp.Dot(os1) < 0) {
            return;
         }
         var rangeId = rangeIdCounter++;
         InsertInternalInternal(s, rangeId, insertionThetaLower, insertionThetaUpper, supportOverlappingLines);
      }

      private void InsertInternalInternal(IntLineSegment2 s, int sRangeId, double insertionThetaLower, double insertionThetaUpper, bool supportOverlappingLines) {
         var sMidpoint = new DoubleVector2((s.First.X + s.Second.X) / 2.0, (s.First.Y + s.Second.Y) / 2.0);
         // See distrsxy for why this makes sense.
         var sDist = _origin.To(sMidpoint).SquaredNorm2D();
         var srange = new IntervalRange {
            Id = sRangeId,
            ThetaStart = insertionThetaLower,
            ThetaEnd = insertionThetaUpper,
            Segment = s,
            MidpointDistanceToOriginSquared = sDist
         };


         var splittableBeginIndexInclusive = FindOverlappingRangeIndex(insertionThetaLower, 0, true);
         var splittableEndIndexInclusive = FindOverlappingRangeIndex(insertionThetaUpper, splittableBeginIndexInclusive, false);

         // a given segment can be split into 3 at max - technically this overallocates because it's impossible
         // for two 3-splits to happen in a row. Actually, assuming no overlaps one can only really produce
         // # splittables + 2 total new segments (new segments on left/right side).
         var n = new IntervalRange[(splittableEndIndexInclusive - splittableBeginIndexInclusive + 1) * 3];
            //new IntervalRange[(splittableEndIndexInclusive - splittableBeginIndexInclusive + 1) + 2];
         var nSize = 0;
         IntervalRange lastRange = null;

         void EmitRange(int rangeId, ref IntLineSegment2 segment, double originDistanceSquared, double thetaStart, double thetaEnd) {
            if (thetaStart == thetaEnd) {
               return;
            }
            if (lastRange != null && lastRange.Id == rangeId) {
               lastRange.ThetaEnd = thetaEnd;
            } else {
               lastRange = new IntervalRange { Id = rangeId, Segment = segment, ThetaStart = thetaStart, ThetaEnd = thetaEnd, MidpointDistanceToOriginSquared = originDistanceSquared };
               n[nSize] = lastRange;
               nSize++;
            }
         }

         // near and far unioned must cover thetaUpper
         void HandleNearFarSplit(IntervalRange nearRange, double nearDistanceSquared, IntervalRange farRange, double farDistanceSquared, double thetaLower, double thetaUpper) {
            // case: near covers range
            if (nearRange.ThetaStart <= thetaLower && thetaUpper <= nearRange.ThetaEnd) {
               EmitRange(nearRange.Id, ref nearRange.Segment, nearDistanceSquared, thetaLower, thetaUpper);
               return;
               //               return new[] { new IntervalRange { Id = nearRange.Id, ThetaStart = thetaLower, ThetaEnd = thetaUpper, Segment = nearRange.Segment } };
            }

            // case: near exclusively within range
            if (thetaLower < nearRange.ThetaStart && nearRange.ThetaEnd < thetaUpper) {
               EmitRange(farRange.Id, ref farRange.Segment, farDistanceSquared, thetaLower, nearRange.ThetaStart);
               EmitRange(nearRange.Id, ref nearRange.Segment, nearDistanceSquared, nearRange.ThetaStart, nearRange.ThetaEnd);
               EmitRange(farRange.Id, ref farRange.Segment, farDistanceSquared, nearRange.ThetaEnd, thetaUpper);
               return;
               //               return new[] {
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaStart, Segment = farRange.Segment},
               //                  new IntervalRange { Id = nearRange.Id, ThetaStart = nearRange.ThetaStart, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = nearRange.ThetaEnd, ThetaEnd = thetaUpper, Segment = farRange.Segment}
               //               };
            }

            // case: near covers left of range
            if (nearRange.ThetaStart <= thetaLower && thetaLower < nearRange.ThetaEnd) {
               EmitRange(nearRange.Id, ref nearRange.Segment, nearDistanceSquared, thetaLower, nearRange.ThetaEnd);
               EmitRange(farRange.Id, ref farRange.Segment, farDistanceSquared, nearRange.ThetaEnd, thetaUpper);
               return;
               //               return new[] {
               //                  new IntervalRange { Id = nearRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = nearRange.ThetaEnd, ThetaEnd = thetaUpper, Segment = farRange.Segment }
               //               };
            }

            // case: near covers right of range
            if (nearRange.ThetaStart < thetaUpper && thetaUpper <= nearRange.ThetaEnd) {
               EmitRange(farRange.Id, ref farRange.Segment, farDistanceSquared, thetaLower, nearRange.ThetaStart);
               EmitRange(nearRange.Id, ref nearRange.Segment, nearDistanceSquared, nearRange.ThetaStart, thetaUpper);
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
               HandleNearFarSplit(srange, sDist, range, double.PositiveInfinity, range.ThetaStart, range.ThetaEnd);
               return;
            }

            var rsxy = range.Segment;

//            // is this code necessary? Seems like not... though not sure why. We do have intersecting segments
//            // but the intersect is quite minor (just at corners)...
            DoubleVector2 intersection;
            // HACK: No segment-segment intersect point implemented
            // sxy.Intersects(rsxy) && GeometryOperations.TryFindLineLineIntersection(sxy, rsxy, out intersection)
            if (GeometryOperations.TryFindSegmentSegmentIntersection(ref s, ref rsxy, out intersection)) {
               // conceptually a ray from _origin to intersection hits s and rs at the same time.
               // If shifted perpendicular to angle of intersection, then the near segment emerges.
               var thetaIntersect = FindXYRadiansRelativeToOrigin(intersection.X, intersection.Y);
               if (range.ThetaStart <= thetaIntersect && thetaIntersect <= range.ThetaEnd) {
                  var directionToLower = DoubleVector2.FromRadiusAngle(1.0, thetaIntersect - PiDiv2);
                  var vsxy = s.First.To(s.Second).ToDoubleVector2().ToUnit();
                  var vrsxy = rsxy.First.To(rsxy.Second).ToDoubleVector2().ToUnit();
                  var lvsxy = vsxy.ProjectOntoComponentD(directionToLower) > 0 ? vsxy : -1.0 * vsxy;
                  var lvrsxy = vrsxy.ProjectOntoComponentD(directionToLower) > 0 ? vrsxy : -1.0 * vrsxy;
                  var originToIntersect = _origin.To(intersection);
                  var clvsxy = lvsxy.ProjectOntoComponentD(originToIntersect);
                  var clvrsxy = lvrsxy.ProjectOntoComponentD(originToIntersect);
                  var isInserteeNearerAtLower = clvsxy < clvrsxy;
//                  Console.WriteLine("IINAL: " + isInserteeNearerAtLower);
                  if (isInserteeNearerAtLower) {
                     HandleNearFarSplit(range, range.MidpointDistanceToOriginSquared, srange, srange.MidpointDistanceToOriginSquared, range.ThetaStart, thetaIntersect);
                     HandleNearFarSplit(srange, srange.MidpointDistanceToOriginSquared, range, range.MidpointDistanceToOriginSquared, thetaIntersect, range.ThetaEnd);
                  } else {
                     HandleNearFarSplit(srange, srange.MidpointDistanceToOriginSquared, range, range.MidpointDistanceToOriginSquared, range.ThetaStart, thetaIntersect);
                     HandleNearFarSplit(range, range.MidpointDistanceToOriginSquared, srange, srange.MidpointDistanceToOriginSquared, thetaIntersect, range.ThetaEnd);
                  }
                  return;
               }
            }

            // At here, one segment completely overlaps the other for the theta range
            // Either that, or inserted segment in front of (but not totally covering) range
            // Either way, it will always be the case that any point on the "near" segment is closer
            // to _origin than any point on the "far" segment assuming within correct theta.
            // I take center of segments as their endpoints are ambiguous between neighboring segments
            // of a polygon.

            var distrsxy = range.MidpointDistanceToOriginSquared;
            bool inserteeNearer = sDist < distrsxy;
            var nearRange = inserteeNearer ? srange : range;
            var nearDistanceSquared = inserteeNearer ? sDist : distrsxy;
            var farRange = inserteeNearer ? range : srange;
            var farDistanceSquared = inserteeNearer ? distrsxy : sDist;
            HandleNearFarSplit(nearRange, nearDistanceSquared, farRange, farDistanceSquared, range.ThetaStart, range.ThetaEnd);
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
//         var r = Math.Atan2(dy, dx);
         var r = FastAtan2(dy, dx);

//         Console.WriteLine(Math.Atan2(1.0, 0.0));
//         Console.WriteLine(FastAtan2(1.0, 0.0));
//         while (true) ;
         return r >= 0 ? r : r + TwoPi;
      }

      // https://math.stackexchange.com/questions/1098487/atan2-faster-approximation
      private static double FastAtan2(double y, double x) {
         var ax = Math.Abs(x);
         var ay = Math.Abs(y);

         // a:= min(| x |, | y |) / max(| x |, | y |)
         var a = ax < ay ? (ax / ay) : (ay / ax);

         // s:= a * a
         var s = a * a;

         // r:= ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a
         var r = ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a;

         // if | y | > | x | then r:= 1.57079637 - r
         if (ay > ax) r = 1.57079637 - r;

         // if x < 0 then r := 3.14159274 - r
         if (x < 0) r = 3.14159274 - r;

         // if y < 0 then r := -r
         if (y < 0) r = -r;

         return r;
      }

      public IntervalRange[] Get() => _intervalRanges;

      public IntervalRange Stab(double theta) => _intervalRanges[FindOverlappingRangeIndex(theta, 0, true)];

      public (int startIndexInclusive, int endIndexExclusive)[] RangeStab(IntLineSegment2 s) {
         return RangeStab(new DoubleLineSegment2(s.First.ToDoubleVector2(), s.Second.ToDoubleVector2()));
      }

      public (int startIndexInclusive, int endIndexExclusive)[] RangeStab(DoubleLineSegment2 s) {
         var theta1 = FindXYRadiansRelativeToOrigin(s.First.X, s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin(s.Second.X, s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;

         if (Math.Abs(theta1 - theta2) > Math.PI) {
            // covered angle range wraps around through theta=0.
            return new [] {
               RangeStabInternal(0.0, thetaLower),
               RangeStabInternal(thetaUpper, TwoPi)
            };
         } else {
            return new[] {
               RangeStabInternal(thetaLower, thetaUpper)
            };
         }
      }

      private (int, int) RangeStabInternal(double thetaLower, double thetaUpper) {
         if (thetaLower == thetaUpper) {
            var index = FindOverlappingRangeIndex(thetaLower, 0, true);
            return (index, index);
         }

         var queryRangeBeginIndexInclusive = FindOverlappingRangeIndex(thetaLower, 0, true);
         var queryRangeEndIndexInclusive = FindOverlappingRangeIndex(thetaUpper, queryRangeBeginIndexInclusive, false);
         return (queryRangeBeginIndexInclusive, queryRangeEndIndexInclusive);
      }

      public bool Contains(DoubleVector2 p) {
         var theta = FindXYRadiansRelativeToOrigin(p.X, p.Y);
         var pToOriginDistSquared = _origin.To(p).SquaredNorm2D();
         var rangeAtTheta = Stab(theta);
         if (rangeAtTheta.Id == RANGE_ID_NULL) {
            return false;
         }
         var originPastPToPolyDistSquared = rangeAtTheta.MidpointDistanceToOriginSquared;
         return pToOriginDistSquared < originPastPToPolyDistSquared;
      }

      public class IntervalRange {
         public int Id;
         public IntLineSegment2 Segment;
         public double ThetaStart; // inclusive
         public double ThetaEnd; // exclusive
         public double MidpointDistanceToOriginSquared;
      }

      public static VisibilityPolygon Create(DoubleVector2 origin, IReadOnlyList<IntLineSegment2> barriers) {
         var vp = new VisibilityPolygon(origin);
         foreach (var barrier in barriers) {
            vp.Insert(barrier);
         }
         return vp;
      }
   }
}
