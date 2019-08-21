using System.Drawing;
using SharpDX;
using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class RenderTargetViewBox : IRenderTargetView {
      public RenderTargetView RenderTargetView;
      public Size Resolution { get; set; }

      public void MoveAssignFrom(RenderTargetViewBox other) {
         Utilities.Dispose<RenderTargetView>(ref RenderTargetView);
         RenderTargetView = other.RenderTargetView;
         other.RenderTargetView = null;
         Resolution = other.Resolution;
         other.Resolution = new Size(-1, -1);
      }
   }
}