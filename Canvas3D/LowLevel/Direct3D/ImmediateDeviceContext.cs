using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Canvas3D.LowLevel.Direct3D {
   public class ImmediateDeviceContext : BaseDeviceContext, IImmediateDeviceContext {
      private readonly SwapChain _swapChain;
      private DepthStencilViewBox _backBufferDepthView;
      private RenderTargetViewBox _backBufferRenderTargetView;

      public ImmediateDeviceContext(DeviceContext deviceContext, RenderStates renderStates, SwapChain swapChain) : base(deviceContext, renderStates) {
         _swapChain = swapChain;
      }

      public void GetBackBufferViews(out IDepthStencilView dsv, out IRenderTargetView rtv) {
         dsv = _backBufferDepthView;
         rtv = _backBufferRenderTargetView;
      }

      public void HandleBackBufferResized(DepthStencilViewBox backBufferDepthView, RenderTargetViewBox backBufferRenderTargetView) {
         _backBufferRenderTargetView = backBufferRenderTargetView;
         _backBufferDepthView = backBufferDepthView;

         if (_currentRenderTargetViews[0] == backBufferRenderTargetView ||
             _currentRenderTargetViews[1] == backBufferRenderTargetView ||
             _currentRenderTargetViews[2] == backBufferRenderTargetView ||
             _currentRenderTargetViews[3] == backBufferRenderTargetView ||
             _currentDepthStencilView == backBufferDepthView) {
            UpdateRenderTargetsInternal();
         }
      }

      public void Present() {
         _swapChain.Present(GetVsyncEnabled() ? 1 : 0, PresentFlags.None);
      }

      public void ExecuteCommandList(ICommandList commandList) {
         _deviceContext.ExecuteCommandList(((CommandListBox)commandList).CommandList, false);
         ResetToUninitializedState();
      }
   }
}