using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class DeferredDeviceContext : BaseDeviceContext, IDeferredDeviceContext {
      private readonly Direct3DGraphicsDevice _graphicsDevice;

      public DeferredDeviceContext(Direct3DGraphicsDevice graphicsDevice, DeviceContext deviceContext, RenderStates renderStates) : base(deviceContext, renderStates) {
         _graphicsDevice = graphicsDevice;
      }

      public ICommandList FinishCommandListAndFree() {
         var box = new CommandListBox { CommandList = _deviceContext.FinishCommandList(false) };
         ResetToUninitializedState();
         _graphicsDevice.ReturnDeferredContext(this);
         return box;
      }
   }
}