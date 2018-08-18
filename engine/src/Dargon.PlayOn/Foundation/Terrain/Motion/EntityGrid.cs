using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Foundation.ECS;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Overlay;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

namespace Dargon.PlayOn.Foundation.Terrain.Motion {
   public class EntityGrid {
      private readonly SectorNodeDescription sectorNodeDescription;
      private readonly int cellSize;
      private readonly int width;
      private readonly int height;
      private readonly Link[,] cells;

      internal EntityGrid(SectorNodeDescription sectorNodeDescription, int cellSize, int width, int height, Link[,] cells) {
         this.sectorNodeDescription = sectorNodeDescription;
         this.cellSize = cellSize;
         this.width = width;
         this.height = height;
         this.cells = cells;
         this.Bounds = sectorNodeDescription.StaticMetadata.LocalBoundary;
      }

      public SectorNodeDescription SectorNodeDescription => sectorNodeDescription;
      public int CellSize => cellSize;
      public int Width => width;
      public int Height => height;
      public Link[,] Cells => cells;
      public Rectangle Bounds { get; }

      public EnumeratorToEnumerableAdapter<Entity, Enumerator> View(int cx, int cy, (int, int, int)[] ranges) {
         return EnumeratorToEnumerableAdapter<Entity>.Create(new Enumerator(this, cx, cy, ranges));
      }

      public class Link {
         public Entity Entity;
         public Link Next;
      }

      public struct Enumerator : IEnumerator<Entity>, IEnumerator {
         private readonly EntityGrid grid;
         private readonly int cx;
         private readonly int cy;
         private readonly (int offsetTop, int offsetLeft, int width)[] ranges;
         private int ity;
         private int itx;
         private Entity current;
         private Link link;

         public Enumerator(EntityGrid grid, int cx, int cy, (int offsetTop, int offsetLeft, int width)[] ranges) {
            this.grid = grid;
            this.cx = cx;
            this.cy = cy;
            this.ranges = ranges;
            this.ity = 0;
            this.itx = 0;
            this.current = null;
            this.link = null;
         }

         public void Dispose() => Reset();

         public bool MoveNext() {
            while (true) {
               // If we're on a link, traverse it.
               if (link != null) {
                  current = link.Entity;
                  link = link.Next;
                  return true;
               }

               // Else, if we're not on a link and at the end, we done.
               if (ity == ranges.Length) return false;

               // Otherwise, try current cell and advance cursor to next cell.
               Assert.IsNull(link);

               var range = ranges[ity];
               var yIndex = cy + range.offsetTop;
               var xIndex = cx + range.offsetLeft + itx;

               // Validate Y valid. If not, skip.
               if (yIndex < 0 || yIndex >= grid.height) {
                  ity++;
                  itx = 0;
                  continue;
               }

               if (xIndex >= grid.width) {
                  // If x overshot right, bump ity (take on next iteration).
                  ity++;
                  itx = 0;
                  continue;
               } else if (xIndex < 0) {
                  // If xIndex below 0, advance to first valid position (sampled on next iteration)
                  // If itx goes beyond valid value, itx gets reset and ity gets bumped below.
                  itx -= xIndex;
               } else {
                  link = grid.cells[yIndex, xIndex];
                  itx++;
               }

               if (itx >= range.width) {
                  ity++;
                  itx = 0;
               }
            }
         }

         public void Reset() {
            ity = 0;
            itx = 0;
            current = null;
            link = null;
         }

         public Entity Current {
            get {
               if (this.itx == 0 && this.ity == 0)
                  throw new IndexOutOfRangeException();
               return current;
            }
         }

         object IEnumerator.Current => current;
      }
   }

   public class EntityGridRangeCalculator {
      private readonly Dictionary<(int, int), (int, int, int)[]> circleRangeCache = new Dictionary<(int, int), (int, int, int)[]>();
      private readonly Dictionary<(int, int, bool), (int, int, int)[]> quarterCircleBottomRightRangeCache = new Dictionary<(int, int, bool), (int, int, int)[]>();

      public (int, int, int)[] ComputeCircleRange(int agentRadius, int cellSize) {
         const int precision = 100;
         const int precisionSquared = precision * precision;

         if (circleRangeCache.TryGetValue((agentRadius, cellSize), out var res)) {
            return res;
         }

         var r = ComputeCellSpanRadius(agentRadius, cellSize);
         var rSquared = r * r;
         res = new(int, int, int)[r * 2 + 1];
         for (int y = -r, i = 0; y <= r; y++, i++) {
            // r^2 = x^2 + y^2; x^2 = r^2 - y^2
            // r^2 = x^2 + y^2; precision^2 x^2 = precision^2 r^2 - precision^2 y^2
            var xRange = IntMath.DivRoundUp(IntMath.Sqrt(precisionSquared * (rSquared - y * y)), precision);
            res[i] = (y, -xRange, xRange * 2 + 1);
         }
         return circleRangeCache[(agentRadius, cellSize)] = res;
      }

