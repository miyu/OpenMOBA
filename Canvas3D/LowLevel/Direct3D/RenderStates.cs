using System;
using SharpDX;
using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class RenderStates : IDisposable {
      private DepthStencilState _depthEnable;
      private DepthStencilState _depthDisable;
      private RasterizerState _rasterizerFillFront;
      private RasterizerState _rasterizerFillBack;
      private RasterizerState _rasterizerFillFrontBack;
      private RasterizerState _rasterizerWireFront;
      private RasterizerState _rasterizerWireBack;
      private RasterizerState _rasterizerWireFrontBack;

      public RenderStates(Device device) {
         _depthEnable = new DepthStencilState(device, DepthStencilDesc(true));
         _depthDisable = new DepthStencilState(device, DepthStencilDesc(false));
         _rasterizerFillFront = new RasterizerState(device, RasterizerDesc(CullMode.Back, false));
         _rasterizerFillBack = new RasterizerState(device, RasterizerDesc(CullMode.Front, false));
         _rasterizerFillFrontBack = new RasterizerState(device, RasterizerDesc(CullMode.None, false));
         _rasterizerWireFront = new RasterizerState(device, RasterizerDesc(CullMode.Back, true));
         _rasterizerWireBack = new RasterizerState(device, RasterizerDesc(CullMode.Front, true));
         _rasterizerWireFrontBack = new RasterizerState(device, RasterizerDesc(CullMode.None, true));
      }

      public DepthStencilState DepthDisable => _depthDisable;
      public DepthStencilState DepthEnable => _depthEnable;
      public RasterizerState RasterizerFillFront => _rasterizerFillFront;
      public RasterizerState RasterizerFillBack => _rasterizerFillBack;
      public RasterizerState RasterizerFillFrontBack => _rasterizerFillFrontBack;
      public RasterizerState RasterizerWireFront => _rasterizerWireFront;
      public RasterizerState RasterizerWireBack => _rasterizerWireBack;
      public RasterizerState RasterizerWireFrontBack => _rasterizerWireFrontBack;

      public void Dispose() {
         Utilities.Dispose<DepthStencilState>(ref _depthDisable);
         Utilities.Dispose<DepthStencilState>(ref _depthEnable);
         Utilities.Dispose<RasterizerState>(ref _rasterizerFillFront);
      }

      private static DepthStencilStateDescription DepthStencilDesc(bool enableDepth) => new DepthStencilStateDescription {
         IsDepthEnabled = enableDepth,
         DepthComparison = Comparison.Less,
         DepthWriteMask = DepthWriteMask.All,
         IsStencilEnabled = false,
         StencilReadMask = 0xff,
         StencilWriteMask = 0xff
      };

      private static RasterizerStateDescription RasterizerDesc(CullMode cullMode, bool wireframe) => new RasterizerStateDescription {
         CullMode = cullMode,
         FillMode = wireframe ? FillMode.Wireframe : FillMode.Solid,
         IsDepthClipEnabled = false,
         IsAntialiasedLineEnabled = false,
         IsMultisampleEnabled = false
      };
   }
}