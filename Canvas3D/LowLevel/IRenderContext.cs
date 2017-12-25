using System;
using SharpDX;
using SharpDX.Direct3D;

namespace Canvas3D.LowLevel {
   [Flags]
   public enum RenderStage {
      Pixel = 1 << 0,
      Vertex = 1 << 1,

      PixelVertex = Pixel | Vertex
   }

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
      void SetVertexBuffer<T>(int slot, IBuffer<T> buffer) where T : struct;
      void SetVertexBuffer(int slot, int? @null);
      void SetConstantBuffer<T>(int slot, IBuffer<T> buffer, RenderStage stages) where T : struct;
      void SetShaderResource(int slot, IShaderResourceView view, RenderStage stages);

      void Draw(int vertices, int verticesOffset);
      void DrawInstanced(int vertices, int verticesOffset, int instances, int instancesOffset);

      void Update<T>(IBuffer<T> buffer, T item) where T : struct;
      void Update<T>(IBuffer<T> buffer, IntPtr data, int count) where T : struct;
      void Update<T>(IBuffer<T> buffer, T[] arr, int offset, int count) where T : struct;
   }

   public interface IImmediateRenderContext : IRenderContext {
      void Present();
   }

   public interface IDeferredRenderContext : IRenderContext {
   }
}