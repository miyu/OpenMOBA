using System;
using System.Collections.Generic;
using System.IO;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace Canvas3D.LowLevel.Direct3D {
   public class BaseDeviceContext : IDeviceContext, IDisposable {
      protected DeviceContext _deviceContext;
      protected RenderStates _renderStates;

      protected bool _isVsyncEnabled = true;
      protected DepthConfiguration _currentDepthConfiguration;
      protected RasterizerConfiguration _currentRasterizerConfiguration;
      protected DepthStencilViewBox _currentDepthStencilView;
      protected RenderTargetViewBox[] _currentRenderTargetViews = new RenderTargetViewBox[4];
      protected RenderTargetView[] _preallocatedRtvArray = new RenderTargetView[4];
      protected RectangleF _currentViewportRect;

      public BaseDeviceContext(DeviceContext deviceContext, RenderStates renderStates) {
         _deviceContext = deviceContext;
         _renderStates = renderStates;

         ResetToUninitializedState(); // should be a no-op.
      }

      protected void ResetToUninitializedState() {
         _currentDepthConfiguration = DepthConfiguration.Uninitialized;
         _currentRasterizerConfiguration = RasterizerConfiguration.Uninitialized;
         _currentDepthStencilView = null;
         for (var i = 0; i < _currentRenderTargetViews.Length; i++) {
            _currentRenderTargetViews[i] = null;
            _preallocatedRtvArray[i] = null;
         }
         _currentViewportRect = RectangleF.Empty;
      }

      public void SetVsyncEnabled(bool val) => _isVsyncEnabled = val;
      public bool GetVsyncEnabled() => _isVsyncEnabled;

      public void SetDepthConfiguration(DepthConfiguration config) {
         if (config != _currentDepthConfiguration) {
            _currentDepthConfiguration = config;

            // Console.WriteLine("Set Depth Configuration: " + config);

            switch (config) {
               case DepthConfiguration.Disabled:
                  _deviceContext.OutputMerger.DepthStencilState = _renderStates.DepthDisable;
                  break;
               case DepthConfiguration.Enabled:
                  _deviceContext.OutputMerger.DepthStencilState = _renderStates.DepthEnable;
                  break;
               default:
                  throw new ArgumentException($"Unknown Depth Configuration '{config}'");
            }
         }
      }

      public void SetRasterizerConfiguration(RasterizerConfiguration config) {
         if (config != _currentRasterizerConfiguration) {
            _currentRasterizerConfiguration = config;

            // Console.WriteLine("Set Rasterizer Configuration: " + config);

            switch (config) {
               case RasterizerConfiguration.FillFront:
                  _deviceContext.Rasterizer.State = _renderStates.RasterizerFillFront;
                  break;
               case RasterizerConfiguration.FillBack:
                  _deviceContext.Rasterizer.State = _renderStates.RasterizerFillBack;
                  break;
               case RasterizerConfiguration.FillFrontBack:
                  _deviceContext.Rasterizer.State = _renderStates.RasterizerFillFrontBack;
                  break;
               case RasterizerConfiguration.WireFront:
                  _deviceContext.Rasterizer.State = _renderStates.RasterizerWireFront;
                  break;
               case RasterizerConfiguration.WireBack:
                  _deviceContext.Rasterizer.State = _renderStates.RasterizerWireBack;
                  break;
               case RasterizerConfiguration.WireFrontBack:
                  _deviceContext.Rasterizer.State = _renderStates.RasterizerWireFrontBack;
                  break;
               default:
                  throw new ArgumentException($"Unknown Rasterizer Configuration '{config}'");
            }
         }
      }

      public void SetRenderTargets(IDepthStencilView depthStencilView, IRenderTargetView renderTargetView0, IRenderTargetView renderTargetView1 = null, IRenderTargetView renderTargetView2 = null, IRenderTargetView renderTargetView3 = null) {
         var dsv = (DepthStencilViewBox)depthStencilView;
         var rtv0 = (RenderTargetViewBox)renderTargetView0;
         var rtv1 = (RenderTargetViewBox)renderTargetView1;
         var rtv2 = (RenderTargetViewBox)renderTargetView2;
         var rtv3 = (RenderTargetViewBox)renderTargetView3;

         if (_currentDepthStencilView == dsv &&
             _currentRenderTargetViews[0] == rtv0 &&
             _currentRenderTargetViews[1] == rtv1 &&
             _currentRenderTargetViews[2] == rtv2 &&
             _currentRenderTargetViews[3] == rtv3) {
            return;
         }

         _currentDepthStencilView = dsv;
         _currentRenderTargetViews[0] = rtv0;
         _currentRenderTargetViews[1] = rtv1;
         _currentRenderTargetViews[2] = rtv2;
         _currentRenderTargetViews[3] = rtv3;

         UpdateRenderTargetsInternal();
      }

      protected void UpdateRenderTargetsInternal() {
         var depthStencilView = _currentDepthStencilView?.DepthStencilView;
         _preallocatedRtvArray[0] = _currentRenderTargetViews[0]?.RenderTargetView;
         _preallocatedRtvArray[1] = _currentRenderTargetViews[1]?.RenderTargetView;
         _preallocatedRtvArray[2] = _currentRenderTargetViews[2]?.RenderTargetView;
         _preallocatedRtvArray[3] = _currentRenderTargetViews[3]?.RenderTargetView;
         _deviceContext.OutputMerger.SetRenderTargets(depthStencilView, _preallocatedRtvArray);
      }

      public void ClearRenderTarget(Color4 color) {
         var renderTargetView = _currentRenderTargetViews[0].RenderTargetView;
         _deviceContext.ClearRenderTargetView(renderTargetView, color);
      }

      public void ClearRenderTargets(Color4? c0 = null, Color4? c1 = null, Color4? c2 = null, Color4? c3 = null) {
         if (c0.HasValue) _deviceContext.ClearRenderTargetView(_currentRenderTargetViews[0].RenderTargetView, c0.Value);
         if (c1.HasValue) _deviceContext.ClearRenderTargetView(_currentRenderTargetViews[1].RenderTargetView, c1.Value);
         if (c2.HasValue) _deviceContext.ClearRenderTargetView(_currentRenderTargetViews[2].RenderTargetView, c2.Value);
         if (c3.HasValue) _deviceContext.ClearRenderTargetView(_currentRenderTargetViews[3].RenderTargetView, c3.Value);
      }

      public void ClearDepthBuffer(float depth) {
         var depthStencilView = ((DepthStencilViewBox)_currentDepthStencilView).DepthStencilView;
         _deviceContext.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, depth, 0);
      }

      public void SetViewportRect(Vector2 position, Vector2 size) {
         SetViewportRect(new RectangleF(position.X, position.Y, size.X, size.Y));
      }

      public void SetViewportRect(RectangleF rectangle) {
         if (_currentViewportRect != rectangle) {
            _currentViewportRect = rectangle;

            _deviceContext.Rasterizer.SetViewport(new ViewportF(rectangle));
         }
      }

      public void SetVertexShader(IVertexShader shader) {
         var box = (VertexShaderBox)shader;
         _deviceContext.VertexShader.Set(box.Shader);
         _deviceContext.InputAssembler.InputLayout = box.InputLayout;
      }

      public void SetHullShader(IHullShader shader) {
         _deviceContext.HullShader.Set(((HullShaderBox)shader)?.Shader);
      }

      public void SetDomainShader(IDomainShader shader) {
         _deviceContext.DomainShader.Set(((DomainShaderBox)shader)?.Shader);
      }

      public void SetGeometryShader(IGeometryShader shader) {
         _deviceContext.GeometryShader.Set(((GeometryShaderBox)shader)?.Shader);
      }

      public void SetPixelShader(IPixelShader shader) {
         _deviceContext.PixelShader.Set(((PixelShaderBox)shader).Shader);
      }

      public void SetPrimitiveTopology(PrimitiveTopology topology) {
         _deviceContext.InputAssembler.PrimitiveTopology = topology;
      }

      public void SetVertexBuffer<T>(int slot, IBuffer<T> buffer) where T : struct {
         var box = (BufferBox<T>)buffer;
         _deviceContext.InputAssembler.SetVertexBuffers(
            slot,
            new VertexBufferBinding(box.Buffer, box.Stride, 0));
      }

      public void SetVertexBuffer(int slot, int? @null) {
         _deviceContext.InputAssembler.SetVertexBuffers(slot, new VertexBufferBinding());
      }

      public void SetIndexBuffer<T>(int slot, IBuffer<T> buffer) where T : struct {
         var box = (BufferBox<T>)buffer;
         _deviceContext.InputAssembler.SetIndexBuffer(box.Buffer, box.Format, 0);
      }

      public void SetIndexBuffer(int slot, int? @null) {
         _deviceContext.InputAssembler.SetIndexBuffer(null, Format.Unknown, 0);
      }

      public void SetConstantBuffer<T>(int slot, IBuffer<T> buffer, RenderStage stages) where T : struct {
         var box = (BufferBox<T>)buffer;
         if ((stages & RenderStage.Vertex) != 0) {
            _deviceContext.VertexShader.SetConstantBuffer(slot, box.Buffer);
         }
         if ((stages & RenderStage.Domain) != 0) {
            _deviceContext.DomainShader.SetConstantBuffer(slot, box.Buffer);
         }
         if ((stages & RenderStage.Pixel) != 0) {
            _deviceContext.PixelShader.SetConstantBuffer(slot, box.Buffer);
         }
      }

      public void SetConstantBuffer(int slot, int? @null, RenderStage stages) {
         if ((stages & RenderStage.Vertex) != 0) {
            _deviceContext.VertexShader.SetConstantBuffer(slot, null);
         }
         if ((stages & RenderStage.Domain) != 0) {
            _deviceContext.DomainShader.SetConstantBuffer(slot, null);
         }
         if ((stages & RenderStage.Pixel) != 0) {
            _deviceContext.PixelShader.SetConstantBuffer(slot, null);
         }
      }

      public void SetShaderResource(int slot, IShaderResourceView view, RenderStage stages) {
         var box = (ShaderResourceViewBox)view;
         if ((stages & RenderStage.Vertex) != 0) {
            _deviceContext.VertexShader.SetShaderResource(slot, box?.ShaderResourceView);
         }
         if ((stages & RenderStage.Domain) != 0) {
            _deviceContext.DomainShader.SetShaderResource(slot, box?.ShaderResourceView);
         }
         if ((stages & RenderStage.Pixel) != 0) {
            _deviceContext.PixelShader.SetShaderResource(slot, box?.ShaderResourceView);
         }
      }

      public void Draw(int vertices, int verticesOffset) {
         _deviceContext.Draw(vertices, verticesOffset);
      }

      public void DrawInstanced(int vertices, int verticesOffset, int instances, int instancesOffset) {
         _deviceContext.DrawInstanced(vertices, instances, verticesOffset, instancesOffset);
      }

      public void DrawIndexedInstanced(int indexCountPerIndex, int instances, int indicesOffset, int verticesOffset, int instancesOffset) {
         _deviceContext.DrawIndexedInstanced(indexCountPerIndex, instances, indicesOffset, verticesOffset, instancesOffset);
      }

      public IBufferUpdater<T> TakeUpdater<T>(IBuffer<T> buffer) where T : struct {
         return BufferUpdaterPool<T>.Take(_deviceContext, (BufferBox<T>)buffer);
      }

      public void Update<T>(IBuffer<T> buffer, T item) where T : struct {
         var box = (BufferBox<T>)buffer;
         if (box.Count < 1) {
            throw new ArgumentOutOfRangeException();
         }
         var db = _deviceContext.MapSubresource(box.Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
         Utilities.Write(db.DataPointer, ref item);
         _deviceContext.UnmapSubresource(box.Buffer, 0);
      }

      public void Update<T>(IBuffer<T> buffer, IntPtr data, int count) where T : struct {
         var box = (BufferBox<T>)buffer;
         if (count > box.Count) {
            throw new ArgumentOutOfRangeException();
         }
         var db = _deviceContext.MapSubresource(box.Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
         Utilities.CopyMemory(db.DataPointer, data, count * box.Stride);
         _deviceContext.UnmapSubresource(box.Buffer, 0);
      }

      public void Update<T>(IBuffer<T> buffer, T[] arr, int offset = 0, int count = -1) where T : struct {
         if (count == -1) {
            count = arr.Length;
         }

         var box = (BufferBox<T>)buffer;
         if (count > box.Count) {
            throw new ArgumentOutOfRangeException();
         }

         var db = _deviceContext.MapSubresource(box.Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
         Utilities.Write(db.DataPointer, arr, offset, count);
         _deviceContext.UnmapSubresource(box.Buffer, 0);
      }

      public void Dispose() {
         Utilities.Dispose<DeviceContext>(ref _deviceContext);
      }

      private static class BufferUpdaterPool<T> where T : struct {
         private static readonly object synchronization = new object();
         private static readonly Stack<BufferUpdater> store = new Stack<BufferUpdater>();

         public static IBufferUpdater<T> Take(DeviceContext deviceContext, BufferBox<T> bufferBox) {
            BufferUpdater TakeUninitialized() {
               lock (synchronization) {
                  if (store.Count > 0) return store.Pop();
               }
               return new BufferUpdater();
            }

            var updater = TakeUninitialized();
            updater.Initialize(deviceContext, bufferBox);
            return updater;
         }

         private class BufferUpdater : IBufferUpdater<T> {
            private DeviceContext deviceContext;
            private BufferBox<T> bufferBox;
            private IntPtr currentBufferPointer;
            private int remaining;

            internal void Initialize(DeviceContext deviceContext, BufferBox<T> bufferBox) {
               this.deviceContext = deviceContext;
               this.bufferBox = bufferBox;
               this.currentBufferPointer = deviceContext.MapSubresource(bufferBox.Buffer, 0, MapMode.WriteDiscard, MapFlags.None).DataPointer;
               this.remaining = bufferBox.Count;
            }

            public void Write(T val) {
               if (--remaining < 0) throw new InternalBufferOverflowException();
               currentBufferPointer = Utilities.WriteAndPosition(currentBufferPointer, ref val);
            }

            public void Write(ref T val) {
               if (--remaining < 0) throw new InternalBufferOverflowException();
               currentBufferPointer = Utilities.WriteAndPosition(currentBufferPointer, ref val);
            }

            public void Write(T[] vals) {
               Write(vals, 0, vals.Length);
            }

            public void Write(T[] vals, int offset, int count) {
               remaining -= count;
               if (remaining < 0) throw new InternalBufferOverflowException();
               Utilities.Write(currentBufferPointer, vals, 0, count);
               currentBufferPointer += Utilities.SizeOf<T>() * count;
            }

            public void UpdateAndReset() {
               UpdateAndClose();
               Reopen();
            }

            public void Reopen() {
               this.currentBufferPointer = deviceContext.MapSubresource(bufferBox.Buffer, 0, MapMode.WriteDiscard, MapFlags.None).DataPointer;
               this.remaining = bufferBox.Count;
            }

            public void UpdateAndClose() {
               deviceContext.UnmapSubresource(bufferBox.Buffer, 0);
               currentBufferPointer = default(IntPtr);
               remaining = 0;
            }

            public void UpdateCloseAndDispose() {
               UpdateAndClose();
               Dispose();
            }

            public void Dispose() {
               if (currentBufferPointer != default(IntPtr)) {
                  UpdateAndClose();
               }

               // Zero self
               deviceContext = null;
               bufferBox = null;
               currentBufferPointer = default(IntPtr);
               remaining = 0;

               // return to pool
               lock (synchronization) {
                  store.Push(this);
               }
            }
         }
      }
   }
}