      public (int, int, int)[] ComputeQuarterCircleBottomRightRange(int agentRadius, int cellSize, bool includeCenter) {
         const int precision = 100;
         const int precisionSquared = precision * precision;

         if (quarterCircleBottomRightRangeCache.TryGetValue((agentRadius, cellSize, includeCenter), out var res)) {
            return res;
         }

         var r = ComputeCellSpanRadius(agentRadius, cellSize);
         var rSquared = r * r;
         res = new(int, int, int)[r + 1];
         for (int y = 0; y <= r; y++) {
            // r^2 = x^2 + y^2; x^2 = r^2 - y^2
            // r^2 = x^2 + y^2; precision^2 x^2 = precision^2 r^2 - precision^2 y^2
            var xRange = IntMath.DivRoundUp(IntMath.Sqrt(precisionSquared * (rSquared - y * y)), precision);
            var sx = (y == 0 && !includeCenter) ? 1 : 0;
            res[y] = (y, sx, xRange + 1 - sx);
         }
         return quarterCircleBottomRightRangeCache[(agentRadius, cellSize, includeCenter)] = res;
      }

      // i.e. if only one cell, then r=0. If 5 cell +, then r = 1
      private int ComputeCellSpanRadius(int radius, int cellSize) {
         return IntMath.DivRoundUp(radius, cellSize);
      }
   }

   public class EntityGridView {
      private readonly EntityGrid grid;
      private readonly EntityGridRangeCalculator rangeCalculator;

      public EntityGridView(EntityGrid grid, EntityGridRangeCalculator rangeCalculator) {
         this.grid = grid;
         this.rangeCalculator = rangeCalculator;
      }

      public EntityGrid Grid => grid;

      public EnumeratorToEnumerableAdapter<Entity, EntityGrid.Enumerator> InCircle(int cx, int cy, int agentRadius) {
         return grid.View(cx, cy, rangeCalculator.ComputeCircleRange(agentRadius, grid.CellSize));
      }

      public EnumeratorToEnumerableAdapter<Entity, EntityGrid.Enumerator> InQuarterCircleBR(int cx, int cy, int agentRadius) {
         return grid.View(cx, cy, rangeCalculator.ComputeQuarterCircleBottomRightRange(agentRadius, grid.CellSize, true));
      }

      public EnumeratorToEnumerableAdapter<Entity, EntityGrid.Enumerator> InQuarterCircleBRExcludeCenter(int cx, int cy, int agentRadius) {
         return grid.View(cx, cy, rangeCalculator.ComputeQuarterCircleBottomRightRange(agentRadius, grid.CellSize, false));
      }

      private void LocalToCell(int lx, int ly, out int cx, out int cy) {
         cx = (lx - grid.Bounds.Left) / grid.Bounds.Width;
         cy = (ly - grid.Bounds.Top) / grid.Bounds.Height;
      }
   }

   public class EntityGridFacade {
      private readonly EntityGridRangeCalculator rangeCalculator;

      public EntityGridFacade(EntityGridRangeCalculator rangeCalculator) {
         this.rangeCalculator = rangeCalculator;
      }

      public Dictionary<SectorNodeDescription, EntityGrid> CreateGrids(Entity[] entities) => GridCreationHelper(entities);

      public EntityGridView CreateGridView(EntityGrid grid) => new EntityGridView(grid, rangeCalculator);

      public Dictionary<SectorNodeDescription, EntityGridView> CreateGridViews(Dictionary<SectorNodeDescription, EntityGrid> grids)
         => grids.Map(CreateGridView);

      public Dictionary<SectorNodeDescription, EntityGridView> CreateGridViews(Entity[] entities)
         => CreateGridViews(CreateGrids(entities));

      private static Dictionary<SectorNodeDescription, EntityGrid> GridCreationHelper(Entity[] allEntities) {
         var sndToEntities = new ExposedArrayListMultiValueDictionary<SectorNodeDescription, Entity>();
         foreach (var entity in allEntities) {
            ref var mci = ref entity.MotionComponent.Internals;
            var snd = mci.Localization.TerrainOverlayNetworkNode.SectorNodeDescription;
            sndToEntities.Add(snd, entity);
         }

         var res = new Dictionary<SectorNodeDescription, EntityGrid>();
         foreach (var (snd, entities) in sndToEntities) {
            var bounds = snd.StaticMetadata.LocalBoundary;
            var cellSize = Math.Min(Math.Min((int)Math.Ceiling(snd.WorldToLocalScalingFactor * 100), bounds.Width / 100), bounds.Height / 100);
            var w = IntMath.DivRoundUp(bounds.Width, cellSize);
            var h = IntMath.DivRoundUp(bounds.Height, cellSize);
            var cells = new EntityGrid.Link[h, w];
            foreach (var entity in entities) {
               var p = entity.MotionComponent.Internals.Localization.LocalPositionIv2;
               var x = (p.X - bounds.Left) / cellSize;
               var y = (p.Y - bounds.Top) / cellSize;
               cells[y, x] = new EntityGrid.Link {
                  Entity = entity,
                  Next = cells[y, x]
               };
            }
            res[snd] = new EntityGrid(snd, cellSize, w, h, cells);
         }
         return res;
      }
   }
}
