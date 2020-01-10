using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   public static class MathExtensions {
      public static IntVector2 LossyPointAtRatio(this Rectangle r, int numx, int denomx, int numy, int denomy) {
         return new IntVector2(
            r.X + r.Width * numx / denomx,
            r.Y + r.Height * numy / denomy);
      }
   }
}
