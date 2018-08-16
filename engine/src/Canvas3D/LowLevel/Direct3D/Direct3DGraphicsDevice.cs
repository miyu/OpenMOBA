using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using Resource = SharpDX.Direct3D11.Resource;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DGraphicsDevice : IGraphicsDevice {
      public const Format kUnusedThereforeUnknownFormat = Format.Unknown;
      public const int BackBufferCount = 2;

      private static readonly Size kUninitializedBackBufferSize = new Size(16, 16);
      private readonly DepthStencilViewBox _backBufferDepthView = new DepthStencilViewBox { Resolution = kUninitializedBackBufferSize };
      private readonly RenderTargetViewBox _backBufferRenderTargetView = new RenderTargetViewBox { Resolution = kUninitializedBackBufferSize };
      private readonly RenderForm _form; // don't dispose
      private readonly ImmediateDeviceContext _immediateContext;
      private readonly RenderStates _renderStates;
      private readonly List<(Texture2DBox, DepthStencilViewBox, ShaderResourceViewBox)> _screenSizeDepthStencilTargets = new List<(Texture2DBox, DepthStencilViewBox, ShaderResourceViewBox)>();
      private readonly List<(Texture2DBox, RenderTargetViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[])> _screenSizeRenderTargets = new List<(Texture2DBox, RenderTargetViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[])>();
      private readonly SwapChain _swapChain;
      private readonly Stack<DeferredDeviceContext> deferredRenderContextPool = new Stack<DeferredDeviceContext>();
      private readonly object deferredRenderContextPoolLock = new object();
      private Texture2D _backBufferDepthTexture;
      private Texture2D _backBufferRenderTargetTexture;
      private bool _isResizeTriggered;
      private Size _renderSize;

      internal Direct3DGraphicsDevice(RenderForm form, Device device, SwapChain swapChain) {
         _form = form;
         InternalD3DDevice = device;
         _swapChain = swapChain;

         // code smell: init subsystems
         _renderStates = new RenderStates(InternalD3DDevice);
         _immediateContext = new ImmediateDeviceContext(InternalD3DDevice.ImmediateContext, _renderStates, _swapChain);
      }

      internal Device InternalD3DDevice { get; }

      public IImmediateDeviceContext ImmediateContext => _immediateContext;

      public void DoEvents() {
         if (_isResizeTriggered) {
            _renderSize = _form.ClientSize;
            ResizeScreenSizeBuffers(_renderSize);
            _isResizeTriggered = false;
         }
      }

      public IDeferredDeviceContext CreateDeferredRenderContext() {
         lock (deferredRenderContextPoolLock) {
            if (deferredRenderContextPool.Count > 0) return deferredRenderContextPool.Pop();
         }
         Console.WriteLine("Alloc DRC");
         return new DeferredDeviceContext(this, new DeviceContext(InternalD3DDevice), _renderStates);
      }

      public IBuffer<T> CreateConstantBuffer<T>(int count) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = new Buffer(InternalD3DDevice, count * sizeOfT, ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, 0, sizeOfT);
         return new BufferBox<T> { Buffer = buffer, Count = count, Stride = sizeOfT, Format = kUnusedThereforeUnknownFormat };
      }

      public IBuffer<T> CreateVertexBuffer<T>(int count) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = new Buffer(InternalD3DDevice, count * sizeOfT, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, 0, sizeOfT);
         return new BufferBox<T> { Buffer = buffer, Count = count, Stride = sizeOfT, Format = kUnusedThereforeUnknownFormat };
      }

      public IBuffer<T> CreateVertexBuffer<T>(T[] content) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = Buffer.Create(InternalD3DDevice, BindFlags.VertexBuffer, content, sizeOfT * content.Length, ResourceUsage.Dynamic, CpuAccessFlags.Write, 0, sizeOfT);
         return new BufferBox<T> { Buffer = buffer, Count = content.Length, Stride = sizeOfT, Format = kUnusedThereforeUnknownFormat };
      }

      public IBuffer<T> CreateIndexBuffer<T>(int count) where T : struct {
         var format = 
            typeof(T) == typeof(int) ? Format.R32_UInt :
            typeof(T) == typeof(short) ? Format.R16_UInt :
            throw new ArgumentException();

         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = new Buffer(InternalD3DDevice, count * sizeOfT, ResourceUsage.Dynamic, BindFlags.IndexBuffer, CpuAccessFlags.Write, 0, sizeOfT);
         return new BufferBox<T> { Buffer = buffer, Count = count, Stride = sizeOfT, Format = format };
      }

      public IBuffer<T> CreateIndexBuffer<T>(T[] content) where T : struct {
         var buffer = CreateIndexBuffer<T>(content.Length);
         ImmediateContext.Update(buffer, content);
         return buffer;
      }


      public (IBuffer<T>, IShaderResourceView) CreateStructuredBufferAndView<T>(int count) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = new Buffer(InternalD3DDevice, count * sizeOfT, ResourceUsage.Dynamic, BindFlags.ShaderResource, CpuAccessFlags.Write, ResourceOptionFlags.BufferStructured, sizeOfT);
         var bufferBox = new BufferBox<T> { Buffer = buffer, Count = count, Stride = sizeOfT };
         var srv = new ShaderResourceView(InternalD3DDevice, buffer,
            new ShaderResourceViewDescription {
               Dimension = ShaderResourceViewDimension.Buffer,
               Format = Format.Unknown,
               Buffer = {
                  FirstElement = 0,
                  ElementOffset = 0,
                  ElementCount = sizeOfT, // WTF: This works if I flip ElementCount/Width as done here...
                  ElementWidth = count // Otherwise runtime + renderdoc think there are sizeOfT elements in buffer?
               }
            });
         var srvBox = new ShaderResourceViewBox { ShaderResourceView = srv };
         return (bufferBox, srvBox);
      }

      internal void Initialize() {
         // On first frame, must alloc backbuffers and renderview. Same after form resize.
         _isResizeTriggered = true;
         _form.UserResized += (s, e) => _isResizeTriggered = true;

         _immediateContext.SetDepthConfiguration(DepthConfiguration.Enabled);
         _immediateContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
      }

      internal void ReturnDeferredContext(DeferredDeviceContext deferredDeviceContext) {
         lock (deferredRenderContextPoolLock) {
            deferredRenderContextPool.Push(deferredDeviceContext);
         }
      }

      // Lifetime Resources

      // Swap Chain + Screen-Size Buffers + Resizing

      // Subsystems

      // Deferred render context pool
      public (IRenderTargetView[], IShaderResourceView, IShaderResourceView[]) CreateScreenSizeRenderTarget(int levels) {
         var (tex, rtvs, srv, srvs) = CreateRenderTargetInternal(levels, _backBufferRenderTargetView.Resolution);
         _screenSizeRenderTargets.Add((tex, rtvs, srv, srvs));
         // ReSharper disable CoVariantArrayConversion
         return (rtvs, srv, srvs);
         // ReSharper enable CoVariantArrayConversion
      }

      public (ITexture2D, IRenderTargetView[], IShaderResourceView, IShaderResourceView[]) CreateRenderTarget(int levels, Size resolution) {
         return CreateRenderTargetInternal(levels, resolution);
      }

      private (Texture2DBox, RenderTargetViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[]) CreateRenderTargetInternal(int levels, Size resolution) {
         var hq = false;
         var format = hq ? Format.R32G32B32A32_Float : Format.R16G16B16A16_UNorm;
         var texture = new Texture2D(InternalD3DDevice,
            new Texture2DDescription {
               Format = format,
               ArraySize = levels,
               MipLevels = 1,
               Width = resolution.Width,
               Height = resolution.Height,
               SampleDescription = new SampleDescription(1, 0),
               Usage = ResourceUsage.Default,
               BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
               CpuAccessFlags = CpuAccessFlags.None,
               OptionFlags = ResourceOptionFlags.None
            });
         var textureBox = new Texture2DBox { Texture = texture };
         var rtvs = new RenderTargetViewBox[levels];
         for (var i = 0; i < levels; i++)
            rtvs[i] = new RenderTargetViewBox {
               RenderTargetView = new RenderTargetView(InternalD3DDevice, texture,
                  new RenderTargetViewDescription {
                     Format = format,
                     Dimension = RenderTargetViewDimension.Texture2DArray,
                     Texture2DArray = {
                        ArraySize = 1,
                        FirstArraySlice = i,
                        MipSlice = 0
                     }
                  }),
               Resolution = resolution
            };
         var srv = new ShaderResourceViewBox {
            ShaderResourceView = new ShaderResourceView(InternalD3DDevice, texture,
               new ShaderResourceViewDescription {
                  Format = format,
                  Dimension = ShaderResourceViewDimension.Texture2DArray,
                  Texture2DArray = {
                     ArraySize = levels,
                     FirstArraySlice = 0,
                     MipLevels = 1,
                     MostDetailedMip = 0
                  }
               })
         };

         var srvs = new ShaderResourceViewBox[levels];
         for (var i = 0; i < levels; i++)
            srvs[i] = new ShaderResourceViewBox {
               ShaderResourceView = new ShaderResourceView(InternalD3DDevice, texture,
                  new ShaderResourceViewDescription {
                     Format = format,
                     Dimension = ShaderResourceViewDimension.Texture2DArray,
                     Texture2DArray = {
                        MipLevels = 1,
                        MostDetailedMip = 0,
                        ArraySize = 1,
                        FirstArraySlice = i
                     }
                  })
            };
         return (textureBox, rtvs, srv, srvs);
      }

      public (IDepthStencilView, IShaderResourceView) CreateScreenSizeDepthTarget() {
         var (texture, dsvs, srv, srvs) = CreateDepthTextureAndViewsInternal(1, _backBufferDepthView.Resolution);
         srv.ShaderResourceView.Dispose();
         _screenSizeDepthStencilTargets.Add((texture, dsvs[0], srvs[0]));
         return (dsvs[0], srvs[0]);
      }

      public (ITexture2D, IDepthStencilView[], IShaderResourceView, IShaderResourceView[]) CreateDepthTextureAndViews(int levels, Size resolution) {
         return CreateDepthTextureAndViewsInternal(levels, resolution);
      }

      private (Texture2DBox, DepthStencilViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[]) CreateDepthTextureAndViewsInternal(int levels, Size resolution) {
         var hq = true;
         var textureFormat = hq ? Format.R32_Typeless : Format.R16_Typeless;
         var dsvFormat = hq ? Format.D32_Float : Format.D16_UNorm;
         var srvFormat = hq ? Format.R32_Float : Format.R16_UNorm;

         var texture = new Texture2D(InternalD3DDevice,
            new Texture2DDescription {
               Format = textureFormat,
               ArraySize = levels,
               MipLevels = 1,
               Width = resolution.Width,
               Height = resolution.Height,
               SampleDescription = new SampleDescription(1, 0),
               Usage = ResourceUsage.Default,
               BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
               CpuAccessFlags = CpuAccessFlags.None,
               OptionFlags = ResourceOptionFlags.None
            });
         var textureBox = new Texture2DBox { Texture = texture };
         var dsvs = new DepthStencilViewBox[levels];
         for (var i = 0; i < levels; i++)
            dsvs[i] = new DepthStencilViewBox {
               DepthStencilView = new DepthStencilView(InternalD3DDevice, texture,
                  new DepthStencilViewDescription {
                     Format = dsvFormat,
                     Dimension = DepthStencilViewDimension.Texture2DArray,
                     Texture2DArray = {
                        ArraySize = 1,
                        FirstArraySlice = i,
                        MipSlice = 0
                     }
                  }),
               Resolution = resolution
            };
         var srv = new ShaderResourceViewBox {
            ShaderResourceView = new ShaderResourceView(InternalD3DDevice, texture,
               new ShaderResourceViewDescription {
                  Format = srvFormat,
                  Dimension = ShaderResourceViewDimension.Texture2DArray,
                  Texture2DArray = {
                     MipLevels = 1,
                     MostDetailedMip = 0,
                     ArraySize = levels,
                     FirstArraySlice = 0
                  }
               })
         };

         var srvs = new ShaderResourceViewBox[levels];
         for (var i = 0; i < levels; i++)
            srvs[i] = new ShaderResourceViewBox {
               ShaderResourceView = new ShaderResourceView(InternalD3DDevice, texture,
                  new ShaderResourceViewDescription {
                     Format = srvFormat,
                     Dimension = ShaderResourceViewDimension.Texture2DArray,
                     Texture2DArray = {
                        MipLevels = 1,
                        MostDetailedMip = 0,
                        ArraySize = 1,
                        FirstArraySlice = i
                     }
                  })
            };
         return (textureBox, dsvs, srv, srvs);
      }

      private void ResizeScreenSizeBuffers(Size renderSize) {
         DisposeScreenSizeBuffersAndViews();

         var isFirstInitialize = _backBufferRenderTargetView.RenderTargetView == null;

         _swapChain.ResizeBuffers(BackBufferCount, renderSize.Width, renderSize.Height, Format.Unknown, SwapChainFlags.None);
         _backBufferRenderTargetTexture = Resource.FromSwapChain<Texture2D>(_swapChain, 0);
         _backBufferRenderTargetView.RenderTargetView = new RenderTargetView(InternalD3DDevice, _backBufferRenderTargetTexture);
         _backBufferRenderTargetView.Resolution = renderSize;
         _backBufferDepthTexture = new Texture2D(InternalD3DDevice, CreateBackBufferDescription(renderSize));
         _backBufferDepthView.DepthStencilView = new DepthStencilView(InternalD3DDevice, _backBufferDepthTexture);
         _backBufferDepthView.Resolution = renderSize;

         if (isFirstInitialize) _immediateContext.SetRenderTargets(_backBufferDepthView, _backBufferRenderTargetView);
         _immediateContext.HandleBackBufferResized(_backBufferDepthView, _backBufferRenderTargetView);

         for (var i = 0; i < _screenSizeRenderTargets.Count; i++) {
            var (tex, rtvs, srv, srvs) = _screenSizeRenderTargets[i];
            var (newTex, newRtvs, newSrv, newSrvs) = CreateRenderTargetInternal(rtvs.Length, renderSize);
            Utilities.Dispose(ref tex.Texture);
            for (var layer = 0; layer < rtvs.Length; layer++) {
               rtvs[layer].MoveAssignFrom(newRtvs[layer]);
               srvs[layer].MoveAssignFrom(newSrvs[layer]);
            }
            srv.MoveAssignFrom(newSrv);
            _screenSizeRenderTargets[i] = (newTex, rtvs, srv, srvs);
         }

         for (var i = 0; i < _screenSizeDepthStencilTargets.Count; i++) {
            var (tex, dsv, srv) = _screenSizeDepthStencilTargets[i];
            var (newTex, newDsvs, newSrv, newSrvs) = CreateDepthTextureAndViewsInternal(1, renderSize);
            Utilities.Dispose(ref tex.Texture);
            Utilities.Dispose(ref newSrv.ShaderResourceView);
            dsv.MoveAssignFrom(newDsvs[0]);
            srv.MoveAssignFrom(newSrvs[0]);
            _screenSizeDepthStencilTargets[i] = (newTex, dsv, srv);
         }
      }

      private void DisposeScreenSizeBuffersAndViews() {
         Utilities.Dispose(ref _backBufferRenderTargetTexture);
         Utilities.Dispose(ref _backBufferRenderTargetView.RenderTargetView);
         Utilities.Dispose(ref _backBufferDepthTexture);
         Utilities.Dispose(ref _backBufferDepthView.DepthStencilView);

         for (var i = 0; i < _screenSizeRenderTargets.Count; i++) {
            var (tex, rtvs, srv, srvs) = _screenSizeRenderTargets[i];
            Utilities.Dispose(ref tex.Texture);
            foreach (var rtv in rtvs) Utilities.Dispose(ref rtv.RenderTargetView);
            Utilities.Dispose(ref srv.ShaderResourceView);
            foreach (var levelSrv in srvs) Utilities.Dispose(ref levelSrv.ShaderResourceView);
         }
      }

      public void Dispose() {
         DisposeScreenSizeBuffersAndViews();

         InternalD3DDevice?.Dispose();
         _swapChain?.Dispose();
         _renderStates?.Dispose();
         _immediateContext?.Dispose();
      }

      private static Texture2DDescription CreateBackBufferDescription(Size clientSize) {
         return new Texture2DDescription {
            Format = Format.D16_UNorm,
            ArraySize = 1,
            MipLevels = 1,
            Width = clientSize.Width,
            Height = clientSize.Height,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
         };
      }

      private static SwapChainDescription CreateSwapChainDescription(Control form) {
         return new SwapChainDescription {
            BufferCount = Direct3DGraphicsDevice.BackBufferCount,
            ModeDescription = new ModeDescription(
               form.ClientSize.Width,
               form.ClientSize.Height,
               new Rational(300, 1), // doesn't matter
               Format.R8G8B8A8_UNorm_SRgb),
            IsWindowed = true,
            OutputHandle = form.Handle,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.Discard,
            Usage = Usage.RenderTargetOutput
         };
      }

      internal static Direct3DGraphicsDevice Create(RenderForm form) {
         var swapChainDescription = CreateSwapChainDescription(form);

         // Init device, swapchain
         var deviceCreationFlags = DeviceCreationFlags.Debug;
         Device device;
         SwapChain swapChain;
         FeatureLevel[] featureLevels = { FeatureLevel.Level_12_1 };
         Device.CreateWithSwapChain(DriverType.Hardware, deviceCreationFlags, featureLevels, swapChainDescription, out device, out swapChain);
         Console.WriteLine("Created device with feature level: " + device.FeatureLevel);
         var immediateContext = device.ImmediateContext;

         // DXGI ignores window events
         var factory = swapChain.GetParent<Factory>();
         factory.MakeWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAll);

         var graphicsDevice = new Direct3DGraphicsDevice(form, device, swapChain);
         graphicsDevice.Initialize();
         return graphicsDevice;
      }
   }
}
