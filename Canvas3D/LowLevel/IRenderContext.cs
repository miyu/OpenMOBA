using SharpDX;
using SharpDX.Direct3D;

namespace Canvas3D.LowLevel {
   public interface IRenderContext {
      void SetVsyncEnabled(bool val);
      void SetDepthConfiguration(DepthConfiguration config);
      void SetRasterizerConfiguration(RasterizerConfiguration config);

      void GetRenderTargets(out IDepthStencilView depthStencilView, out IRenderTargetView renderTargetView);
      void SetRenderTargets(IDepthStencilView depthStencilView, IRenderTargetView renderTargetView);

      void ClearRenderTarget(Color color);
      void ClearDepthBuffer(float depth);

      void SetViewportRect(RectangleF rectangle);

      void SetPixelShader(IPixelShader shader);
      void SetVertexShader(IVertexShader shader);

      void SetPrimitiveTopology(PrimitiveTopology topology);
      void SetVertexBuffer(IVertexBuffer vertexBuffer);
      void Draw(int vertices, int offset);
   }

   public interface IImmediateRenderContext : IRenderContext {
      void Present();
   }

   public interface IDeferredRenderContext : IRenderContext {
   }
}