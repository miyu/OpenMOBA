using System;
using SharpDX;
using SharpDX.Direct3D;

namespace Canvas3D.LowLevel {
   [Flags]
   public enum RenderStage {
      Vertex = 1 << 0,
      Domain = 1 << 2,
      Pixel = 1 << 4,

      VertexPixel = Vertex | Pixel,
      VertexDomainPixel = Vertex | Domain | Pixel
   }

   public interface IDeviceContext {
      void SetVsyncEnabled(bool val);
      void SetDepthConfiguration(DepthConfiguration config);
      void SetRasterizerConfiguration(RasterizerConfiguration config);

      // Unbinds subresources (side-effect of OMSetRenderTargets)
      void SetRenderTargets(IDepthStencilView depthStencilView, IRenderTargetView renderTargetView0, IRenderTargetView renderTargetView1 = null, IRenderTargetView renderTargetView2 = null, IRenderTargetView renderTargetView3 = null);

      void ClearRenderTarget(Color4 color);
      void ClearRenderTargets(Color4? c0 = null, Color4? c1 = null, Color4? c2 = null, Color4? c3 = null);
      void ClearDepthBuffer(float depth);

      void SetViewportRect(Vector2 position, Vector2 size);
      void SetViewportRect(RectangleF rectangle);

      void SetVertexShader(IVertexShader shader);
      void SetHullShader(IHullShader shader);
      void SetDomainShader(IDomainShader shader);
      void SetGeometryShader(IGeometryShader shader);
      void SetPixelShader(IPixelShader shader);

      void SetPrimitiveTopology(PrimitiveTopology topology);
      void SetVertexBuffer<T>(int slot, IBuffer<T> buffer) where T : struct;
      void SetVertexBuffer(int slot, int? @null);
      void SetIndexBuffer<T>(int slot, IBuffer<T> buffer) where T : struct;
      void SetIndexBuffer(int slot, int? @null);
      void SetConstantBuffer<T>(int slot, IBuffer<T> buffer, RenderStage stages) where T : struct;
      void SetShaderResource(int slot, IShaderResourceView view, RenderStage stages);

      void Draw(int vertices, int verticesOffset);
      void DrawInstanced(int vertices, int verticesOffset, int instances, int instancesOffset);
      void DrawIndexedInstanced(int indexCountPerIndex, int instances, int indicesOffset, int verticesOffset, int instancesOffset);

      IBufferUpdater<T> TakeUpdater<T>(IBuffer<T> buffer) where T : struct;
      void Update<T>(IBuffer<T> buffer, T item) where T : struct;
      void Update<T>(IBuffer<T> buffer, IntPtr data, int count) where T : struct;
      void Update<T>(IBuffer<T> buffer, T[] arr, int offset = 0, int count = -1) where T : struct;
   }

   public interface IImmediateDeviceContext : IDeviceContext {
      void GetBackBufferViews(out IDepthStencilView depthStencilView, out IRenderTargetView renderTargetView);
      void Present();
      void ExecuteCommandList(ICommandList commandList);
   }

   public interface IDeferredDeviceContext : IDeviceContext {
      ICommandList FinishCommandListAndFree();
   }
}