using System.Drawing;

namespace Dargon.PlayOn.DevTool.Debugging {
   public class FillStyle {
      public FillStyle(Color? color = null) {
         Color = color ?? Color.Black;
      }

      public Color Color;
   }
}