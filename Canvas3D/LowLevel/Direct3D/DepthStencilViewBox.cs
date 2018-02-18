using System.Drawing;
using SharpDX;
using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class DepthStencilViewBox : IDepthStencilView {
      public DepthStencilView DepthStencilView;
      public Size Resolution { get; set; }

      public void MoveAssignFrom(DepthStencilViewBox other) {
         Utilities.Dispose<DepthStencilView>(ref DepthStencilView);
         DepthStencilView = other.DepthStencilView;
         other.DepthStencilView = null;
         Resolution = other.Resolution;
         other.Resolution = new Size(-1, -1);
      }
   }
}