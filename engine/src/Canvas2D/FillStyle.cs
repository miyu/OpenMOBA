using System.Drawing;

namespace Dargon.Dviz {
   public class FillStyle {
      public FillStyle(Color? color = null) {
         Color = color ?? Color.Black;
      }

      public Color Color;
   }
}