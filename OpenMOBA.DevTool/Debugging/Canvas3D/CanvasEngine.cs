using System;
using System.Numerics;
using System.Windows.Forms;
using OpenMOBA.DevTool.Debugging.Canvas3D;
using OpenMOBA.Foundation;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.Windows;
using Size = System.Drawing.Size;
using Buffer = SharpDX.Direct3D11.Buffer;
using Direct3DDevice = SharpDX.Direct3D11.Device;
using Resource = SharpDX.Direct3D11.Resource;

namespace Shade {
   public interface IGraphicsDevice {
      IRenderContext RenderContext { get; }
      void DoEvents();
   }

   public interface IRenderContext {
      void ClearTargetAndDepthBuffers(Color color);
      void Present();

      void SetPixelShader(IPixelShader shader);
      void SetVertexShader(IVertexShader shader);
   }

   public class Direct3DGraphicsDevice : IGraphicsDevice, IRenderContext, IDisposable {
      private const int BackBufferCount = 2;

      // Lifetime Resources
      private readonly RenderForm _form; // don't dispose
      private readonly Direct3DDevice _device;
      private readonly SwapChain _swapChain;
      private readonly DeviceContext _immediateContext;

      // Swap Chain + Resizing
      private Size _renderSize;
      private Texture2D _backBuffer;
      private RenderTargetView _renderView;
      private Texture2D _depthBuffer;
      private DepthStencilView _depthView;
      private bool _isResizeTriggered;

      // Camera Projection State, subsystem-like stuff
//      private Matrix _proj;

      private Direct3DGraphicsDevice(RenderForm form, Direct3DDevice device, SwapChain swapChain, DeviceContext immediateContext) {
         _form = form;
         _device = device;
         _swapChain = swapChain;
         _immediateContext = immediateContext;

         // code smell: init subsystems
         AssetManager = new Direct3DAssetManager(this);
      }

      internal Direct3DDevice InternalD3DDevice => _device;
      public IRenderContext RenderContext => this;
      public IAssetManager AssetManager { get; }

      private void Initialize() {
         // On first frame, must alloc backbuffers and renderview. Same after form resize.
         _isResizeTriggered = true;
         _form.UserResized += (s, e) => _isResizeTriggered = true;

         // z-near is positive
         var depthStencilStateDescription = new DepthStencilStateDescription {
            IsDepthEnabled = true,
            DepthComparison = Comparison.Less,
            DepthWriteMask = DepthWriteMask.All,
            IsStencilEnabled = false,
            StencilReadMask = 0xff,
            StencilWriteMask = 0xff
         };
         _immediateContext.OutputMerger.DepthStencilState = new DepthStencilState(_device, depthStencilStateDescription);

         var rasterizerStateDescription = new RasterizerStateDescription {
            CullMode = CullMode.Back,
            FillMode = FillMode.Solid,
//            FillMode = FillMode.Wireframe,
            IsDepthClipEnabled = false
         };
         _immediateContext.Rasterizer.State = new RasterizerState(_device, rasterizerStateDescription);
      }

      public void DoEvents() {
         if (_isResizeTriggered) {
            _renderSize = _form.ClientSize;
            SetResizeBackBufferSize(_renderSize);
            _isResizeTriggered = false;

            // Setup new projection matrix with correct aspect ratio
//            _proj = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, _renderSize.Width / (float)_renderSize.Height, 0.1f, 100.0f);
         }
      }

