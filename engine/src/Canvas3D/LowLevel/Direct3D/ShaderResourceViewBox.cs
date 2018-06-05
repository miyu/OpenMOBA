using SharpDX;
using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class ShaderResourceViewBox : IShaderResourceView {
      public ShaderResourceView ShaderResourceView;

      public void MoveAssignFrom(ShaderResourceViewBox other) {
         Utilities.Dispose<ShaderResourceView>(ref ShaderResourceView);
         ShaderResourceView = other.ShaderResourceView;
         other.ShaderResourceView = null;
      }
   }
}