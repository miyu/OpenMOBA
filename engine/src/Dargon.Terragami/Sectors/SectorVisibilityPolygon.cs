#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Dargon.Commons.Exceptions;
using Dargon.Commons.Pooling;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami.Sectors {
   // Everything's in radians, all geometry is reasoned as if relative to _origin.
   // This can be efficiently implemented with an interval tree = O(logN) runtime.
   // For now, just using a flat unordered list, so quite inefficient.
   // It is a persistent data structure, though.
   public class SectorVisibilityPolygon {
      public const int RANGE_ID_INFINITESIMALLY_NEAR = -2;
      public const int RANGE_ID_INFINITELY_FAR = -1; // (The null range id)
      public const int RANGE_ID_INITIAL = 0;

#if use_fixed
      public static readonly cDouble TwoPi = CDoubleMath.TwoPi;
      private static readonly cDouble PiDiv2 = CDoubleMath.PiDiv2;
#else
      public const double TwoPi = 2.0 * Math.PI;
      private const double PiDiv2 = Math.PI / 2.0;
#endif
      private readonly DoubleVector2 _origin;
      private IntervalRange[] _intervalRanges;
      private readonly IComparer<IntLineSegment2> _segmentComparer;
      private int rangeIdCounter = RANGE_ID_INITIAL;

      private static TlsPow2BufferManager<(cDouble, int, bool)> tlsEventBuffer = new TlsPow2BufferManager<(cDouble, int, bool)>();
      private static TlsPow2BufferManager<int> tlsEventOrder = new TlsPow2BufferManager<int>();
      private static TlsPow2BufferManager<bool> tlsEventBugCheck = new TlsPow2BufferManager<bool>();

      public SectorVisibilityPolygon(DoubleVector2 origin)
         : this(
            origin, new[] {
               new IntervalRange {
                  Id = RANGE_ID_INFINITELY_FAR,
                  ThetaStart = PlayOn.CDoubleMath.c0,
                  ThetaEnd = TwoPi
               }
            }) { }

      public SectorVisibilityPolygon(DoubleVector2 origin, IntervalRange[] intervalRanges, IComparer<IntLineSegment2> segmentComparer = null) {
         _origin = origin;
         _intervalRanges = intervalRanges;
         _segmentComparer = segmentComparer ?? new OverlappingIntSegmentOriginDistanceComparator(_origin);
      }

      public DoubleVector2 Origin => _origin;
      public IComparer<IntLineSegment2> SegmentComparer => _segmentComparer;

      public void Insert(IntLineSegment2 s, bool supportOverlappingLines = false) {
         var theta1 = FindXYRadiansRelativeToOrigin((cDouble)s.First.X, (cDouble)s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin((cDouble)s.Second.X, (cDouble)s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;

         if (PlayOn.CDoubleMath.Abs(theta1 - theta2) > PlayOn.CDoubleMath.Pi) {
            // covered angle range wraps around through theta=0.
            InsertInternal(ref s, PlayOn.CDoubleMath.c0, thetaLower, supportOverlappingLines);
            InsertInternal(ref s, thetaUpper, PlayOn.CDoubleMath.TwoPi, supportOverlappingLines);
         } else {
            InsertInternal(ref s, thetaLower, thetaUpper, supportOverlappingLines);
         }
      }

      public void ClearBeyond(IntLineSegment2 s, bool supportOverlappingLines = false) {
         var theta1 = FindXYRadiansRelativeToOrigin((cDouble)s.First.X, (cDouble)s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin((cDouble)s.Second.X, (cDouble)s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;

         if (PlayOn.CDoubleMath.Abs(theta1 - theta2) > PlayOn.CDoubleMath.Pi) {
            // covered angle range wraps around through theta=0.
            InsertInternalInternal(s, RANGE_ID_INFINITELY_FAR, PlayOn.CDoubleMath.c0, thetaLower, supportOverlappingLines, false);
            InsertInternalInternal(s, RANGE_ID_INFINITELY_FAR, thetaUpper, TwoPi, supportOverlappingLines, false);
         } else {
            InsertInternalInternal(s, RANGE_ID_INFINITELY_FAR, thetaLower, thetaUpper, supportOverlappingLines, false);
         }
      }

      public void ClearBefore(IntLineSegment2 s, bool supportOverlappingLines = false) {
         var theta1 = FindXYRadiansRelativeToOrigin((cDouble)s.First.X, (cDouble)s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin((cDouble)s.Second.X, (cDouble)s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;

         if (PlayOn.CDoubleMath.Abs(theta1 - theta2) > PlayOn.CDoubleMath.Pi) {
            // covered angle range wraps around through theta=0.
            InsertInternalInternal(s, RANGE_ID_INFINITELY_FAR, PlayOn.CDoubleMath.c0, thetaLower, supportOverlappingLines, true);
            InsertInternalInternal(s, RANGE_ID_INFINITELY_FAR, thetaUpper, TwoPi, supportOverlappingLines, true);
         } else {
            InsertInternalInternal(s, RANGE_ID_INFINITELY_FAR, thetaLower, thetaUpper, supportOverlappingLines, true);
         }
      }

      private void InsertInternal(ref IntLineSegment2 s, cDouble insertionThetaLower, cDouble insertionThetaUpper, bool supportOverlappingLines) {
         if (insertionThetaLower == insertionThetaUpper) {
            return;
         }

         //         Console.WriteLine($"InsertInternal: {s}, {thetaLower} {thetaUpper}");

         // cull if wall faces away from origin
         var sperp = new DoubleVector2(s.Y2 - s.Y1, -(s.X2 - s.X1));
         var os1 = _origin.To(s.First.ToDoubleVector2());
         if (sperp.Dot(os1) < PlayOn.CDoubleMath.c0) {
            //return;
         }
         var rangeId = rangeIdCounter++;
         InsertInternalInternal(s, rangeId, insertionThetaLower, insertionThetaUpper, supportOverlappingLines, false);
      }

      private void InsertInternalInternal(IntLineSegment2 s, int sRangeId, cDouble insertionThetaLower, cDouble insertionThetaUpper, bool supportOverlappingLines, bool furthestSegmentWins) {
         // ReSharper disable once CompareOfFloatsByEqualityOperator
         if (insertionThetaLower == insertionThetaUpper) return;

         // See distrsxy for why this makes sense.
//         var sDist = _origin.To(sMidpoint).SquaredNorm2D();
         var srange = new IntervalRange {
            Id = sRangeId,
            ThetaStart = insertionThetaLower,
            ThetaEnd = insertionThetaUpper,
            Segment = s
         };

         var splittableBeginIndexInclusive = FindOverlappingRangeIndex(insertionThetaLower, 0, true);
         var splittableEndIndexInclusive = FindOverlappingRangeIndex(insertionThetaUpper, splittableBeginIndexInclusive, false);

         // a given segment can be split into 3 at max - technically this overallocates because it's impossible
         // for two 3-splits to happen in a row. Actually, assuming no overlaps one can only really produce
         // # splittables + 2 total new segments (new segments on left/right side).
         var n = new IntervalRange[(splittableEndIndexInclusive - splittableBeginIndexInclusive + 1) * 3];
            //new IntervalRange[(splittableEndIndexInclusive - splittableBeginIndexInclusive + 1) + 2];
         var nSize = 0;

         void EmitRange(int rangeId, ref IntLineSegment2 segment, cDouble thetaStart, cDouble thetaEnd) {
            if (thetaStart == thetaEnd) {
               return;
            }
            if (nSize > 0 && n[nSize - 1].Id == rangeId) {
               n[nSize - 1].ThetaEnd = thetaEnd;
            } else {
               n[nSize] = new IntervalRange { Id = rangeId, Segment = segment, ThetaStart = thetaStart, ThetaEnd = thetaEnd };
               nSize++;
            }
         }

         // near and far unioned must cover thetaUpper
         void HandleNearFarSplit(IntervalRange nearRange, IntervalRange farRange, cDouble thetaLower, cDouble thetaUpper) {
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

            // case: near covers left of range (as in, covers the lower thetas of range)
            if (nearRange.ThetaStart <= thetaLower && nearRange.ThetaEnd < thetaUpper) {
               EmitRange(nearRange.Id, ref nearRange.Segment, thetaLower, nearRange.ThetaEnd);
               EmitRange(farRange.Id, ref farRange.Segment, nearRange.ThetaEnd, thetaUpper);
               return;
               //               return new[] {
               //                  new IntervalRange { Id = nearRange.Id, ThetaStart = thetaLower, ThetaEnd = nearRange.ThetaEnd, Segment = nearRange.Segment },
               //                  new IntervalRange { Id = farRange.Id, ThetaStart = nearRange.ThetaEnd, ThetaEnd = thetaUpper, Segment = farRange.Segment }
               //               };
            }

            // case: near covers right of range
            if (nearRange.ThetaStart > thetaLower && thetaUpper <= nearRange.ThetaEnd) {
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

            if (range.Id == RANGE_ID_INFINITELY_FAR) {
               HandleNearFarSplit(srange, range, range.ThetaStart, range.ThetaEnd);
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
                  var directionToLower = DoubleVector2.FromRadiusAngle(PlayOn.CDoubleMath.c1, thetaIntersect - PiDiv2);
                  var vsxy = s.First.To(s.Second).ToDoubleVector2().ToUnit();
                  var vrsxy = rsxy.First.To(rsxy.Second).ToDoubleVector2().ToUnit();
                  var lvsxy = vsxy.ProjectOntoComponentD(directionToLower) > PlayOn.CDoubleMath.c0 ? vsxy : PlayOn.CDoubleMath.cNeg1 * vsxy;
                  var lvrsxy = vrsxy.ProjectOntoComponentD(directionToLower) > PlayOn.CDoubleMath.c0 ? vrsxy : PlayOn.CDoubleMath.cNeg1 * vrsxy;
                  var originToIntersect = _origin.To(intersection);
                  var clvsxy = lvsxy.ProjectOntoComponentD(originToIntersect);
                  var clvrsxy = lvrsxy.ProjectOntoComponentD(originToIntersect);
                  var isInserteeNearerAtLower = clvsxy < clvrsxy;
//                  Console.WriteLine("IINAL: " + isInserteeNearerAtLower);
                  if (isInserteeNearerAtLower) {
                     HandleNearFarSplit(range, srange, range.ThetaStart, thetaIntersect);
                     HandleNearFarSplit(srange, range, thetaIntersect, range.ThetaEnd);
                  } else {
                     HandleNearFarSplit(srange, range, range.ThetaStart, thetaIntersect);
                     HandleNearFarSplit(range, srange, thetaIntersect, range.ThetaEnd);
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

//            var distrsxy = range.MidpointDistanceToOriginSquared;
            bool ComputeIsInserteeNearer() {
               //range.Id != RANGE_ID_INFINITELY_FAR && (sRangeId == RANGE_ID_INFINITELY_FAR || _segmentComparer.Compare(s, rsxy) < 0);
               if (range.Id == RANGE_ID_INFINITELY_FAR) return true;
               if (range.Id == RANGE_ID_INFINITESIMALLY_NEAR) return false;
               if (srange.Id == RANGE_ID_INFINITELY_FAR) return false;
               if (srange.Id == RANGE_ID_INFINITESIMALLY_NEAR) return true;
               return _segmentComparer.Compare(s, rsxy) < 0;
            }

            bool inserteeNearer = ComputeIsInserteeNearer();
            var nearRange = inserteeNearer ? srange : range;
            var farRange = inserteeNearer ? range : srange;
            if (furthestSegmentWins) {
               (nearRange, farRange) = (farRange, nearRange);
            }
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
         if (result[result.Length - 1].ThetaEnd != TwoPi) throw new InvalidStateException();
         _intervalRanges = result;
      }

      // inclusiveRangeStart: given insertion range r, its start is inclusive while its end is exclusive,
      // so if insertee start/end == split candidate range start, varying behavior.
      private int FindOverlappingRangeIndex(cDouble theta, int lowerInitInclusive, bool inclusiveRangeStart) {
         if (theta == PlayOn.CDoubleMath.c0) {
            Debug.Assert(inclusiveRangeStart);
            return 0;
         } else if (theta == TwoPi) {
            return _intervalRanges.Length - 1;
         }

         var lowerInclusive = lowerInitInclusive;
         var upperExclusive = _intervalRanges.Length;
         while (lowerInclusive != upperExclusive) {
            var mid = lowerInclusive + (upperExclusive - lowerInclusive) / 2;
            ref var item = ref _intervalRanges[mid];
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

      private bool IsRangeOverlap(cDouble aStart, cDouble aEnd, cDouble bStart, cDouble bEnd) {
         return !(bEnd < aStart || aEnd < bStart);
      }

      private cDouble FindXYRadiansRelativeToOrigin(cDouble x, cDouble y) => FindXYRadiansRelativeToOrigin(_origin, x, y);

      // from [0, 2pi)
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static cDouble FindXYRadiansRelativeToOrigin(DoubleVector2 origin, cDouble x, cDouble y) {
         var dx = x - origin.X;
         var dy = y - origin.Y;
         //         var r = Math.Atan2(dy, dx);

#if use_fixed
         var r = CDoubleMath.Atan2(dy, dx);
#else
         var r = FastAtan2(dy, dx);
         //         Console.WriteLine(Math.Atan2(1.0, 0.0));
         //         Console.WriteLine(FastAtan2(1.0, 0.0));
#endif
         return r >= PlayOn.CDoubleMath.c0 ? r : r + TwoPi;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static cDouble FindXYRadiansRelativeToOrigin(IntVector2 origin, cDouble x, cDouble y) {
         var dx = x - (cDouble)origin.X;
         var dy = y - (cDouble)origin.Y;
         //         var r = Math.Atan2(dy, dx);
         var r = FastAtan2(dy, dx);

         //         Console.WriteLine(Math.Atan2(1.0, 0.0));
         //         Console.WriteLine(FastAtan2(1.0, 0.0));
         //         while (true) ;
         return r >= PlayOn.CDoubleMath.c0 ? r : r + TwoPi;
      }

      // https://math.stackexchange.com/questions/1098487/atan2-faster-approximation
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      private static cDouble FastAtan2(cDouble y, cDouble x) {
         var ax = PlayOn.CDoubleMath.Abs(x);
         var ay = PlayOn.CDoubleMath.Abs(y);

         // a:= min(| x |, | y |) / max(| x |, | y |)
         var a = ax < ay ? (ax / ay) : (ay / ax);

         // s:= a * a
         var s = a * a;

         // r:= ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a
         var r = (((cDouble)(-0.0464964749) * s + (cDouble)0.15931422) * s - (cDouble)0.327622764) * s * a + a;

         // if | y | > | x | then r:= 1.57079637 - r
         if (ay > ax) r = (cDouble)1.57079637 - r;

         // if x < 0 then r := 3.14159274 - r
         if (x < PlayOn.CDoubleMath.c0) r = (cDouble)3.14159274 - r;

         // if y < 0 then r := -r
         if (y < PlayOn.CDoubleMath.c0) r = -r;

         return r;
      }

      public IntervalRange[] Get() => _intervalRanges;

      public IntervalRange Stab(IntVector2 reference) => Stab(FindXYRadiansRelativeToOrigin((cDouble)reference.X, (cDouble)reference.Y));
      public IntervalRange Stab(DoubleVector2 reference) => Stab(FindXYRadiansRelativeToOrigin((cDouble)reference.X, (cDouble)reference.Y));
      public IntervalRange Stab(cDouble theta) => _intervalRanges[FindOverlappingRangeIndex(theta, 0, true)];
      public ref IntervalRange RefStab(cDouble theta) => ref _intervalRanges[FindOverlappingRangeIndex(theta, 0, true)];

      public (int startIndexInclusive, int endIndexInclusive)[] RangeStab(IntLineSegment2 s) {
         return RangeStab(new DoubleLineSegment2(s.First.ToDoubleVector2(), s.Second.ToDoubleVector2()));
      }

      public (int startIndexInclusive, int endIndexInclusive)[] RangeStab(DoubleLineSegment2 s) {
         var theta1 = FindXYRadiansRelativeToOrigin(s.First.X, s.First.Y);
         var theta2 = FindXYRadiansRelativeToOrigin(s.Second.X, s.Second.Y);

         var thetaLower = theta1 < theta2 ? theta1 : theta2;
         var thetaUpper = theta1 < theta2 ? theta2 : theta1;

         if (PlayOn.CDoubleMath.Abs(theta1 - theta2) > PlayOn.CDoubleMath.Pi) {
            // covered angle range wraps around through theta=0.
            return new [] {
               RangeStabInternal(PlayOn.CDoubleMath.c0, thetaLower),
               RangeStabInternal(thetaUpper, PlayOn.CDoubleMath.TwoPi)
            };
         } else {
            return new[] {
               RangeStabInternal(thetaLower, thetaUpper)
            };
         }
      }

      private (int, int) RangeStabInternal(cDouble thetaLower, cDouble thetaUpper) {
         if (thetaLower == thetaUpper) {
            var index = FindOverlappingRangeIndex(thetaLower, 0, true);
            return (index, index);
         }

         var queryRangeBeginIndexInclusive = FindOverlappingRangeIndex(thetaLower, 0, true);
         var queryRangeEndIndexInclusive = FindOverlappingRangeIndex(thetaUpper, queryRangeBeginIndexInclusive, false);
         return (queryRangeBeginIndexInclusive, queryRangeEndIndexInclusive);
      }

      public bool IsPartiallyVisible(DoubleLineSegment2 segment) {
         if (GeometryOperations.Clockness(segment.First, segment.Second, _origin) == Clockness.Neither) {
            return true;
         }

         var ranges = RangeStab(segment);
         foreach (var (startInclusive, endInclusive) in ranges) {
            for (var i = startInclusive; i <= endInclusive; i++) {
               ref var s = ref _intervalRanges[i].Segment;
               var comparison = OverlappingIntSegmentOriginDistanceComparator.Compare(
                  _origin,
                  segment,
                  new DoubleLineSegment2(
                     s.First.ToDoubleVector2(),
                     s.Second.ToDoubleVector2()));
               if (comparison <= 0) {
                  return true;
               }
            }
         }
         return false;
      }

      public bool Contains(IntVector2 p) {
         if (p.ToDoubleVector2() == _origin) return true;
         var theta = FindXYRadiansRelativeToOrigin((cDouble)p.X, (cDouble)p.Y);
         ref var rangeAtTheta = ref RefStab(theta);
         if (rangeAtTheta.Id == RANGE_ID_INFINITELY_FAR) {
            return false;
         }
         ref var s = ref rangeAtTheta.Segment;
         return GeometryOperations.Clockness(s.X1, s.Y1, s.X2, s.Y2, p.X, p.Y) != Clockness.ClockWise;
      }

      public bool Contains(DoubleVector2 p) {
         if (p == _origin) return true;
         var theta = FindXYRadiansRelativeToOrigin(p.X, p.Y);
         ref var rangeAtTheta = ref RefStab(theta);
         if (rangeAtTheta.Id == RANGE_ID_INFINITELY_FAR) {
            return false;
         }
         ref var s = ref rangeAtTheta.Segment;
         return GeometryOperations.Clockness((cDouble)s.X1, (cDouble)s.Y1, (cDouble)s.X2, (cDouble)s.Y2, p.X, p.Y) != Clockness.ClockWise;
      }

      public SectorVisibilityPolygon Clone() {
         return new SectorVisibilityPolygon(_origin, _intervalRanges, _segmentComparer);
      }

      public struct IntervalRange {
         public int Id;
         public IntLineSegment2 Segment;
         public cDouble ThetaStart; // inclusive
         public cDouble ThetaEnd; // exclusive
      }

      private class EventIndexComparer : IComparer<int> {
         private readonly (cDouble, int, bool)[] events;

         public EventIndexComparer((double, int, bool)[] events) {
            this.events = events;
         }

         public int Compare(int a, int b) {
            var res = events[a].Item1.CompareTo(events[b].Item1);
            if (res != 0) return res;
            return events[a].Item3.CompareTo(events[b].Item3);
         }
      }
      
      // IV2 origin variant doesn't have significant perf gains - overhead is largely in struct copying
      // and tree structure stuff.
      public static SectorVisibilityPolygon Create(DoubleVector2 origin, IntLineSegment2[] barriers, int eventLimit = -1) {
         var events = tlsEventBuffer.UnsafeTakeAndGive(barriers.Length * 4);
         var numEvents = 0;

         // Initialize PQ with events
         for (var i = 0; i < barriers.Length; i++) {
            var s = barriers[i];

            // for legacy reasons, flip barrier, prior we said front-faces were CCW, now they're CW.
            s = barriers[i] = new IntLineSegment2(s.Second, s.First);

            // front-face looks CCW to us
            var clk = GeometryOperations.Clockness(origin.X, origin.Y, (cDouble)s.X1, (cDouble)s.Y1, (cDouble)s.X2, (cDouble)s.Y2);
            if (clk != Clockness.CounterClockWise) {
               continue;
            }

            var id = i;
            var theta1 = (cDouble)FindXYRadiansRelativeToOrigin(origin, (cDouble)s.X1, (cDouble)s.Y1);
            var theta2 = (cDouble)FindXYRadiansRelativeToOrigin(origin, (cDouble)s.X2, (cDouble)s.Y2);

            // Even though we check clockness above, thetas can be equal because of floating point error.
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (theta1 == theta2) continue;

            // ensure theta1 < theta2
            if (theta1 > theta2) (theta1, theta2) = (theta2, theta1);

            void Enqueue((cDouble, int, bool) item) {
               // Console.WriteLine(
               //    "TASK " + item.Item1 + " " +
               //    (item.Item2 ? "ADD" : "REM") + " " +
               //    item.Item3 + " " +
               //    item.Item4);
               // var val = (item.Item3 << 1) | (item.Item2 ? 1 : 0);
               events[numEvents] = item;
               numEvents++;
            }

            if (theta2 - theta1 > PlayOn.CDoubleMath.Pi) {
               if (theta1 > PlayOn.CDoubleMath.c0) {
                  Enqueue((PlayOn.CDoubleMath.c0, id, true));
                  Enqueue((theta1, id, false));
               }
               if (theta2 < PlayOn.CDoubleMath.TwoPi) {
                  Enqueue((theta2, id, true));
                  Enqueue((PlayOn.CDoubleMath.TwoPi, id, false));
               }
            } else {
               Enqueue((theta1, id, true));
               Enqueue((theta2, id, false));
            }
         }

         var eventIndices = tlsEventOrder.UnsafeTakeAndGive(numEvents);
         Trace.Assert(eventIndices.Length >= numEvents); // try to avoid bounds check
         for (var i = 0; i < numEvents; i++) {
            eventIndices[i] = i;
         }

         Array.Sort(eventIndices, 0, numEvents, new EventIndexComparer(events));
         // Array.Sort(eventIndices, 0, numEvents, Comparer<int>.Create((a, b) => {
         //    var res = events[a].Item1.CompareTo(events[b].Item1);
         //    if (res != 0) return res;
         //    return events[a].Item3.CompareTo(events[b].Item3);
         // }));

#if DEBUG
         var temp = tlsEventBugCheck.UnsafeTakeAndGive(barriers.Length);
         Trace.Assert(temp.Length >= barriers.Length); // try to avoid bounds check
         for (var i = 0; i < barriers.Length; i++) {
            temp[i] = false;
         }
         for (var i = 0; i < numEvents; i++) {
            var item = events[eventIndices[i]];
            if (temp[item.Item2] == item.Item3) {
               throw new Exception();
            }
            temp[item.Item2] = item.Item3;
         }
#endif

         var lastTheta = PlayOn.CDoubleMath.c0;
         var segmentComparer = Comparer<IntLineSegment2>.Create((a, b) => OverlappingIntSegmentOriginDistanceComparator.Compare(origin, a, b));
         int OrderedSegmentComparison(int a, int b) => OverlappingIntSegmentOriginDistanceComparator.Compare(ref origin, ref barriers[a], ref barriers[b]);
         var orderedSegmentComparer = Comparer<int>.Create(OrderedSegmentComparison);
         //var orderedSegments = new SortedSet<int>(orderedSegmentComparer);

         // Generally segment count is quite small. We only care about the minimum (nearest) segment 
         // at a given time, not the total ordering of segments. In practice PQ is much faster than sorted set.
         // We do need to support removal - nontracking PQ (one that scans on removal) is fast enough given
         // we only have ~10 at most items in the PQ (usually more like 1-5) and we're scanning for ints.
         // Tracking PQ indices with hashset is still faster than sortedset, but slower than nontracking PQ.
         var orderedSegments = new NonTrackingRemovablePriorityQueue<int>(OrderedSegmentComparison);
         var outputRanges = new IntervalRange[numEvents + 1];

         void ValidateOrderingTransitivity(IntLineSegment2 sq) {
            var arr = orderedSegments.ToArray();
            for (var i = 0; i < arr.Length; i++) {
               for (var j = i + 1; j < arr.Length; j++) {
                  var si = barriers[arr[i]];
                  var sj = barriers[arr[j]];

                  void Assert(bool truth) {
                     if (!truth) throw new InvalidStateException();
                  }

                  var c_iq = segmentComparer.Compare(si, sq);
                  var c_qi = segmentComparer.Compare(sq, si);

                  var c_jq = segmentComparer.Compare(sj, sq);
                  var c_qj = segmentComparer.Compare(sq, sj);

                  var c_ij = segmentComparer.Compare(si, sj);
                  var c_ji = segmentComparer.Compare(sj, si);

                  var int_iq = si.Intersects(sq);
                  var int_jq = sj.Intersects(sq);
                  var int_ij = si.Intersects(sj);

                  Assert(!int_iq);
                  Assert(!int_jq);
                  Assert(!int_ij);

                  Assert(c_iq == -c_qi);
                  Assert(c_jq == -c_qj);
                  Assert(c_ij == -c_ji);

                  if (c_iq < 0 && c_qj < 0) Assert(c_ij < 0);
                  if (c_iq > 0 && c_qj > 0) Assert(c_ij > 0);
                  if (c_iq == 0 && c_qj == 0) Assert(c_ij == 0);

                  if (c_ij < 0 && c_jq < 0) Assert(c_iq < 0);
                  if (c_ij > 0 && c_jq > 0) Assert(c_iq > 0);
                  if (c_ij == 0 && c_jq == 0) Assert(c_iq == 0);

                  if (c_jq < 0 && c_qi < 0) Assert(c_ji < 0);
                  if (c_jq > 0 && c_qi > 0) Assert(c_ji > 0);
                  if (c_jq == 0 && c_qi == 0) Assert(c_ji == 0);
               }
            }
         }

         void ThrowInvalidState(IntLineSegment2 s) {
            Console.WriteLine("@" + s + " with origin " + origin + " " + FindXYRadiansRelativeToOrigin(origin, (cDouble)s.X1, (cDouble)s.Y1) + " " + FindXYRadiansRelativeToOrigin(origin, (cDouble)s.X2, (cDouble)s.Y2));
            foreach (var x in orderedSegments)
               Console.WriteLine(
                  "!!" + x + " " +
                  FindXYRadiansRelativeToOrigin(origin, (cDouble)barriers[x].X1, (cDouble)barriers[x].Y1) + " " +
                  FindXYRadiansRelativeToOrigin(origin, (cDouble)barriers[x].X2, (cDouble)barriers[x].Y2) + " " +
                  segmentComparer.Compare(s, barriers[x]) + " " + 
                  segmentComparer.Compare(barriers[x], s));
            if (DateTime.Now == default(DateTime))
               return;
            throw new InvalidStateException();
         }

         var lastOutputRangeIndex = -1;
         for (var i = 0; i < numEvents; i++) {
            if (i == eventLimit) break;

            var (theta, id, add) = events[eventIndices[i]];

            if (theta != lastTheta) {
               var nearestId = orderedSegments.Count == 0 ? RANGE_ID_INFINITELY_FAR : orderedSegments.Peek();
               if (lastOutputRangeIndex != -1 && outputRanges[lastOutputRangeIndex].Id == nearestId) {
                  // extend prev output range
                  // Console.WriteLine($"EXTEND to {theta} {id}");
                  outputRanges[lastOutputRangeIndex].ThetaEnd = theta;
               } else {
                  // emit from theta to lastTheta
                  // Console.WriteLine($"EMIT {lastTheta} to {theta} {nearestId} {nearestSegment}");
                  lastOutputRangeIndex++;
                  outputRanges[lastOutputRangeIndex] = new IntervalRange {
                     Id = nearestId,
                     Segment = nearestId == RANGE_ID_INFINITELY_FAR ? default(IntLineSegment2) : barriers[nearestId],
                     ThetaStart = lastTheta,
                     ThetaEnd = theta
                  };
               }
               lastTheta = theta;
            }

            if (add) {
               // ValidateOrderingTransitivity(seg);

               // Console.WriteLine("ADD " + theta + " " + barriers[id] + " " + id + " " + orderedSegments.Count);
               orderedSegments.Enqueue(id);
               // if (!orderedSegments.Enqueue(id))
               //    ThrowInvalidState(barriers[id]);
            } else {
               // Console.WriteLine("REM " + theta + " " + barriers[id] + " " + id + " " + orderedSegments.Count);
               if (!orderedSegments.Remove(id))
                  ThrowInvalidState(barriers[id]);
            }
         }

         if (outputRanges[lastOutputRangeIndex].ThetaEnd < TwoPi) {
            var start = outputRanges[lastOutputRangeIndex].ThetaEnd;
            lastOutputRangeIndex++;
            outputRanges[lastOutputRangeIndex] = new IntervalRange {
               Id = RANGE_ID_INFINITELY_FAR,
               Segment = default,
               ThetaStart = start,
               ThetaEnd = TwoPi
            };
         }

         var outputRangeCount = lastOutputRangeIndex + 1;
         var finalOutputRanges = new IntervalRange[outputRangeCount];
         Array.Copy(outputRanges, finalOutputRanges, outputRangeCount);
         if (finalOutputRanges[finalOutputRanges.Length - 1].ThetaEnd != TwoPi) throw new InvalidStateException();
         return new SectorVisibilityPolygon(origin, finalOutputRanges, segmentComparer);
      }
   }
}
