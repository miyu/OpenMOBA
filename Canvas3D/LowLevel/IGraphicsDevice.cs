using System;
using System.Drawing;

namespace Canvas3D.LowLevel {
   public interface IGraphicsDevice : IDisposable {
      IImmediateRenderContext ImmediateContext { get; }
      ITechniqueCollection TechniqueCollection { get; }
      IPresetsStore PresetsStore { get; }

      void DoEvents();

      IDeferredRenderContext CreateDeferredRenderContext();

      IBuffer<T> CreateConstantBuffer<T>(int count) where T : struct;
      IBuffer<T> CreateVertexBuffer<T>(int count) where T : struct;
      IBuffer<T> CreateVertexBuffer<T>(T[] content) where T : struct;
      (IBuffer<T>, IShaderResourceView) CreateStructuredBufferAndView<T>(int count) where T : struct;

      (IRenderTargetView[], IShaderResourceView, IShaderResourceView[]) CreateScreenSizeRenderTarget(int levels);
      (ITexture2D, IRenderTargetView[], IShaderResourceView, IShaderResourceView[]) CreateRenderTarget(int levels, Size resolution);
      (IDepthStencilView, IShaderResourceView) CreateScreenSizeDepthTarget();
      (ITexture2D, IDepthStencilView[], IShaderResourceView, IShaderResourceView[]) CreateDepthTextureAndViews(int levels, Size resolution);
   }
}
