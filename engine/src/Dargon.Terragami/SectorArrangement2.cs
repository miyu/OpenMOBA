using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Commons.Comparers;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;
using SharpDX;
using cDouble = System.Double;

namespace Dargon.Terragami {
   public class SectorArrangement2 {
      // public class ArrangementEvent {
      //    public DoubleVector2 Point;
      // }

      // public class VerticalEvent : ArrangementEvent {
      //    public VerticalEvent(cDouble x, int lineIndex) {
      //       Point = new DoubleVector2(x, float.NegativeInfinity);
      //       LineIndex = lineIndex;
      //    }
      //
      //    public int LineIndex;
      // }
      //
      // public class JoinEvent : ArrangementEvent {
      //    public JoinEvent(DoubleVector2 p, int[] lineIndices) {
      //       Point = p;
      //       LineIndices = lineIndices;
      //    }
      //
      //    public DoubleVector2 Point;
      //    public int[] LineIndices;
      // }

      public class State {
         public DoubleLineSegment2[] Lines;
         public PriorityQueue<ArrangementEvent> EventQueue;
         public int[] OrderedNonverticalIndices;

         public List<ArrangementCell> AllCells;
         public ArrangementCell[] ActiveCells;
         public int[] LineIndexToOrderedNonverticalIndicesIndex;
      }

      public class ArrangementEvent {
         public cDouble X;
         public int[] IntersectingNonverticalLineIndices;
         public int[] VerticalLineIndices;
      }

      public static SectorArrangement Create(DoubleLineSegment2[] lines, AxisAlignedBoundingBox2 bounds, IDebugCanvas debugCanvas) {
         var (state, initialSweepX, initialOrderedNonverticalYs) = InitState(lines, bounds);

         StartNewCellsAtX(state, initialSweepX, initialOrderedNonverticalYs);

         // Sweep through event queue
         var sweepX = initialSweepX;
         while (!state.EventQueue.IsEmpty) {
            var e = state.EventQueue.Dequeue();
            sweepX = e.X;

            var hasVerticalSplit = e.VerticalLineIndices.Length != 0;
            var hasIntersectingNonverticals = e.IntersectingNonverticalLineIndices.Length != 0;
            var orderedNonverticalYsAtX = hasVerticalSplit
               ? state.OrderedNonverticalIndices.Map(li => state.Lines[li].PointAtX(sweepX).Y)
               : null;

            if (hasVerticalSplit) {
               TerminateActiveCellsAtX(state, sweepX, orderedNonverticalYsAtX);
            }

            if (hasIntersectingNonverticals) {
               ProcessJoinEvent(state, e.IntersectingNonverticalLineIndices, hasVerticalSplit);
            }

            if (hasVerticalSplit) {
               StartNewCellsAtX(state, sweepX, orderedNonverticalYsAtX);
            }
         }

         // terminate
         var finalSweepX = sweepX + 10;
         var orderedNonverticalYsAtFinalSweepX = state.OrderedNonverticalIndices.Map(li => state.Lines[li].PointAtX(finalSweepX).Y);
         TerminateActiveCellsAtX(state, finalSweepX, orderedNonverticalYsAtFinalSweepX);

         return null;
      }

      private static (State state, double sweepX, double[] orderedNonverticalYs) InitState(DoubleLineSegment2[] lines, AxisAlignedBoundingBox2 bounds) {
         var boundsLeftX = bounds.Center.X - bounds.Extents.X;
         var boundsRightX = bounds.Center.X + bounds.Extents.X;

         var verticalLineIndicesByX = new ListMultiValueDictionary<cDouble, int>();

         // Find nonverticals & throw verticals into event queue
         var verticalIndices = new List<int>();
         var nonverticalIndices = new List<int>();
         for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
            var line = lines[lineIndex];

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            var isVertical = line.X1 == line.X2;

            if (isVertical) {
               if (line.X1 < boundsLeftX || line.X1 > boundsRightX) continue; // outside area of interest
               verticalLineIndicesByX.Add(line.X1, lineIndex);
               verticalIndices.Add(lineIndex);
            } else {
               nonverticalIndices.Add(lineIndex);
            }
         }

         // Throw intersections into intersection queue
         // Note: Output-dependent algos exist. Didn't bother for initial implementation.
         var intersectionQueueComparer = Comparer<(DoubleVector2 p, int li0, int li1)>.Create(
            (a, b) => {
               var res = a.p.X.CompareTo(b.p.X);
               if (res != 0) return res;
               return a.p.Y.CompareTo(b.p.Y);
            });

