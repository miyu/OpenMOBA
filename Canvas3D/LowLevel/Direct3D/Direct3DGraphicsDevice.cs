using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using Buffer = SharpDX.Direct3D11.Buffer;
using Color = SharpDX.Color;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using RectangleF = SharpDX.RectangleF;
using Resource = SharpDX.Direct3D11.Resource;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DGraphicsDevice : IGraphicsDevice {
      private const int BackBufferCount = 2;
      private static readonly Size kUninitializedBackBufferSize = new Size(16, 16);

      // Lifetime Resources
      private readonly RenderForm _form; // don't dispose
      private readonly Device _device;
      private readonly SwapChain _swapChain;

      // Swap Chain + Screen-Size Buffers + Resizing
      private Size _renderSize;
      private Texture2D _backBufferRenderTargetTexture;
      private readonly RenderTargetViewBox _backBufferRenderTargetView = new RenderTargetViewBox { Resolution = kUninitializedBackBufferSize };
      private Texture2D _backBufferDepthTexture;
      private readonly DepthStencilViewBox _backBufferDepthView = new DepthStencilViewBox { Resolution = kUninitializedBackBufferSize };
      private readonly List<(Texture2D, RenderTargetViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[])> _screenSizeRenderTargets = new List<(Texture2D, RenderTargetViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[])>();
      private readonly List<(Texture2D, DepthStencilViewBox, ShaderResourceViewBox)> _screenSizeDepthStencilTargets = new List<(Texture2D, DepthStencilViewBox, ShaderResourceViewBox)>();
      private bool _isResizeTriggered;

      // Subsystems
      private readonly RenderStates _renderStates;
      private readonly ImmediateRenderContext _immediateContext;
      private readonly Direct3DLowLevelAssetManager _lowLevelAssetManager;
      private readonly Direct3DTechniqueCollection _techniqueCollection;
      private readonly Direct3DMeshPresets _meshPresets;

      // Deferred render context pool
      private readonly object deferredRenderContextPoolLock = new object();
      private readonly Stack<DeferredRenderContext> deferredRenderContextPool = new Stack<DeferredRenderContext>();

      private Direct3DGraphicsDevice(RenderForm form, Device device, SwapChain swapChain, DeviceContext deviceImmediateContext) {
         _form = form;
         _device = device;
         _swapChain = swapChain;

         // code smell: init subsystems
         _renderStates = new RenderStates(_device);
         _immediateContext = new ImmediateRenderContext(_device.ImmediateContext, _renderStates, _swapChain);
         _lowLevelAssetManager = new Direct3DLowLevelAssetManager(this);
         _techniqueCollection = Direct3DTechniqueCollection.Create(LowLevelAssetManager);
         _meshPresets = Direct3DMeshPresets.Create(this);
      }

      internal Device InternalD3DDevice => _device;
      public IImmediateRenderContext ImmediateContext => _immediateContext;
      public ILowLevelAssetManager LowLevelAssetManager => _lowLevelAssetManager;
      public ITechniqueCollection TechniqueCollection => _techniqueCollection;
      public IMeshPresets MeshPresets => _meshPresets;

      private void Initialize() {
         // On first frame, must alloc backbuffers and renderview. Same after form resize.
         _isResizeTriggered = true;
         _form.UserResized += (s, e) => _isResizeTriggered = true;
         
         _immediateContext.SetDepthConfiguration(DepthConfiguration.Enabled);
         _immediateContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
      }

      public void DoEvents() {
         if (_isResizeTriggered) {
            _renderSize = _form.ClientSize;
            ResizeScreenSizeBuffers(_renderSize);
            _isResizeTriggered = false;
         }
      }

      public IDeferredRenderContext CreateDeferredRenderContext() {
         lock (deferredRenderContextPoolLock) {
            if (deferredRenderContextPool.Count > 0) {
               return deferredRenderContextPool.Pop();
            }
         }
         Console.WriteLine("Alloc DRC");
         return new DeferredRenderContext(this, new DeviceContext(_device), _renderStates);
      }

      private void ReturnDeferredContext(DeferredRenderContext deferredRenderContext) {
         lock (deferredRenderContextPoolLock) {
            deferredRenderContextPool.Push(deferredRenderContext);
         }
      }

      public IBuffer<T> CreateConstantBuffer<T>(int count) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = new Buffer(_device, count * sizeOfT, ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, 0, sizeOfT);
         return new BufferBox<T> { Buffer = buffer, Count = count, Stride = sizeOfT };
      }

      public IBuffer<T> CreateVertexBuffer<T>(int count) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = new Buffer(_device, count * sizeOfT, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, 0, sizeOfT); ;
         return new BufferBox<T> { Buffer = buffer, Count = count, Stride = sizeOfT };
      }

      public IBuffer<T> CreateVertexBuffer<T>(T[] content) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = Buffer.Create<T>(_device, BindFlags.VertexBuffer, content);
         return new BufferBox<T> { Buffer = buffer, Count = content.Length, Stride = sizeOfT };
      }

      public (IBuffer<T>, IShaderResourceView) CreateStructuredBufferAndView<T>(int count) where T : struct {
         var sizeOfT = Utilities.SizeOf<T>();
         var buffer = new Buffer(_device, count * sizeOfT, ResourceUsage.Dynamic, BindFlags.ShaderResource, CpuAccessFlags.Write, ResourceOptionFlags.BufferStructured, sizeOfT);
         var bufferBox = new BufferBox<T> { Buffer = buffer, Count = count, Stride = sizeOfT };
         var srv = new ShaderResourceView(_device, buffer, 
            new ShaderResourceViewDescription {
               Dimension = ShaderResourceViewDimension.Buffer,
               Format = Format.Unknown,
               Buffer = {
                  ElementCount = count,
                  FirstElement = 0,
                  ElementOffset = 0,
                  ElementWidth = sizeOfT
               }
            });
         var srvBox = new ShaderResourceViewBox { ShaderResourceView = srv };
         return (bufferBox, srvBox);
      }

      public (IRenderTargetView[], IShaderResourceView, IShaderResourceView[]) CreateScreenSizeRenderTarget(int levels) {
         var (tex, rtvs, srv, srvs) = CreateRenderTargetInternal(levels, _backBufferRenderTargetView.Resolution);
         _screenSizeRenderTargets.Add((tex, rtvs, srv, srvs));
         // ReSharper disable CoVariantArrayConversion
         return (rtvs, srv, srvs);
         // ReSharper enable CoVariantArrayConversion
      }

      public (IDisposable, IRenderTargetView[], IShaderResourceView, IShaderResourceView[]) CreateRenderTarget(int levels, Size resolution) {
         return CreateRenderTargetInternal(levels, resolution);
      }

      private (Texture2D, RenderTargetViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[]) CreateRenderTargetInternal(int levels, Size resolution) {
         var texture = new Texture2D(_device,
            new Texture2DDescription {
               Format = Format.R16G16B16A16_UNorm,
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
         var rtvs = new RenderTargetViewBox[levels];
         for (var i = 0; i < levels; i++) {
            rtvs[i] = new RenderTargetViewBox {
               RenderTargetView = new RenderTargetView(_device, texture,
                  new RenderTargetViewDescription {
                     Format = Format.R16G16B16A16_UNorm,
                     Dimension = RenderTargetViewDimension.Texture2DArray,
                     Texture2DArray = {
                        ArraySize = 1,
                        FirstArraySlice = i,
                        MipSlice = 0
                     }
                  }),
               Resolution = resolution
            };
         }
         var srv = new ShaderResourceViewBox {
            ShaderResourceView = new ShaderResourceView(_device, texture,
               new ShaderResourceViewDescription {
                  Format = Format.R16G16B16A16_UNorm,
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
         for (var i = 0; i < levels; i++) {
            srvs[i] = new ShaderResourceViewBox {
               ShaderResourceView = new ShaderResourceView(_device, texture,
                  new ShaderResourceViewDescription {
                     Format = Format.R16G16B16A16_UNorm,
                     Dimension = ShaderResourceViewDimension.Texture2DArray,
                     Texture2DArray = {
                        MipLevels = 1,
                        MostDetailedMip = 0,
                        ArraySize = 1,
                        FirstArraySlice = i
                     }
                  })
            };
         }
         return (texture, rtvs, srv, srvs);
      }

      public (IDepthStencilView, IShaderResourceView) CreateScreenSizeDepthTarget() {
         var (texture, dsvs, srv, srvs) = CreateDepthTextureAndViewsInternal(1, _backBufferDepthView.Resolution);
         srv.ShaderResourceView.Dispose();
         _screenSizeDepthStencilTargets.Add((texture, dsvs[0], srvs[0]));
         return (dsvs[0], srvs[0]);
      }

      public (IDisposable, IDepthStencilView[], IShaderResourceView, IShaderResourceView[]) CreateDepthTextureAndViews(int levels, Size resolution) {
         return CreateDepthTextureAndViewsInternal(levels, resolution);
      }

      private (Texture2D, DepthStencilViewBox[], ShaderResourceViewBox, ShaderResourceViewBox[]) CreateDepthTextureAndViewsInternal(int levels, Size resolution) {
         var texture = new Texture2D(_device,
            new Texture2DDescription {
               Format = Format.R32_Typeless,
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
         var dsvs = new DepthStencilViewBox[levels];
         for (var i = 0; i < levels; i++) {
            dsvs[i] = new DepthStencilViewBox {
               DepthStencilView = new DepthStencilView(_device, texture,
                  new DepthStencilViewDescription {
                     Format = Format.D32_Float,
                     Dimension = DepthStencilViewDimension.Texture2DArray,
                     Texture2DArray = {
                        ArraySize = 1,
                        FirstArraySlice = i,
                        MipSlice = 0
                     }
                  }),
               Resolution = resolution
            };
         }
         var srv = new ShaderResourceViewBox {
            ShaderResourceView = new ShaderResourceView(_device, texture,
               new ShaderResourceViewDescription {
                  Format = Format.R32_Float,
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
         for (var i = 0; i < levels; i++) {
            srvs[i] = new ShaderResourceViewBox {
               ShaderResourceView = new ShaderResourceView(_device, texture,
                  new ShaderResourceViewDescription {
                     Format = Format.R32_Float,
                     Dimension = ShaderResourceViewDimension.Texture2DArray,
                     Texture2DArray = {
                        MipLevels = 1,
                        MostDetailedMip = 0,
                        ArraySize = 1,
                        FirstArraySlice = i
                     }
                  })
            };
         }
         return (texture, dsvs, srv, srvs);
      }

      private void ResizeScreenSizeBuffers(Size renderSize) {
         DisposeScreenSizeBuffersAndViews();

         bool isFirstInitialize = _backBufferRenderTargetView.RenderTargetView == null;

         _swapChain.ResizeBuffers(BackBufferCount, renderSize.Width, renderSize.Height, Format.Unknown, SwapChainFlags.None);
         _backBufferRenderTargetTexture = Resource.FromSwapChain<Texture2D>(_swapChain, 0);
         _backBufferRenderTargetView.RenderTargetView = new RenderTargetView(_device, _backBufferRenderTargetTexture);
         _backBufferRenderTargetView.Resolution = renderSize;
         _backBufferDepthTexture = new Texture2D(_device, CreateBackBufferDescription(renderSize));
         _backBufferDepthView.DepthStencilView = new DepthStencilView(_device, _backBufferDepthTexture);
         _backBufferDepthView.Resolution = renderSize;

         if (isFirstInitialize) {
            _immediateContext.SetRenderTargets(_backBufferDepthView, _backBufferRenderTargetView);
         }
         _immediateContext.HandleBackBufferResized(_backBufferDepthView, _backBufferRenderTargetView);

         for (var i = 0; i < _screenSizeRenderTargets.Count; i++) {
            var (tex, rtvs, srv, srvs) = _screenSizeRenderTargets[i];
            var (newTex, newRtvs, newSrv, newSrvs) = CreateRenderTargetInternal(rtvs.Length, renderSize);
            Utilities.Dispose(ref tex);
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
            Utilities.Dispose(ref tex);
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
            Utilities.Dispose(ref tex);
            foreach (var rtv in rtvs) {
               Utilities.Dispose(ref rtv.RenderTargetView);
            }
            Utilities.Dispose(ref srv.ShaderResourceView);
            foreach (var levelSrv in srvs) {
               Utilities.Dispose(ref levelSrv.ShaderResourceView);
            }
         }
      }

      public void Dispose() {
         DisposeScreenSizeBuffersAndViews();

         _device?.Dispose();
         _swapChain?.Dispose();
         _renderStates?.Dispose();
         _immediateContext?.Dispose();
      }

      public static Direct3DGraphicsDevice Create(RenderForm form) {
         var swapChainDescription = CreateSwapChainDescription(form);

         // Init device, swapchain
         var deviceCreationFlags = DeviceCreationFlags.Debug;
         Device device;
         SwapChain swapChain;
         Device.CreateWithSwapChain(DriverType.Hardware, deviceCreationFlags, swapChainDescription, out device, out swapChain);
         var immediateContext = device.ImmediateContext;

         // DXGI ignores window events
         var factory = swapChain.GetParent<Factory>();
         factory.MakeWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAll);

         var graphicsDeviceManager = new Direct3DGraphicsDevice(form, device, swapChain, immediateContext);
         graphicsDeviceManager.Initialize();
         return graphicsDeviceManager;
      }

      private static SwapChainDescription CreateSwapChainDescription(Control form) {
         return new SwapChainDescription {
            BufferCount = BackBufferCount,
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

      private class Direct3DLowLevelAssetManager : ILowLevelAssetManager {
         private readonly Direct3DGraphicsDevice _graphicsDevice;

         public Direct3DLowLevelAssetManager(Direct3DGraphicsDevice graphicsDevice) {
            _graphicsDevice = graphicsDevice;
         }

         public string BasePath => @"C:\my-repositories\miyu\derp\Canvas3D\Assets";

         public IPixelShader LoadPixelShaderFromFile(string relativePath, string entryPoint = null) {
            var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "PS", "ps_5_0");
            var shader = new PixelShader(_graphicsDevice.InternalD3DDevice, bytecode);
            return new PixelShaderBox { Shader = shader };
         }

         public IVertexShader LoadVertexShaderFromFile(string relativePath, VertexLayout vertexLayout, string entryPoint = null) {
            var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "VS", "vs_5_0");
            var shader = new VertexShader(_graphicsDevice.InternalD3DDevice, bytecode);
            var signature = ShaderSignature.GetInputSignature(bytecode);
            var inputLayout = CreateInputLayout(vertexLayout, signature);
            return new VertexShaderBox { Shader = shader, InputLayout = inputLayout };
         }

         private InputLayout CreateInputLayout(VertexLayout vertexLayout, ShaderSignature signature) {
            if (vertexLayout == VertexLayout.PositionNormalColorTexture) {
               return new InputLayout(_graphicsDevice.InternalD3DDevice, signature, new[] {
                  new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                  new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
                  new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 24, 0, InputClassification.PerVertexData, 0),
                  new InputElement("TEXCOORD", 0, Format.R32G32_Float, 28, 0, InputClassification.PerVertexData, 0),
                  new InputElement("INSTANCE_TRANSFORM", 0, Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
                  new InputElement("INSTANCE_TRANSFORM", 1, Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1),
                  new InputElement("INSTANCE_TRANSFORM", 2, Format.R32G32B32A32_Float, 32, 1, InputClassification.PerInstanceData, 1),
                  new InputElement("INSTANCE_TRANSFORM", 3, Format.R32G32B32A32_Float, 48, 1, InputClassification.PerInstanceData, 1),
               });
            }
            throw new NotSupportedException("Unsupported Input Layout: " + vertexLayout);
         }

         private byte[] CompileShaderBytecodeFromFileOrThrow(string path, string entryPoint, string profile) {
            // D3D expects row-major matrices but defaults to sending column-major matrices to GPU, so it'll do
            // a transpose. This tells it to keep the row-major-ness. This is because our code actually works
            // in column-major, so the extra transpose is the opposite of what we want.
            var shaderFlags = ShaderFlags.PackMatrixRowMajor;
            using (var include = new IncludeImpl(path)) {
               var compilationResult = ShaderBytecode.CompileFromFile(path, entryPoint, profile, shaderFlags, include: include);
               if (compilationResult.Bytecode == null || compilationResult.HasErrors) {
                  throw new ShaderCompilationException(compilationResult.ResultCode.Code, compilationResult.Message);
               }
               return compilationResult.Bytecode;
            }
         }

         private class IncludeImpl : Include {
            private readonly string _firstShaderPath;

            public IncludeImpl(string firstShaderPath) {
               _firstShaderPath = firstShaderPath;
            }

            public IDisposable Shadow { get; set; }

            public void Dispose() {
               Shadow?.Dispose();
            }

            public Stream Open(IncludeType type, string fileName, Stream parentStream) {
               Trace.Assert(type == IncludeType.Local);
               var sourcerPath = parentStream is FileStream ? ((FileStream)parentStream).Name : _firstShaderPath;
               var resolvedPath = Path.Combine(new FileInfo(sourcerPath).DirectoryName, fileName);
               if (!File.Exists(resolvedPath)) {
                  Console.WriteLine("Shader resolution failed");
                  Console.WriteLine($"FileName: {fileName}");
                  Console.WriteLine($"Sourcer: {sourcerPath}");
                  Console.WriteLine($"Resolved: {resolvedPath}");
               }
               return File.OpenRead(resolvedPath);
            }

            public void Close(Stream stream) {
               stream.Dispose();
            }
         }


         public (ITexture2D, IShaderResourceView) CreateSolidTexture(Color4 c) {
            var _d3d = _graphicsDevice._device;
            var texture = new Texture2D(_d3d, new Texture2DDescription {
               Format = Format.R32G32B32A32_Float,
               ArraySize = 1,
               BindFlags = BindFlags.ShaderResource,
               CpuAccessFlags = CpuAccessFlags.Write,
               Height = 1,
               Width = 1,
               MipLevels = 1,
               OptionFlags = ResourceOptionFlags.None,
               SampleDescription = new SampleDescription(1, 0),
               Usage = ResourceUsage.Dynamic
            });

            DataStream stream;
            _d3d.ImmediateContext.MapSubresource(texture, 0, 0, MapMode.WriteDiscard, MapFlags.None, out stream);
            stream.Write(c);
            _d3d.ImmediateContext.UnmapSubresource(texture, 0);

            var srv = new ShaderResourceView(_d3d, texture);
            return (new Texture2DBox { Texture = texture }, new ShaderResourceViewBox { ShaderResourceView = srv });
         }

         public (ITexture2D, IShaderResourceView) CreateSolidCubeTexture(Color4 c) {
            return CreateSolidCubeTexture(c, c, c, c, c, c);
         }

         public unsafe (ITexture2D, IShaderResourceView) CreateSolidCubeTexture(Color4 posx, Color4 negx, Color4 posy, Color4 negy, Color4 posz, Color4 negz) {
            DataBox Wrap(Color4* p) => new DataBox(new IntPtr(p), 4 * 4, 0);

            var _d3d = _graphicsDevice._device;
            var texture = new Texture2D(_d3d, new Texture2DDescription {
               Format = Format.R32G32B32A32_Float,
               ArraySize = 6,
               BindFlags = BindFlags.ShaderResource,
               CpuAccessFlags = CpuAccessFlags.Write,
               Height = 1,
               Width = 1,
               MipLevels = 1,
               OptionFlags = ResourceOptionFlags.TextureCube,
               SampleDescription = new SampleDescription(1, 0),
               Usage = ResourceUsage.Default
            }, new[] { Wrap(&posx), Wrap(&negx), Wrap(&posy), Wrap(&negy), Wrap(&posz), Wrap(&negz) });

            var srv = new ShaderResourceView(_d3d, texture);
            return (new Texture2DBox { Texture = texture }, new ShaderResourceViewBox { ShaderResourceView = srv });
         }
      }

      internal class DepthStencilViewBox : IDepthStencilView {
         public DepthStencilView DepthStencilView;
         public Size Resolution { get; set; }

         public void MoveAssignFrom(DepthStencilViewBox other) {
            Utilities.Dispose(ref DepthStencilView);
            DepthStencilView = other.DepthStencilView;
            other.DepthStencilView = null;
            Resolution = other.Resolution;
            other.Resolution = new Size(-1, -1);
         }
      }

      internal class RenderTargetViewBox : IRenderTargetView {
         public RenderTargetView RenderTargetView;
         public Size Resolution { get; set; }

         public void MoveAssignFrom(RenderTargetViewBox other) {
            Utilities.Dispose(ref RenderTargetView);
            RenderTargetView = other.RenderTargetView;
            other.RenderTargetView = null;
            Resolution = other.Resolution;
            other.Resolution = new Size(-1, -1);
         }
      }

      private class ShaderResourceViewBox : IShaderResourceView {
         public ShaderResourceView ShaderResourceView;

         public void MoveAssignFrom(ShaderResourceViewBox other) {
            Utilities.Dispose(ref ShaderResourceView);
            ShaderResourceView = other.ShaderResourceView;
            other.ShaderResourceView = null;
         }
      }

      private class PixelShaderBox : IPixelShader {
         public PixelShader Shader;
      }

      private class VertexShaderBox : IVertexShader {
         public VertexShader Shader;
         public InputLayout InputLayout;
      }

      private class BufferBox<T> : IBuffer<T> where T : struct {
         public Buffer Buffer;
         public int Count;
         public int Stride;
      }

      private class Texture2DBox : ITexture2D {
         public Texture2D Texture;
      }

      internal class BaseRenderContext : IRenderContext, IDisposable {
         protected DeviceContext _deviceContext;
         protected RenderStates _renderStates;

         protected bool _isVsyncEnabled = true;
         protected DepthConfiguration _currentDepthConfiguration;
         protected RasterizerConfiguration _currentRasterizerConfiguration;
         protected DepthStencilViewBox _currentDepthStencilView;
         protected RenderTargetViewBox[] _currentRenderTargetViews = new RenderTargetViewBox[4];
         protected RenderTargetView[] _preallocatedRtvArray = new RenderTargetView[4];
         protected RectangleF _currentViewportRect;

         public BaseRenderContext(DeviceContext deviceContext, RenderStates renderStates) {
            _deviceContext = deviceContext;
            _renderStates = renderStates;
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

         public void SetViewportRect(RectangleF rectangle) {
            if (_currentViewportRect != rectangle) {
               _currentViewportRect = rectangle;

               _deviceContext.Rasterizer.SetViewport(new ViewportF(rectangle));
            }
         }

         public void SetPixelShader(IPixelShader shader) {
            _deviceContext.PixelShader.Set(((PixelShaderBox)shader).Shader);
         }

         public void SetVertexShader(IVertexShader shader) {
            var box = (VertexShaderBox)shader;
            _deviceContext.VertexShader.Set(box.Shader);
            _deviceContext.InputAssembler.InputLayout = box.InputLayout;
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

         public void SetConstantBuffer<T>(int slot, IBuffer<T> buffer, RenderStage stages) where T : struct {
            var box = (BufferBox<T>)buffer;
            if ((stages & RenderStage.Pixel) != 0) {
               _deviceContext.PixelShader.SetConstantBuffer(slot, box.Buffer);
            }
            if ((stages & RenderStage.Vertex) != 0) {
               _deviceContext.VertexShader.SetConstantBuffer(slot, box.Buffer);
            }
         }

         public void SetShaderResource(int slot, IShaderResourceView view, RenderStage stages) {
            var box = (ShaderResourceViewBox)view;
            if ((stages & RenderStage.Pixel) != 0) {
               _deviceContext.PixelShader.SetShaderResource(slot, box?.ShaderResourceView);
            }
            if ((stages & RenderStage.Vertex) != 0) {
               _deviceContext.VertexShader.SetShaderResource(slot, box?.ShaderResourceView);
            }
         }

         public void Draw(int vertices, int verticesOffset) {
            _deviceContext.Draw(vertices, verticesOffset);
         }

         public void DrawInstanced(int vertices, int verticesOffset, int instances, int instancesOffset) {
            _deviceContext.DrawInstanced(vertices, instances, verticesOffset, instancesOffset);
         }

         public void Update<T>(IBuffer<T> buffer, T item) where T : struct {
            var box = (BufferBox<T>)buffer;
            var db = _deviceContext.MapSubresource(box.Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
            Utilities.Write(db.DataPointer, ref item);
            _deviceContext.UnmapSubresource(box.Buffer, 0);
         }

         public void Update<T>(IBuffer<T> buffer, IntPtr data, int count) where T : struct {
            var box = (BufferBox<T>)buffer;
            var db = _deviceContext.MapSubresource(box.Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
            Utilities.CopyMemory(db.DataPointer, data, count * box.Stride);
            _deviceContext.UnmapSubresource(box.Buffer, 0);
         }

         public void Update<T>(IBuffer<T> buffer, T[] arr, int offset, int count) where T : struct {
            var box = (BufferBox<T>)buffer;
            var db = _deviceContext.MapSubresource(box.Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
            Utilities.Write(db.DataPointer, arr, offset, count);
            _deviceContext.UnmapSubresource(box.Buffer, 0);
         }

         public void Dispose() {
            Utilities.Dispose<DeviceContext>(ref _deviceContext);
         }
      }

      private class ImmediateRenderContext : BaseRenderContext, IImmediateRenderContext {
         private readonly SwapChain _swapChain;
         private DepthStencilViewBox _backBufferDepthView;
         private RenderTargetViewBox _backBufferRenderTargetView;

         public ImmediateRenderContext(DeviceContext deviceContext, RenderStates renderStates, SwapChain swapChain) : base(deviceContext, renderStates) {
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
         }
      }

      private class DeferredRenderContext : BaseRenderContext, IDeferredRenderContext {
         private readonly Direct3DGraphicsDevice _graphicsDevice;

         public DeferredRenderContext(Direct3DGraphicsDevice graphicsDevice, DeviceContext deviceContext, RenderStates renderStates) : base(deviceContext, renderStates) {
            _graphicsDevice = graphicsDevice;
         }

         public ICommandList FinishCommandListAndFree() {
            var box = new CommandListBox { CommandList = _deviceContext.FinishCommandList(false) };
            _graphicsDevice.ReturnDeferredContext(this);
            return box;
         }
      }

      private class CommandListBox : ICommandList {
         private static int instanceCount = 0;
         private int disposed = 0;
         public CommandList CommandList;

         public CommandListBox() {
            if (Interlocked.Increment(ref instanceCount) > 512) {
               Console.Error.WriteLine("Warning: Make sure CommandListBox is being disposed! (>512 unfreed instances)");
            }
            //Console.WriteLine(instanceCount);
         }

         ~CommandListBox() {
            if (CommandList != null) {
               Console.Error.WriteLine("Warning: Finalizer called on undisposed command list. This will leak memory (SharpDX lacks a finalizer on CommandList).");
            }
            Dispose();
         }

         public void Dispose() {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 0) {
               Utilities.Dispose(ref CommandList);
               Interlocked.Decrement(ref instanceCount);
            }
         }
      }

      public class RenderStates : IDisposable {
         private DepthStencilState _depthEnable;
         private DepthStencilState _depthDisable;
         private RasterizerState _rasterizerFillFront;
         private RasterizerState _rasterizerFillBack;

         public RenderStates(Device device) {
            _depthEnable = new DepthStencilState(device, DepthStencilDesc(true));
            _depthDisable = new DepthStencilState(device, DepthStencilDesc(false));
            _rasterizerFillFront = new RasterizerState(device, RasterizerDesc(true));
            _rasterizerFillBack = new RasterizerState(device, RasterizerDesc(false));
         }

         public DepthStencilState DepthDisable => _depthDisable;
         public DepthStencilState DepthEnable => _depthEnable;
         public RasterizerState RasterizerFillFront => _rasterizerFillFront;
         public RasterizerState RasterizerFillBack => _rasterizerFillBack;

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

         private static RasterizerStateDescription RasterizerDesc(bool frontFacesElseBackFace) => new RasterizerStateDescription {
            CullMode = frontFacesElseBackFace ? CullMode.Back : CullMode.Front,
            FillMode = FillMode.Solid,
            IsDepthClipEnabled = false
         };
      }

      public class Direct3DTechniqueCollection : ITechniqueCollection {
         private Direct3DTechniqueCollection() { }

         public ITechnique Forward { get; private set; }
         public ITechnique ForwardDepthOnly { get; private set; }
         public ITechnique DeferredToGBuffer { get; private set; }
         public ITechnique DeferredFromGBuffer { get; private set; }

         public static Direct3DTechniqueCollection Create(ILowLevelAssetManager lowLevelAssetManager) {
            var collection = new Direct3DTechniqueCollection();
            collection.Forward = new Technique {
               Passes = 1,
               PixelShader = lowLevelAssetManager.LoadPixelShaderFromFile("shaders/forward", "PSMain"),
               VertexShader = lowLevelAssetManager.LoadVertexShaderFromFile("shaders/forward", VertexLayout.PositionNormalColorTexture, "VSMain")
            };
            collection.ForwardDepthOnly = new Technique {
               Passes = 1,
               PixelShader = lowLevelAssetManager.LoadPixelShaderFromFile("shaders/forward_depth_only", "PSMain"),
               VertexShader = lowLevelAssetManager.LoadVertexShaderFromFile("shaders/forward_depth_only", VertexLayout.PositionNormalColorTexture, "VSMain")
            };
            collection.DeferredToGBuffer = new Technique {
               Passes = 1,
               PixelShader = lowLevelAssetManager.LoadPixelShaderFromFile("shaders/deferred_to_gbuffer", "PSMain"),
               VertexShader = lowLevelAssetManager.LoadVertexShaderFromFile("shaders/deferred_to_gbuffer", VertexLayout.PositionNormalColorTexture, "VSMain")
            };
            collection.DeferredFromGBuffer = new Technique {
               Passes = 1,
               PixelShader = lowLevelAssetManager.LoadPixelShaderFromFile("shaders/deferred_from_gbuffer", "PSMain"),
               VertexShader = lowLevelAssetManager.LoadVertexShaderFromFile("shaders/deferred_from_gbuffer", VertexLayout.PositionNormalColorTexture, "VSMain")
            };
            return collection;
         }

         private class Technique : ITechnique {
            public IPixelShader PixelShader { get; set; }
            public IVertexShader VertexShader { get; set; }

            public int Passes { get; set; }

            public void BeginPass(IRenderContext renderContext, int pass) {
               if (pass != 0) {
                  throw new ArgumentOutOfRangeException();
               }

               renderContext.SetPixelShader(PixelShader);
               renderContext.SetVertexShader(VertexShader);
            }
         }
      }

      internal class Direct3DMesh : IMesh {
         public IBuffer<VertexPositionNormalColorTexture> VertexBuffer;
         public int Vertices;
         public int VertexBufferOffset;

         public VertexLayout VertexLayout { get; internal set; }

         public void Draw(IRenderContext renderContext, int instances) {
            renderContext.SetPrimitiveTopology(PrimitiveTopology.TriangleList);
            renderContext.SetVertexBuffer(0, VertexBuffer);
            renderContext.DrawInstanced(Vertices, VertexBufferOffset, instances, 0);
         }
      }

      private class Direct3DMeshPresets : IMeshPresets {
         private Direct3DMeshPresets() { }

         public IMesh UnitCube { get; set; }
         public IMesh UnitPlaneXY { get; set; }
         public IMesh UnitSphere { get; set; }

         public static Direct3DMeshPresets Create(Direct3DGraphicsDevice device) {
            var presets = new Direct3DMeshPresets();

            presets.UnitCube = new Direct3DMesh {
               VertexBuffer = device.CreateVertexBuffer(
                  ConvertLeftHandToRightHandTriangleList(HardcodedMeshPresets.ColoredCubeVertices)),
               Vertices = HardcodedMeshPresets.ColoredCubeVertices.Length,
               VertexBufferOffset = 0
            };

            presets.UnitPlaneXY = new Direct3DMesh {
               VertexBuffer = device.CreateVertexBuffer(
                  ConvertLeftHandToRightHandTriangleList(HardcodedMeshPresets.PlaneXYVertices)),
               Vertices = HardcodedMeshPresets.PlaneXYVertices.Length,
               VertexBufferOffset = 0
            };

            presets.UnitSphere = new Direct3DMesh {
               VertexBuffer = device.CreateVertexBuffer(HardcodedMeshPresets.Sphere),
               Vertices = HardcodedMeshPresets.Sphere.Length,
               VertexBufferOffset = 0
            };

            return presets;
         }

         private static VertexPositionNormalColorTexture[] ConvertLeftHandToRightHandTriangleList(VertexPositionNormalColorTexture[] vertices) {
            var results = new VertexPositionNormalColorTexture[vertices.Length];
            for (var i = 0; i < vertices.Length; i++) {
               results[i] = new VertexPositionNormalColorTexture(
                  new Vector3(
                     vertices[i].Position.X,
                     vertices[i].Position.Y,
                     -vertices[i].Position.Z),
                  new Vector3(
                     vertices[i].Normal.X,
                     vertices[i].Normal.Y,
                     -vertices[i].Normal.Z),
                  vertices[i].Color,
                  vertices[i].UV);
            }
            return results;
         }
      }
   }
}