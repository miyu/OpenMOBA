using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Canvas3D.LowLevel;
using Canvas3D.LowLevel.Direct3D;
using Canvas3D.LowLevel.Helpers;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using Buffer = SharpDX.Direct3D11.Buffer;
using Color = SharpDX.Color;
using Device = SharpDX.Direct3D11.Device;
using RectangleF = SharpDX.RectangleF;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace Canvas3D {
   public interface IAssetManager {
      IMesh GetPresetMesh(MeshPreset preset);
   }

   public interface IRenderer3D {
      void ClearScene();
      void SetCamera(Vector3 cameraEye, Matrix projView);
      void AddRenderable(MeshPreset preset, Matrix worldCm);
      void AddRenderable(IMesh mesh, Matrix worldCm);
      void AddRenderable(IMesh mesh, RenderJobDescription info);
      void AddRenderJobBatch(RenderJobBatch batch);
      void AddSpotlight(Vector3 position, Vector3 lookat, float theta, Color color, float far, float daRatioConstant, float daRatioLinear, float daRatioQuadratic, float edgeSpotlightAttenuationPercent = 1.0f / 256.0f);
      void AddSpotlight(BatchedRenderer3D.SpotlightInfo info);
      void RenderScene();
   }

   public class BatchedRenderer3D : IAssetManager, IRenderer3D {
      private readonly Dictionary<IMesh, RenderJobBatch> defaultRenderJobBatchesByMesh = new Dictionary<IMesh, RenderJobBatch>();
      private readonly List<RenderJobBatch> renderJobBatches = new List<RenderJobBatch>();
      private readonly List<SpotlightInfo> spotlightInfos = new List<SpotlightInfo>();

      private readonly IGraphicsDevice _graphicsDevice;
      //private readonly Device _d3d;
      private readonly IBuffer<SceneConstantBufferData> _sceneBuffer;
      private readonly IBuffer<BatchConstantBufferData> _batchBuffer;
      private readonly List<IBuffer<RenderJobDescription>> _instancingBuffers;
      private readonly IBuffer<SpotlightDescription> _shadowMapEntriesBuffer;
      private readonly IShaderResourceView _shadowMapEntriesBufferSrv;
      private readonly IDisposable _lightDepthTexture;
      private readonly IDepthStencilView[] _lightDepthStencilViews;
      private readonly IShaderResourceView _lightShaderResourceView;
      private readonly IShaderResourceView[] _lightShaderResourceViews;
      private readonly ITexture2D _whiteTexture;
      private readonly IShaderResourceView _whiteTextureShaderResourceView;
      private readonly ITexture2D _whiteCubeMap;
      private readonly IShaderResourceView _whiteCubeMapShaderResourceView;
      private readonly ITexture2D _limeTexture;
      private readonly IShaderResourceView _limeTextureShaderResourceView;
      private readonly ITexture2D _limeCubeMap;
      private readonly IShaderResourceView _limeCubeMapShaderResourceView;
      private readonly ITexture2D _flatColoredCubeMap;
      private readonly IShaderResourceView _flatColoredCubeMapShaderResourceView;
      private Vector3 _cameraEye;
      private Matrix _projView;

      public BatchedRenderer3D(IGraphicsDevice graphicsDevice) {
         _graphicsDevice = graphicsDevice;
         _sceneBuffer = _graphicsDevice.CreateConstantBuffer<SceneConstantBufferData>(1);
         _batchBuffer = _graphicsDevice.CreateConstantBuffer<BatchConstantBufferData>(1);
         _instancingBuffers = new List<IBuffer<RenderJobDescription>>();

         const int kMaxPreallocatedInstanceBufferPower = 14;
         for (var i = 0; i <= kMaxPreallocatedInstanceBufferPower; i++) {
            _instancingBuffers.Add(_graphicsDevice.CreateVertexBuffer<RenderJobDescription>(1 << i));
         }

         (_shadowMapEntriesBuffer, _shadowMapEntriesBufferSrv) = _graphicsDevice.CreateStructuredBufferAndView<SpotlightDescription>(256);
         (_lightDepthTexture, _lightDepthStencilViews, _lightShaderResourceView, _lightShaderResourceViews) = _graphicsDevice.CreateDepthTextureAndViews(10, new Size(2048, 2048));

         var lowLevelAssetManager = _graphicsDevice.LowLevelAssetManager;
         (_whiteTexture, _whiteTextureShaderResourceView) = lowLevelAssetManager.CreateSolidTexture(Color4.White);
         (_whiteCubeMap, _whiteCubeMapShaderResourceView) = lowLevelAssetManager.CreateSolidCubeTexture(Color4.White);
         (_limeTexture, _limeTextureShaderResourceView) = lowLevelAssetManager.CreateSolidTexture(Color.Lime);
         (_limeCubeMap, _limeCubeMapShaderResourceView) = lowLevelAssetManager.CreateSolidCubeTexture(Color.Lime);
         (_flatColoredCubeMap, _flatColoredCubeMapShaderResourceView) = lowLevelAssetManager.CreateSolidCubeTexture(Color.Cyan, Color.Magenta, Color.Blue, Color.Yellow, Color.Red, Color.Lime);

         Trace.Assert(Utilities.SizeOf<SpotlightInfo>() == SpotlightInfo.Size);
         Trace.Assert(Utilities.SizeOf<AtlasLocation>() == AtlasLocation.SIZE);
         Trace.Assert(Utilities.SizeOf<SpotlightDescription>() == SpotlightDescription.Size);
         Trace.Assert(Utilities.SizeOf<RenderJobDescription>() == RenderJobDescription.Size);
      }

      public ITechniqueCollection Techniques => _graphicsDevice.TechniqueCollection;

      public void ClearScene() {
         renderJobBatches.Clear();
         foreach (var kvp in defaultRenderJobBatchesByMesh) {
            kvp.Value.Jobs.Clear();
            renderJobBatches.Add(kvp.Value);
         }
         spotlightInfos.Clear();
      }

      public void SetCamera(Vector3 cameraEye, Matrix projView) {
         _cameraEye = cameraEye;
         _projView = projView;
      }

      public IMesh GetPresetMesh(MeshPreset preset) {
         if (preset == MeshPreset.UnitCube) {
            return _graphicsDevice.MeshPresets.UnitCube;
         } else if (preset == MeshPreset.UnitPlaneXY) {
            return _graphicsDevice.MeshPresets.UnitPlaneXY;
         } else if (preset == MeshPreset.UnitSphere) {
            return _graphicsDevice.MeshPresets.UnitSphere;
         } else {
            throw new NotSupportedException();
         }
      }

      public void AddRenderable(MeshPreset preset, Matrix worldCm) {
         AddRenderable(GetPresetMesh(preset), worldCm);
      }

      public void AddRenderable(IMesh mesh, Matrix worldCm) {
         AddRenderable(mesh, new RenderJobDescription {
            WorldTransform = worldCm,
         });
      }

      public void AddRenderable(IMesh mesh, RenderJobDescription info) {
         if (!defaultRenderJobBatchesByMesh.TryGetValue(mesh, out var batch)) {
            batch = defaultRenderJobBatchesByMesh[mesh] = new RenderJobBatch();
            batch.Mesh = mesh;
            renderJobBatches.Add(batch);
         }
         batch.Jobs.Add(info);
      }

      public void AddRenderJobBatch(RenderJobBatch batch) {
         renderJobBatches.Add(batch);
      }

      public void AddSpotlight(Vector3 position, Vector3 lookat, float theta, Color color, float far, float daRatioConstant, float daRatioLinear, float daRatioQuadratic, float edgeSpotlightAttenuationPercent = 1.0f / 256.0f) {
         var proj = MatrixCM.PerspectiveFovRH(theta, 1.0f, 0.1f, far);

         var up = Vector3.Up; // todo: handle degenerate
         var view = MatrixCM.LookAtRH(position, lookat, up);

         // solve distance attenuation constants: 1/256 = atten = 1 / (x * darc + far * x * darl + far * far * x * darq)
         // 256 = x * darc + far * x * darl + far * far * x * darq
         // 256 = x * (darc + far * darl + far * far * darq)
         float x = 256 / (daRatioConstant + far * daRatioLinear + far * far * daRatioQuadratic);
         //Console.WriteLine(x + " " + daRatioConstant + " " + daRatioLinear + " " + daRatioQuadratic);
         var direction = lookat - position;
         direction.Normalize();
         //Console.WriteLine("@8: " + (daRatioConstant * x + daRatioLinear * x * 8 + daRatioQuadratic * x * 8 * 8));
         //Console.WriteLine("@far: " + (daRatioConstant * x + daRatioLinear * x * far + daRatioQuadratic * x * far * far));

         // solve spotlight attenuation constant.
         // edge% = atten_spotlight = dot(spotlightDirectionUnit, objectDirectionUnit) ^ power
         // edge% = cos(theta) ^ power
         // Math.Log(edge%, cos(theta)) = power
         var power = Math.Log(edgeSpotlightAttenuationPercent, Math.Cos(theta));
         //Console.WriteLine("Power: " + power);
         //Console.WriteLine("@far: " + Math.Pow(Math.Cos(theta), power));

         AddSpotlight(new SpotlightInfo {
            Origin = position,
            Direction = direction,
            
            Color = color,
            DistanceAttenuationConstant = x * daRatioConstant,
            DistanceAttenuationLinear = x * daRatioLinear,
            DistanceAttenuationQuadratic = x * daRatioQuadratic,
            SpotlightAttenuationPower = (float)power,

            ProjViewCM = proj * view,
         });
      }

      public void AddSpotlight(SpotlightInfo info) {
         spotlightInfos.Add(info);
      }

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      private struct SceneConstantBufferData {
         public Vector4 cameraEye;
         public Matrix projView;
         public int pbrEnabled;
         public int shadowTestEnabled;
         public int numSpotlights;
         public int padding;

         public const int Size = 16 + 64 + 4 * 3 + 4;
      }

      private void UpdateSceneConstantBuffer(IRenderContext renderContext, Vector4 cameraEye, Matrix projView, bool pbrEnabled, bool shadowTestEnabled, int numSpotlights) {
         renderContext.Update(_sceneBuffer, new SceneConstantBufferData {
            cameraEye = cameraEye,
            projView = projView,
            pbrEnabled = pbrEnabled ? 1 : 0,
            shadowTestEnabled = shadowTestEnabled ? 1 : 0,
            numSpotlights = numSpotlights,
            padding = 0,
         });
      }

      public enum DiffuseTextureSamplingMode {
         FlatUV = 0,
         FlatGrayscale = 10,
         FlatGrayscaleDerivative = 11,
         CubeObjectRelative = 20,
         CubeNormal = 21,
      }

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      private struct BatchConstantBufferData {
         public Matrix batchTransform;
         public int diffuseSamplingMode;
         public int padding0, padding1, padding2;

         public const int Size = 64 + 4 + 12;
      }

      private void UpdateBatchConstantBuffer(IRenderContext renderContext, Matrix batchTransform, DiffuseTextureSamplingMode diffuseSamplingMode) {
         renderContext.Update(_batchBuffer, new BatchConstantBufferData {
            batchTransform = batchTransform,
            diffuseSamplingMode = (int)diffuseSamplingMode,
            padding0 = 0,
            padding1 = 0,
            padding2 = 0,
         });
      }

      private IBuffer<RenderJobDescription> PickInstancingBuffer(int sz) {
         for (var i = 0; i <= _instancingBuffers.Count; i++) {
            if (sz <= (1 << i)) {
               return _instancingBuffers[i];
            }
         }
         throw new ArgumentOutOfRangeException();
      }

      private unsafe IBuffer<RenderJobDescription> PickAndUpdateInstancingBuffer(IRenderContext renderContext, StructArrayList<RenderJobDescription> jobDescriptions) {
         var instancingBuffer = PickInstancingBuffer(jobDescriptions.Count);
         fixed (RenderJobDescription* p = jobDescriptions.store) {
            renderContext.Update(instancingBuffer, (IntPtr)p, jobDescriptions.size);
         }
         return instancingBuffer;
      }

      private IBuffer<RenderJobDescription> PickAndUpdateInstancingBuffer(IRenderContext renderContext, RenderJobDescription jobDescription) {
         var instancingBuffer = _instancingBuffers[0];
         renderContext.Update(instancingBuffer, jobDescription);
         return instancingBuffer;
      }

      public unsafe void RenderScene() {
         // Store backbuffer render targets for screen draw
         _graphicsDevice.ImmediateContext.GetRenderTargets(out var backBufferDepthStencilView, out var backBufferRenderTargetView);

         //var renderContext = _graphicsDevice.ImmediateContext;
         var renderContext = _graphicsDevice.CreateDeferredRenderContext();
         renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         renderContext.ClearRenderTarget(Color.Gray);
         renderContext.ClearDepthBuffer(1.0f);

         renderContext.SetDepthConfiguration(DepthConfiguration.Enabled);

         // Draw spotlights
         var spotlightDescriptions = stackalloc SpotlightDescription[spotlightInfos.Count];
         ComputeSpotlightDescriptions(spotlightDescriptions);

         for (var i = 0; i < _lightDepthStencilViews.Length; i++) {
            var ldsv = _lightDepthStencilViews[i];
            renderContext.SetRenderTargets(ldsv, null);
            renderContext.ClearDepthBuffer(1.0f);
         }

         renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
         for (var spotlightIndex = 0; spotlightIndex < spotlightInfos.Count; spotlightIndex++) {
            var spotlightDescription = &spotlightDescriptions[spotlightIndex];
            renderContext.SetRenderTargets(_lightDepthStencilViews[(int)spotlightDescription->AtlasLocation.Position.Z], null);

            renderContext.SetViewportRect(new RectangleF(
               spotlightDescription->AtlasLocation.Position.X,
               spotlightDescription->AtlasLocation.Position.Y,
               2048 * spotlightDescription->AtlasLocation.Size.X,
               2048 * spotlightDescription->AtlasLocation.Size.Y
            ));

            UpdateSceneConstantBuffer(renderContext, new Vector4(spotlightDescription->SpotlightInfo.Origin, 1.0f), spotlightDescription->SpotlightInfo.ProjViewCM, false, false, 0);
            for (var pass = 0; pass < Techniques.ForwardDepthOnly.Passes; pass++) {
               Techniques.ForwardDepthOnly.BeginPass(renderContext, pass);
               renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
               renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);

               foreach (var batch in renderJobBatches) {
                  UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, 0);
                  var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, batch.Jobs);

                  //renderContext.SetVertexBuffer(1, instancingBuffer);
                  batch.Mesh.Draw(renderContext, batch.Jobs.Count);
                  //renderContext.SetVertexBuffer(1, null);
               }
            }
         }

         // Prepare for scene render
         renderContext.Update(_shadowMapEntriesBuffer, (IntPtr)spotlightDescriptions, spotlightInfos.Count);
         renderContext.SetShaderResource(11, _shadowMapEntriesBufferSrv, RenderStage.Pixel);

         // Draw Scene
         renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         renderContext.SetViewportRect(new RectangleF(0, 0, 1280, 720));
         renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);

         UpdateSceneConstantBuffer(renderContext, new Vector4(_cameraEye, 1.0f), _projView, true, true, spotlightInfos.Count);
         for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
            Techniques.Forward.BeginPass(renderContext, pass);

            renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
            renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);
            renderContext.SetShaderResource(1, _whiteCubeMapShaderResourceView, RenderStage.Pixel);
            renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
            renderContext.SetShaderResource(11, _shadowMapEntriesBufferSrv, RenderStage.Pixel);
            foreach (var batch in renderJobBatches) {
               UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal);
               var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, batch.Jobs);

               renderContext.SetVertexBuffer(1, instancingBuffer);
               batch.Mesh.Draw(renderContext, batch.Jobs.Count);
               renderContext.SetVertexBuffer(1, null);
            }
         }

         // draw depth texture
         for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
            Techniques.Forward.BeginPass(renderContext, pass);
            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatGrayscaleDerivative);
            for (var i = 0; i < 2; i++) {
               var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, 1280.0f, 720.0f, 0.0f, 0.1f, 100.0f); // top-left origin
               UpdateSceneConstantBuffer(renderContext, Vector4.Zero, orthoProj, false, false, 0);

               var quadWorld = MatrixCM.Scaling(256, 256, 0) * MatrixCM.Translation(0.5f + i, 0.5f, 0.0f);
               var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, new RenderJobDescription {
                  WorldTransform = quadWorld
               });

               renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
               renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);
               renderContext.SetShaderResource(0, _lightShaderResourceViews[i], RenderStage.Pixel);

               //renderContext.SetVertexBuffer(1, instancingBuffer);
               _graphicsDevice.MeshPresets.UnitPlaneXY.Draw(renderContext, 1);
               //renderContext.SetVertexBuffer(1, null);
            }
         }

         var cl = ((Direct3DGraphicsDevice.DeferredRenderContext)renderContext).HackFinishCommandList();
         var device = ((Direct3DGraphicsDevice)_graphicsDevice).InternalD3DDevice;
         device.ImmediateContext.ExecuteCommandList(cl, false);
         _graphicsDevice.ImmediateContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         _graphicsDevice.ImmediateContext.Present();
      }

      private unsafe void ComputeSpotlightDescriptions(SpotlightDescription* res) {
         for (var i = 0; i < spotlightInfos.Count; i++) {
            res[i].AtlasLocation = new AtlasLocation {
               Position = new Vector3(0, 0, i),
               Size = new Vector2(1, 1)
            };
            res[i].SpotlightInfo = spotlightInfos[i];
         }
      }

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      struct AtlasLocation {
         public Vector3 Position;
         public Vector2 Size;

         public const int SIZE = 4 * (3 + 2);
      };

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      struct SpotlightDescription {
         public SpotlightInfo SpotlightInfo;
         public AtlasLocation AtlasLocation;

         public const int Size = AtlasLocation.SIZE + SpotlightInfo.Size;
      };

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      public struct SpotlightInfo {
         public Vector3 Origin;
         public Vector3 Direction;

         public Color4 Color;
         public float DistanceAttenuationConstant;
         public float DistanceAttenuationLinear;
         public float DistanceAttenuationQuadratic;
         public float SpotlightAttenuationPower;

         public Matrix ProjViewCM;

         public const int Size = (3 * 4) * 2 + (4 * 4) * 1 + (4) * 4 + (4 * 4 * 4) * 1;
      }
   }

   public class RenderJobBatch {
      public IMesh Mesh;
      public StructArrayList<RenderJobDescription> Jobs = new StructArrayList<RenderJobDescription>();
      public Matrix BatchTransform = Matrix.Identity;
   }

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct RenderJobDescription {
      public Matrix WorldTransform;
      //public MaterialDescription Material;

      public const int Size = (4 * 4 * 4) * 1 + MaterialDescription.Size;
   }

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct MaterialDescription {
      public const int Size = 0;
   }
}