         var intersectionQueue = new PriorityQueue<(DoubleVector2 p, int li0, int li1)>(intersectionQueueComparer);
         for (var a = 0; a < nonverticalIndices.Count; a++) {
            var indexA = nonverticalIndices[a];
            var lineA = lines[indexA];

            for (var b = a + 1; b < nonverticalIndices.Count; b++) {
               var indexB = nonverticalIndices[b];
               var lineB = lines[indexB];

               if (GeometryOperations.TryFindLineLineIntersection(lineA.First, lineA.Second, lineB.First, lineB.Second, out var p)) {
                  if (p.X < boundsLeftX || p.X > boundsRightX) continue; // outside area of interest

                  Trace.Assert(GeometryOperations.IsReal(p));
                  intersectionQueue.Enqueue((p, indexA, indexB));
               }
            }
         }

         // Process intersection queue to emit line join events (multiple lines can join at the same point)
         var verticalLineXsAndIndices = verticalLineIndicesByX.ToArray();
         Array.Sort(verticalLineXsAndIndices, (a, b) => a.Key.CompareTo(b.Key));
         var nextVerticalLineIndex = 0;

         var eventQueue = new PriorityQueue<ArrangementEvent>((a, b) => a.X.CompareTo(b.X));
         var queuedVerticalXs = new HashSet<cDouble>();
         while (!intersectionQueue.IsEmpty) {
            var item = intersectionQueue.Dequeue();

            var intersectingNonverticalLineIndices = new HashSet<int>();
            intersectingNonverticalLineIndices.Add(item.li0);
            intersectingNonverticalLineIndices.Add(item.li1);

            var tol = 0.00001;
            while (!intersectionQueue.IsEmpty && Math.Abs(intersectionQueue.Peek().p.X - item.p.X) < tol) {
               var add = intersectionQueue.Dequeue();
               intersectingNonverticalLineIndices.Add(add.li0);
               intersectingNonverticalLineIndices.Add(add.li1);
            }

            var verticalLineIndices = new HashSet<int>();
            while (nextVerticalLineIndex < verticalLineXsAndIndices.Length && item.p.X > verticalLineXsAndIndices[nextVerticalLineIndex].Key + tol) {
               nextVerticalLineIndex++;
            }

            while (nextVerticalLineIndex < verticalLineXsAndIndices.Length && Math.Abs(verticalLineXsAndIndices[nextVerticalLineIndex].Key - item.p.X) < tol) {
               foreach (var li in verticalLineXsAndIndices[nextVerticalLineIndex].Value) {
                  verticalLineIndices.Add(li);
               }

               queuedVerticalXs.Add(verticalLineXsAndIndices[nextVerticalLineIndex].Key);
               nextVerticalLineIndex++;
            }

            eventQueue.Enqueue(new ArrangementEvent {
               X = item.p.X,
               IntersectingNonverticalLineIndices = intersectingNonverticalLineIndices.ToArray(),
               VerticalLineIndices = verticalLineIndices.ToArray(),
            });
         }

         // Add vertical arrangement events with no intersections
         foreach (var (vlx, vlindices) in verticalLineXsAndIndices) {
            if (queuedVerticalXs.Contains(vlx)) continue;
            eventQueue.Enqueue(new ArrangementEvent {
               X = vlx,
               IntersectingNonverticalLineIndices = Array.Empty<int>(),
               VerticalLineIndices = vlindices.ToArray(),
            });
         }

         // Determine initial sweepline x & initial ordering of lines
         var sweepX = boundsLeftX;
         var initialLineIndicesAndYs = nonverticalIndices.Map(li => (li, lines[li].PointAtX(sweepX).Y));
         Array.Sort(initialLineIndicesAndYs, (a, b) => a.Y.CompareTo(b.Y));

         var orderedNonverticalIndices = initialLineIndicesAndYs.Map(item => item.li);
         var orderedNonverticalYs = initialLineIndicesAndYs.Map(item => item.Y);

         // Track the index of a line to its index in the collection of ordered nonvertical indices.
         var lineIndexToOrderedNonverticalIndicesIndex = new int[lines.Length];
         for (var i = 0; i < orderedNonverticalIndices.Length; i++) {
            lineIndexToOrderedNonverticalIndicesIndex[orderedNonverticalIndices[i]] = i;
         }

         foreach (var verticalIndex in verticalIndices) {
            lineIndexToOrderedNonverticalIndicesIndex[verticalIndex] = -1;
         }

         var state = new State {
            EventQueue = eventQueue,
            Lines = lines,
            OrderedNonverticalIndices = orderedNonverticalIndices,
            AllCells = new List<ArrangementCell>(),
            ActiveCells = new ArrangementCell[nonverticalIndices.Count + 1],
            LineIndexToOrderedNonverticalIndicesIndex = lineIndexToOrderedNonverticalIndicesIndex,
         };
         return (state, sweepX, orderedNonverticalYs);
      }

      private static void TerminateActiveCellsAtX(State state, cDouble sweepX, cDouble[] orderedNonverticalYs) {
         for (var i = 0; i < orderedNonverticalYs.Length; i++) {
            state.ActiveCells[i].Right.Add((new DoubleVector2(sweepX, orderedNonverticalYs[i]), -1337));
            state.ActiveCells[i + 1].Left.Add((new DoubleVector2(sweepX, orderedNonverticalYs[i]), -1337));
         }

         for (var i = 0; i < state.ActiveCells.Length; i++) {
            state.ActiveCells[i] = null;
         }
      }

