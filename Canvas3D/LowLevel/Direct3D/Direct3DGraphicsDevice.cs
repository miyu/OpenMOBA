using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
using RectangleF = SharpDX.RectangleF;
using Resource = SharpDX.Direct3D11.Resource;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DGraphicsDevice : IGraphicsDevice, IDisposable {
      private const int BackBufferCount = 2;

      // Lifetime Resources
      private readonly RenderForm _form; // don't dispose
      private readonly Device _device;
      private readonly SwapChain _swapChain;

      // Swap Chain + Resizing
      private Size _renderSize;
      private Texture2D _backBufferRenderTargetTexture;
      private readonly RenderTargetViewBox _backBufferRenderTargetView = new RenderTargetViewBox();
      private Texture2D _backBufferDepthTexture;
      private readonly DepthStencilViewBox _backBufferDepthView = new DepthStencilViewBox();
      private bool _isResizeTriggered;

      // Subsystems
      private readonly RenderStates _renderStates;
      private readonly ImmediateRenderContext _immediateContext;
      private readonly Direct3DLowLevelAssetManager _lowLevelAssetManager;
      private readonly Direct3DTechniqueCollection _techniqueCollection;
      private readonly Direct3DMeshPresets _meshPresets;

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
            ResizeBackBuffer(_renderSize);
            _isResizeTriggered = false;
         }
      }

      public IDeferredRenderContext CreateDeferredRenderContext() {
         return new DeferredRenderContext(new DeviceContext(_device), _renderStates);
      }

      public IVertexBuffer CreateVertexBuffer(VertexPositionNormalColorTexture[] vertices) {
         var buffer = Buffer.Create(_device, BindFlags.VertexBuffer, vertices);
         return new VertexBufferBox { Buffer = buffer, Stride = VertexPositionNormalColorTexture.Size };
      }

      private void ResizeBackBuffer(Size renderSize) {
         DisposeBackBuffersAndViews();

         bool isFirstInitialize = _backBufferRenderTargetView.RenderTargetView == null;

         _swapChain.ResizeBuffers(BackBufferCount, renderSize.Width, renderSize.Height, Format.Unknown, SwapChainFlags.None);
         _backBufferRenderTargetTexture = Resource.FromSwapChain<Texture2D>(_swapChain, 0);
         _backBufferRenderTargetView.RenderTargetView = new RenderTargetView(_device, _backBufferRenderTargetTexture);
         _backBufferDepthTexture = new Texture2D(_device, CreateBackBufferDescription(renderSize));
         _backBufferDepthView.DepthStencilView = new DepthStencilView(_device, _backBufferDepthTexture);
         
         if (isFirstInitialize) {
            _immediateContext.SetRenderTargets(_backBufferDepthView, _backBufferRenderTargetView);
         } else {
            _immediateContext.HandleBackBufferResized(_backBufferRenderTargetView, _backBufferDepthView);
         }
      }

      private void DisposeBackBuffersAndViews() {
         Utilities.Dispose<Texture2D>(ref _backBufferRenderTargetTexture);
         Utilities.Dispose<RenderTargetView>(ref _backBufferRenderTargetView.RenderTargetView);
         Utilities.Dispose<Texture2D>(ref _backBufferDepthTexture);
         Utilities.Dispose<DepthStencilView>(ref _backBufferDepthView.DepthStencilView);
      }

      public void Dispose() {
         DisposeBackBuffersAndViews();

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
               new Rational(60, 1),
               Format.R8G8B8A8_UNorm),
            IsWindowed = true,
            OutputHandle = form.Handle,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.Discard,
            Usage = Usage.RenderTargetOutput
         };
      }

      private static Texture2DDescription CreateBackBufferDescription(Size clientSize) {
         return new Texture2DDescription {
            Format = Format.D32_Float_S8X24_UInt,
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
      }

      private class PixelShaderBox : IPixelShader {
         public PixelShader Shader;
      }

      private class VertexShaderBox : IVertexShader {
         public VertexShader Shader;
         public InputLayout InputLayout;
      }

      internal class DepthStencilViewBox : IDepthStencilView {
         public DepthStencilView DepthStencilView;
      }

      private class RenderTargetViewBox : IRenderTargetView {
         public RenderTargetView RenderTargetView;
      }

      internal class VertexBufferBox : IVertexBuffer {
         public Buffer Buffer;
         public int Stride;
      }

      public class BaseRenderContext : IRenderContext, IDisposable {
         protected DeviceContext _deviceContext;
         protected RenderStates _renderStates;

         protected bool _isVsyncEnabled = true;
         protected DepthConfiguration _currentDepthConfiguration;
         protected RasterizerConfiguration _currentRasterizerConfiguration;
         protected IDepthStencilView _currentDepthStencilView;
         protected IRenderTargetView _currentRenderTargetView;
         protected RectangleF _currentViewportRect;
         protected IVertexBuffer _currentVertexBuffer;


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

         public void GetRenderTargets(out IDepthStencilView dsv, out IRenderTargetView rtv) {
            dsv = _currentDepthStencilView;
            rtv = _currentRenderTargetView;
         }

         public void SetRenderTargets(IDepthStencilView dsvBox, IRenderTargetView rtvBox) {
            if (_currentDepthStencilView == dsvBox && _currentRenderTargetView == rtvBox) {
               return;
            }

            _currentDepthStencilView = dsvBox;
            _currentRenderTargetView = rtvBox;

            UpdateRenderTargetsInternal();
         }

         protected void UpdateRenderTargetsInternal() {
            var depthStencilView = ((DepthStencilViewBox)_currentDepthStencilView)?.DepthStencilView;
            var renderTargetView = ((RenderTargetViewBox)_currentRenderTargetView)?.RenderTargetView;
            _deviceContext.OutputMerger.SetRenderTargets(depthStencilView, renderTargetView);
         }

         public void ClearRenderTarget(Color color) {
            var renderTargetView = ((RenderTargetViewBox)_currentRenderTargetView).RenderTargetView;
            _deviceContext.ClearRenderTargetView(renderTargetView, color);
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

         public void SetVertexBuffer(IVertexBuffer vbBox) {
            _currentVertexBuffer = vbBox;

            var vertexBufferBox = (VertexBufferBox)vbBox;
            _deviceContext.InputAssembler.SetVertexBuffers(
               0,
               new VertexBufferBinding(
                  vertexBufferBox.Buffer,
                  vertexBufferBox.Stride,
                  0));
         }

         public void Draw(int vertices, int verticesOffset) {
            _deviceContext.Draw(vertices, verticesOffset);
         }

         public void DrawInstanced(int vertices, int verticesOffset, int instances, int instancesOffset) {
            _deviceContext.DrawInstanced(vertices, instances, verticesOffset, instancesOffset);
         }

         public void Dispose() {
            Utilities.Dispose<DeviceContext>(ref _deviceContext);
         }
      }

      private class ImmediateRenderContext : BaseRenderContext, IImmediateRenderContext {
         private readonly SwapChain _swapChain;

         public ImmediateRenderContext(DeviceContext deviceContext, RenderStates renderStates, SwapChain swapChain) : base(deviceContext, renderStates) {
            _swapChain = swapChain;
         }

         public void HandleBackBufferResized(RenderTargetViewBox backBufferRenderTargetView, DepthStencilViewBox backBufferDepthView) {
            if (_currentRenderTargetView == backBufferRenderTargetView || _currentDepthStencilView == backBufferDepthView) {
               UpdateRenderTargetsInternal();
            }
         }

         public void Present() {
            _swapChain.Present(GetVsyncEnabled() ? 1 : 0, PresentFlags.None);
         }
      }

      public class DeferredRenderContext : BaseRenderContext, IDeferredRenderContext {
         public DeferredRenderContext(DeviceContext deviceContext, RenderStates renderStates) : base(deviceContext, renderStates) { }
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
         public ITechnique Derivative { get; private set; }

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
            //collection.Derivative = new Technique {
            //   Passes = 1,
            //   PixelShader = assetManager.LoadPixelShaderFromFile("shaders/derivative", "PSMain"),
            //   VertexShader = assetManager.LoadVertexShaderFromFile("shaders/derivative", InputLayoutType.PositionNormalColorTexture, "VSMain")
            //};
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
         public IVertexBuffer VertexBuffer;
         public int Vertices;
         public int VertexBufferOffset;

         public VertexLayout VertexLayout { get; internal set; }

         public void Draw(IRenderContext renderContext, int instances) {
            renderContext.SetPrimitiveTopology(PrimitiveTopology.TriangleList);
            renderContext.SetVertexBuffer(VertexBuffer);
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