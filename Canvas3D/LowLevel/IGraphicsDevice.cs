using System;
using System.Drawing;
using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel {
   public interface IGraphicsDevice : IDisposable {
      IImmediateRenderContext ImmediateContext { get; }
      ILowLevelAssetManager LowLevelAssetManager { get; }
      ITechniqueCollection TechniqueCollection { get; }
      IMeshPresets MeshPresets { get; }

      void DoEvents();

      IDeferredRenderContext CreateDeferredRenderContext();

      IBuffer<T> CreateConstantBuffer<T>(int count) where T : struct;
      IBuffer<T> CreateVertexBuffer<T>(int count) where T : struct;
      IBuffer<T> CreateVertexBuffer<T>(T[] content) where T : struct;
      (IBuffer<T>, IShaderResourceView) CreateStructuredBufferAndView<T>(int count) where T : struct;
      (IDisposable, IDepthStencilView[], IShaderResourceView, IShaderResourceView[]) CreateDepthTextureAndViews(int levels, Size resolution);
   }
}