      private static void StartNewCellsAtX(State state, cDouble sweepX, cDouble[] orderedNonverticalYs) {
         var orderedNonverticalIndices = state.OrderedNonverticalIndices;
         for (var i = 0; i <= orderedNonverticalIndices.Length; i++) {
            var cell = new ArrangementCell();

            if (i != 0) {
               var leftNonverticalIndex = orderedNonverticalIndices[i - 1];
               cell.Left = new List<(DoubleVector2, int)> { (new DoubleVector2(sweepX, orderedNonverticalYs[i - 1]), leftNonverticalIndex) };
            }

            if (i != orderedNonverticalIndices.Length) {
               var rightNonverticalIndex = orderedNonverticalIndices[i];
               cell.Right = new List<(DoubleVector2, int)> { (new DoubleVector2(sweepX, orderedNonverticalYs[i]), rightNonverticalIndex) };
            }

            state.AllCells.Add(cell);
            state.ActiveCells[i] = cell;
         }
      }

      private static void ProcessJoinEvent(State state, int[] joinedLineIndices, bool isAlsoVerticalSplitEvent) {
         // todo: rename this to IntervalSetUInt32, cleanup
         var oniIndices = new UniqueIdentificationSet(false);
         
         foreach (var li in joinedLineIndices) {
            var oniIndex = state.LineIndexToOrderedNonverticalIndicesIndex[li];
            oniIndices.GiveUniqueID((uint)oniIndex);
         }

         LinkedList<UniqueIdentificationSet.Segment> ranges = null;
         oniIndices.__Access(ll => ranges = ll);

         foreach (var range in ranges) {
            // indices of segments that're converging to a point
            // '-. left
            //    '-.   y+ ↓
            // ------>x
            //    .-' 
            // .-' right

            var oniIndexLowInclusive = (int)range.low;
            var oniIndexHighInclusive = (int)range.high;

            if (isAlsoVerticalSplitEvent) {
               // oni line order reverses at the intersection point (left points go to right, right points go to left)
               Array.Reverse(state.OrderedNonverticalIndices, oniIndexLowInclusive, oniIndexHighInclusive - oniIndexLowInclusive + 1);

               for (var i = oniIndexLowInclusive; i <= oniIndexHighInclusive; i++) {
                  state.LineIndexToOrderedNonverticalIndicesIndex[state.OrderedNonverticalIndices[i]] = i;
               }
            } else {
               // find intersection point (todo: use value from intersection event)
               var l0 = state.Lines[state.OrderedNonverticalIndices[oniIndexLowInclusive]];
               var l1 = state.Lines[state.OrderedNonverticalIndices[oniIndexHighInclusive]];

               var success = GeometryOperations.TryFindLineLineIntersection(l0.First, l0.Second, l1.First, l1.Second, out var intersect);
               Assert.IsTrue(success);

               // update cells left/right the intersection point.
               state.ActiveCells[oniIndexLowInclusive].Right.Add((intersect, -300));
               state.ActiveCells[oniIndexHighInclusive + 1].Left.Add((intersect, -400));

               // close cells whose left/right chains are converging at the intersection point.
               for (var i = oniIndexLowInclusive; i < oniIndexHighInclusive; i++) {
                  var cell = state.ActiveCells[i];
                  cell.Left.Add((intersect, -100));
                  cell.Right.Add((intersect, -100));
                  state.ActiveCells[i] = null;
               }

               // oni line order reverses at the intersection point (left points go to right, right points go to left)
               Array.Reverse(state.OrderedNonverticalIndices, oniIndexLowInclusive, oniIndexHighInclusive - oniIndexLowInclusive + 1);

               for (var i = oniIndexLowInclusive; i <= oniIndexHighInclusive; i++) {
                  state.LineIndexToOrderedNonverticalIndicesIndex[state.OrderedNonverticalIndices[i]] = i;
               }

               // open cells whose left/right chains begin at the intersection point
               for (var i = oniIndexLowInclusive; i < oniIndexHighInclusive; i++) {
                  var leftLi = state.OrderedNonverticalIndices[i];
                  var rightLi = state.OrderedNonverticalIndices[i + 1];

                  var cell = new ArrangementCell();
                  cell.Left = new List<(DoubleVector2, int)> { (intersect, -200) };
                  cell.Right = new List<(DoubleVector2, int)> { (intersect, -200) };

                  state.ActiveCells[i + 1] = cell;
                  state.AllCells.Add(cell);
               }
            }
         }
      }
   }

   public class ArrangementCell {
      public List<(DoubleVector2, int)> Left;
      public List<(DoubleVector2, int)> Right;
   }
}
