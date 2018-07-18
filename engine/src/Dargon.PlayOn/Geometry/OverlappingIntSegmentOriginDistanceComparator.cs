using System.Collections.Generic;
#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Geometry {
   public class OverlappingIntSegmentOriginDistanceComparator : IComparer<IntLineSegment2> {
      private readonly DoubleVector2 _origin;

      public OverlappingIntSegmentOriginDistanceComparator(DoubleVector2 origin) {
         _origin = origin;
      }

      // clockness(origin, seg.first, seg.second) must be clockwise.
      public int Compare(IntLineSegment2 x, IntLineSegment2 y) {
         return Compare(_origin, x, y);
      }

      public static int Compare(IntVector2 p, IntLineSegment2 a, IntLineSegment2 b) {
#if DEBUG
         if (GeometryOperations.Clockness(p.X, p.Y, a.X1, a.Y1, a.X2, a.Y2) != Clockness.Clockwise) {
            throw new InvalidStateException();
         }
         if (GeometryOperations.Clockness(p.X, p.Y, b.X1, b.Y1, b.X2, b.Y2) != Clockness.Clockwise) {
            throw new InvalidStateException();
         }
#endif
         var clk = GeometryOperations.Clockness(p.X, p.Y, a.X1, a.Y1, b.X1, b.Y1);
         if (clk != Clockness.Clockwise) {
            // b before a; b \' a *origin
            var res = (int)GeometryOperations.Clockness(b.First, b.Second, a.First);
            if (res != 0) return res;

            // just need something to resolve ambiguity. b1 b2 a1 is collinear.
            // a1 must be within the angle b1 p b2 (see visibility polygon building algorithm)
            // so a1 is BETWEEN b1 b2. a2 cannot be collinear with b1 b2 (disallow segments intersecting
            // other than at endpoint), but still, a2 is either 'in front of' or 'behind' b.
            res = (int)GeometryOperations.Clockness(b.First, b.Second, a.Second);
#if DEBUG
            if (res == 0 && a != b) {
               throw new BadInputException();
            }
#endif
            return res;
         } else {
            // a before b; a \' b *origin
            var res = -(int)GeometryOperations.Clockness(a.First, a.Second, b.First);
            if (res != 0) return res;

            // just need something to resolve ambiguity. a1 a2 b1 is collinear.
            res = -(int)GeometryOperations.Clockness(a.First, a.Second, b.Second);
#if DEBUG
            if (res == 0 && a != b) {
               throw new BadInputException();
            }
#endif
            return res;
         }
      }

      public static int Compare(DoubleVector2 p, IntLineSegment2 a, IntLineSegment2 b) {
         return Compare(ref p, ref a, ref b);
      }

      public static int Compare(ref DoubleVector2 p, ref IntLineSegment2 a, ref IntLineSegment2 b) {
//#if DEBUG
         if (GeometryOperations.Clockness(p.X, p.Y, (cDouble)a.X1, (cDouble)a.Y1, (cDouble)a.X2, (cDouble)a.Y2) != Clockness.Clockwise) {
            throw new InvalidStateException();
         }
         if (GeometryOperations.Clockness(p.X, p.Y, (cDouble)b.X1, (cDouble)b.Y1, (cDouble)b.X2, (cDouble)b.Y2) != Clockness.Clockwise) {
            throw new InvalidStateException();
         }
//         var clocknessPA1A2 = GeometryOperations.Clockness(p.X, p.Y, a.X1, a.Y1, a.X2, a.Y2);
//         if (clocknessPA1A2 == Clockness.Neither) {
//            throw new InvalidStateException();
//         } else if (clocknessPA1A2 == Clockness.CounterClockwise) {
//            var aFlipped = new IntLineSegment2(a.Second, a.First);
//            return Compare(ref p, ref aFlipped, ref b);
//         }
//
//         var clocknessPB1B2 = GeometryOperations.Clockness(p.X, p.Y, b.X1, b.Y1, b.X2, b.Y2);
//         if (clocknessPB1B2 == Clockness.Neither) {
//            throw new InvalidStateException();
//         } else if (clocknessPB1B2 == Clockness.CounterClockwise) {
//            var bFlipped = new IntLineSegment2(b.Second, b.First);
//            return Compare(ref p, ref a, ref bFlipped);
//         }
//#endif

         var clk = GeometryOperations.Clockness(p.X, p.Y, (cDouble)a.X1, (cDouble)a.Y1, (cDouble)b.X1, (cDouble)b.Y1);
         if (clk != Clockness.Clockwise) {
            // b before a; b \' a *origin
            var res = (int)GeometryOperations.Clockness(b.First, b.Second, a.First);
            if (res != 0) return res;
            
            // just need something to resolve ambiguity. b1 b2 a1 is collinear.
            // a1 must be within the angle b1 p b2 (see visibility polygon building algorithm)
            // so a1 is BETWEEN b1 b2. a2 cannot be collinear with b1 b2 (disallow segments intersecting
            // other than at endpoint), but still, a2 is either 'in front of' or 'behind' b.
            res = (int)GeometryOperations.Clockness(b.First, b.Second, a.Second);
#if DEBUG
            if (res == 0 && a != b) {
               throw new BadInputException();
            }
#endif
            return res;
         } else {
            // a before b; a \' b *origin
            var res = -(int)GeometryOperations.Clockness(a.First, a.Second, b.First);
            if (res != 0) return res;

            // just need something to resolve ambiguity. a1 a2 b1 is collinear.
            res = -(int)GeometryOperations.Clockness(a.First, a.Second, b.Second);
#if DEBUG
            if (res == 0 && a != b) {
               throw new BadInputException();
            }
#endif
            return res;
         }
      }

      public static int Compare(DoubleVector2 p, DoubleLineSegment2 a, DoubleLineSegment2 b) {
         // TODO: Why did I comment this
//#if DEBUG
//         if (GeometryOperations.Clockness(p.X, p.Y, a.X1, a.Y1, a.X2, a.Y2) != Clockness.Clockwise) {
//            throw new InvalidStateException();
//         }
//         if (GeometryOperations.Clockness(p.X, p.Y, b.X1, b.Y1, b.X2, b.Y2) != Clockness.Clockwise) {
//            throw new InvalidStateException();
//         }
//#endif
         var clk = GeometryOperations.Clockness(p.X, p.Y, a.X1, a.Y1, b.X1, b.Y1);
         if (clk != Clockness.Clockwise) {
            // b before a; b \' a *origin
            var res = (int)GeometryOperations.Clockness(b.First, b.Second, a.First);
            if (res != 0) return res;

            // just need something to resolve ambiguity. b1 b2 a1 is collinear.
            // a1 must be within the angle b1 p b2 (see visibility polygon building algorithm)
            // so a1 is BETWEEN b1 b2. a2 cannot be collinear with b1 b2 (disallow segments intersecting
            // other than at endpoint), but still, a2 is either 'in front of' or 'behind' b.
            res = (int)GeometryOperations.Clockness(b.First, b.Second, a.Second);
#if DEBUG
            if (res == 0 && a != b) {
               throw new BadInputException();
            }
#endif
            return res;
         } else {
            // a before b; a \' b *origin
            var res = -(int)GeometryOperations.Clockness(a.First, a.Second, b.First);
            if (res != 0) return res;

            // just need something to resolve ambiguity. a1 a2 b1 is collinear.
            res = -(int)GeometryOperations.Clockness(a.First, a.Second, b.Second);
#if DEBUG
            if (res == 0 && a != b) {
               throw new BadInputException();
            }
#endif
            return res;
         }
      }
   }
}