      void IRenderContext.ClearTargetAndDepthBuffers(Color color) {
         _immediateContext.ClearDepthStencilView(_depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
         _immediateContext.ClearRenderTargetView(_renderView, color);
      }

      void IRenderContext.Present() {
         _swapChain.Present(1, PresentFlags.None);
      }

      void IRenderContext.SetPixelShader(IPixelShader shader) {
         _immediateContext.PixelShader.Set(((PixelShaderBox)shader).Shader);
      }

      void IRenderContext.SetVertexShader(IVertexShader shader) {
         var box = (VertexShaderBox)shader;
         _immediateContext.VertexShader.Set(box.Shader);
         _immediateContext.InputAssembler.InputLayout = box.InputLayout;
      }

      private void SetResizeBackBufferSize(Size renderSize) {
         DisposeSwapChainBuffersAndViews();

         _swapChain.ResizeBuffers(BackBufferCount, renderSize.Width, renderSize.Height, Format.Unknown, SwapChainFlags.None);
         _backBuffer = Resource.FromSwapChain<Texture2D>(_swapChain, 0);
         _renderView = new RenderTargetView(_device, _backBuffer);
         _depthBuffer = new Texture2D(_device, CreateBackBufferDescription(renderSize));
         _depthView = new DepthStencilView(_device, _depthBuffer);

         _immediateContext.Rasterizer.SetViewport(new Viewport(0, 0, renderSize.Width, renderSize.Height, 0.0f, 1.0f));
         _immediateContext.OutputMerger.SetTargets(_depthView, _renderView);
      }

      private void DisposeSwapChainBuffersAndViews() {
         Utilities.Dispose(ref _backBuffer);
         Utilities.Dispose(ref _renderView);
         Utilities.Dispose(ref _depthBuffer);
         Utilities.Dispose(ref _depthView);
      }

      public void Dispose() {
         DisposeSwapChainBuffersAndViews();

         _device?.Dispose();
         _swapChain?.Dispose();
         _immediateContext?.Dispose();
      }

      public static Direct3DGraphicsDevice Create(RenderForm form) {
         var swapChainDescription = CreateSwapChainDescription(form);

         // Init device, swapchain
         var deviceCreationFlags = DeviceCreationFlags.Debug;
         Direct3DDevice device;
         SwapChain swapChain;
         Direct3DDevice.CreateWithSwapChain(DriverType.Hardware, deviceCreationFlags, swapChainDescription, out device, out swapChain);
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

      private class Direct3DAssetManager : IAssetManager {
         private readonly Direct3DGraphicsDevice _graphicsDevice;

         public Direct3DAssetManager(Direct3DGraphicsDevice graphicsDevice) {
            _graphicsDevice = graphicsDevice;
         }

         public string BasePath => @"C:\my-repositories\miyu\derp\OpenMOBA.DevTool\Debugging\Canvas3D\Assets";

         public IPixelShader LoadPixelShaderFromFile(string relativePath, string entryPoint = null) {
            var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "PS", "ps_4_0");
            var shader = new PixelShader(_graphicsDevice.InternalD3DDevice, bytecode);
            return new PixelShaderBox { Shader = shader };
         }

         public IVertexShader LoadVertexShaderFromFile(string relativePath, string entryPoint = null) {
            var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "VS", "vs_4_0");
            var shader = new VertexShader(_graphicsDevice.InternalD3DDevice, bytecode);
            var signature = ShaderSignature.GetInputSignature(bytecode);
            var inputLayout = new InputLayout(_graphicsDevice.InternalD3DDevice, signature, new[]
            {
               new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
               new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 12, 0)
            });
            return new VertexShaderBox { Shader = shader, InputLayout = inputLayout };
         }

         private byte[] CompileShaderBytecodeFromFileOrThrow(string path, string entryPoint, string profile) {
            // D3D expects row-major matrices but defaults to sending column-major matrices to GPU, so it'll do
            // a transpose. This tells it to keep the row-major-ness. This is because our code actually works
            // in column-major, so the extra transpose is the opposite of what we want.
            var shaderFlags = ShaderFlags.PackMatrixRowMajor;
            var compilationResult = ShaderBytecode.CompileFromFile(path, entryPoint, profile, shaderFlags);
            if (compilationResult.Bytecode == null || compilationResult.HasErrors) {
               throw new ShaderCompilationException(compilationResult.ResultCode.Code, compilationResult.Message);
            }
            return compilationResult.Bytecode;
         }
      }

      private class PixelShaderBox : IPixelShader {
         public PixelShader Shader { get; set; }
      }

      private class VertexShaderBox : IVertexShader {
         public VertexShader Shader { get; set; }
         public InputLayout InputLayout { get; set; }
      }
   }

   public class CanvasApplication { }

//   public class CanvasEngine : Game {
//      private readonly CanvasProgram program;
//      private SpriteBatch spriteBatch;
//
//      public CanvasEngine(CanvasProgram program) {
//         this.program = program;
//
//         var graphicsDeviceManager = new GraphicsDeviceManager(this) {
//            PreferredBackBufferWidth = 1280,
//            PreferredBackBufferHeight = 720,
//            DeviceCreationFlags = DeviceCreationFlags.Debug
//         };
//      }
//
//      public Canvas RootCanvas { get; private set; }
//
//      protected override void Initialize() {
//         base.Initialize();
//         spriteBatch = new SpriteBatch(GraphicsDevice);
//
//         Canvas.Engine = this;
//         RootCanvas = new Canvas(GraphicsDevice.BackBuffer.Width, GraphicsDevice.BackBuffer.Height);
//
//         program.Engine = this;
//         program.Setup();
//      }
//
//      protected override void Draw(GameTime gameTime) {
//         base.Draw(gameTime);
//         program.Render(gameTime);
//         GraphicsDevice.Clear(Color.Black);
//         spriteBatch.Begin();
//         spriteBatch.Draw(RootCanvas.RenderTarget, new Vector2(0, 0), Color.White);
//         spriteBatch.End();
//      }
//
//      protected override void Update(GameTime gameTime) {
//         base.Update(gameTime);
//         program.Step(gameTime);
//      }
//   }
}
