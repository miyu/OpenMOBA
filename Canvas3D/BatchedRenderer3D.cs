using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Canvas3D.LowLevel;
using Canvas3D.LowLevel.Direct3D;
using Canvas3D.LowLevel.Helpers;
using SharpDX;
using SharpDX.Direct3D11;
using Color = SharpDX.Color;
using Rectangle = SharpDX.Rectangle;
using RectangleF = SharpDX.RectangleF;

namespace Canvas3D {
   public interface IAssetManager {
      IMesh GetPresetMesh(MeshPreset preset);
   }

   public interface IRenderer3D {
      void ClearScene();
      void SetCamera(Vector3 cameraEye, Matrix projView);
      int AddMaterial(MaterialDescription desc);
      void AddRenderable(MeshPreset preset, Matrix worldCm, int materialIndex);
      void AddRenderable(MeshPreset preset, Matrix worldCm, MaterialDescription materialDescription);
      void AddRenderable(IMesh mesh, Matrix worldCm, int materialIndex);
      void AddRenderable(IMesh mesh, Matrix worldCm, MaterialDescription materialDescription);
      void AddRenderable(IMesh mesh, RenderJobDescription info);
      void AddRenderJobBatch(RenderJobBatch batch);
      void AddSpotlight(Vector3 position, Vector3 lookat, float theta, Color color, float far, float daRatioConstant, float daRatioLinear, float daRatioQuadratic, float edgeSpotlightAttenuationPercent = 1.0f / 256.0f);
      void RenderScene();
   }

   public class BatchedRenderer3D : IAssetManager, IRenderer3D {
      public enum DiffuseTextureSamplingMode {
         FlatUV = 0,
         FlatUVGrayscale = 10,
         FlatUVGrayscaleDerivative = 11,
         FlatUVNoAlpha = 12,
         FlatUVUnpackMaterialW = 13,
         CubeObjectRelative = 20,
         CubeNormal = 21
      }

      private const int kShadowMapWidthHeight = 256;
      private const int kMaterialLimit = 16384;
      private const int kBatchNoMaterialIndexOverride = -1;

      private readonly Device _d3d;
      private readonly IGraphicsDevice _graphicsDevice;
      private readonly IBuffer<SceneConstantBufferData> _sceneBuffer;
      private readonly IBuffer<BatchConstantBufferData> _batchBuffer;
      private readonly List<IBuffer<RenderJobDescription>> _instancingBuffers;
      private readonly IRenderTargetView[] _gBufferRtvs;
      private readonly IShaderResourceView _gBufferSrv;
      private readonly IShaderResourceView[] _gBufferSrvs;
      private readonly IDepthStencilView _gBufferDsv;
      private readonly IShaderResourceView _gBufferDepthSrv;

      private readonly IBuffer<SpotlightDescription> _spotlightDescriptionsBuffer;
      private readonly IShaderResourceView _spotlightDescriptionsBufferSrv;
      private readonly IBuffer<MaterialDescription> _materialDescriptionsBuffer;
      private readonly IShaderResourceView _materialDescriptionsBufferSrv;

      private readonly IDepthStencilView[] _lightDepthStencilViews;
      private readonly IDisposable _lightDepthTexture;
      private readonly IShaderResourceView _lightShaderResourceView;
      private readonly IShaderResourceView[] _lightShaderResourceViews;
      private readonly ITexture2D _whiteCubeMap;
      private readonly IShaderResourceView _whiteCubeMapShaderResourceView;
      private readonly ITexture2D _whiteTexture;
      private readonly IShaderResourceView _whiteTextureShaderResourceView;
      private readonly ITexture2D _limeCubeMap;
      private readonly IShaderResourceView _limeCubeMapShaderResourceView;
      private readonly ITexture2D _limeTexture;
      private readonly IShaderResourceView _limeTextureShaderResourceView;
      private readonly ITexture2D _flatColoredCubeMap;
      private readonly IShaderResourceView _flatColoredCubeMapShaderResourceView;

      private readonly StructArrayList<MaterialDescription> materials = new StructArrayList<MaterialDescription>();
      private readonly Dictionary<IMesh, RenderJobBatch> defaultRenderJobBatchesByMesh = new Dictionary<IMesh, RenderJobBatch>();
      private readonly List<RenderJobBatch> renderJobBatches = new List<RenderJobBatch>();
      private readonly List<SpotlightInfo> spotlightInfos = new List<SpotlightInfo>();

      private Vector3 _cameraEye;
      private Matrix _projView, _projViewInv;

      public BatchedRenderer3D(IGraphicsDevice graphicsDevice) {
         _d3d = (graphicsDevice as Direct3DGraphicsDevice).InternalD3DDevice;
         _graphicsDevice = graphicsDevice;
         _sceneBuffer = _graphicsDevice.CreateConstantBuffer<SceneConstantBufferData>(1);
         _batchBuffer = _graphicsDevice.CreateConstantBuffer<BatchConstantBufferData>(1);
         _instancingBuffers = new List<IBuffer<RenderJobDescription>>();

         const int kMaxPreallocatedInstanceBufferPower = 18;
         for (var i = 0; i <= kMaxPreallocatedInstanceBufferPower; i++) {
            _instancingBuffers.Add(_graphicsDevice.CreateVertexBuffer<RenderJobDescription>(1 << i));
         }
         
         (_gBufferRtvs, _gBufferSrv, _gBufferSrvs) = _graphicsDevice.CreateScreenSizeRenderTarget(2);
         (_gBufferDsv, _gBufferDepthSrv) = _graphicsDevice.CreateScreenSizeDepthTarget();
         (_spotlightDescriptionsBuffer, _spotlightDescriptionsBufferSrv) = _graphicsDevice.CreateStructuredBufferAndView<SpotlightDescription>(256);
         (_materialDescriptionsBuffer, _materialDescriptionsBufferSrv) = _graphicsDevice.CreateStructuredBufferAndView<MaterialDescription>(kMaterialLimit);
         (_lightDepthTexture, _lightDepthStencilViews, _lightShaderResourceView, _lightShaderResourceViews) = _graphicsDevice.CreateDepthTextureAndViews(10, new Size(kShadowMapWidthHeight, kShadowMapWidthHeight));

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

      public IMesh GetPresetMesh(MeshPreset preset) {
         if (preset == MeshPreset.UnitCube) return _graphicsDevice.MeshPresets.UnitCube;
         else if (preset == MeshPreset.UnitPlaneXY) return _graphicsDevice.MeshPresets.UnitPlaneXY;
         else if (preset == MeshPreset.UnitSphere) return _graphicsDevice.MeshPresets.UnitSphere;
         else throw new NotSupportedException();
      }

      public void ClearScene() {
         materials.Clear();
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
         _projViewInv = Matrix.Invert(projView);
      }

      public int AddMaterial(MaterialDescription desc) {
         materials.Add(desc);
         return materials.Count - 1;
      }

      public void AddRenderable(MeshPreset preset, Matrix worldCm, int materialIndex) {
         AddRenderable(GetPresetMesh(preset), worldCm, materialIndex);
      }

      public void AddRenderable(MeshPreset preset, Matrix worldCm, MaterialDescription material) {
         AddRenderable(GetPresetMesh(preset), worldCm, AddMaterial(material));
      }

      public void AddRenderable(IMesh mesh, Matrix worldCm, int materialIndex) {
         AddRenderable(mesh, new RenderJobDescription {
            WorldTransform = worldCm,
            MaterialIndex = materialIndex,
         });
      }

      public void AddRenderable(IMesh mesh, Matrix worldCm, MaterialDescription material) {
         AddRenderable(mesh, worldCm, AddMaterial(material));
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
         var projCm = MatrixCM.PerspectiveFovRH(theta, 1.0f, 0.1f, far);

         var up = Vector3.Up; // todo: handle degenerate
         var viewCm = MatrixCM.LookAtRH(position, lookat, up);

         // solve distance attenuation constants: 1/256 = atten = 1 / (x * darc + far * x * darl + far * far * x * darq)
         // 256 = x * darc + far * x * darl + far * far * x * darq
         // 256 = x * (darc + far * darl + far * far * darq)
         var x = 256 / (daRatioConstant + far * daRatioLinear + far * far * daRatioQuadratic);
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

         var projViewCm = projCm * viewCm;

         AddSpotlight(new SpotlightInfo {
            Origin = position,
            Direction = direction,

            Color = color,
            DistanceAttenuationConstant = x * daRatioConstant,
            DistanceAttenuationLinear = x * daRatioLinear,
            DistanceAttenuationQuadratic = x * daRatioQuadratic,
            SpotlightAttenuationPower = (float)power,

            ProjViewCM = projViewCm,
         });
      }

      private void AddSpotlight(SpotlightInfo info) {
         spotlightInfos.Add(info);
      }

      public unsafe void RenderScene() {
         // Store backbuffer render targets for screen draw
         _graphicsDevice.ImmediateContext.GetBackBufferViews(out var backBufferDepthStencilView, out var backBufferRenderTargetView);

         //var renderContext = _graphicsDevice.ImmediateContext;
         var renderContext = _graphicsDevice.CreateDeferredRenderContext();
         renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         renderContext.ClearRenderTarget(Color.Gray);
         renderContext.ClearDepthBuffer(1.0f);

         renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
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
               kShadowMapWidthHeight * spotlightDescription->AtlasLocation.Size.X,
               kShadowMapWidthHeight * spotlightDescription->AtlasLocation.Size.Y
            ));

            UpdateSceneConstantBuffer(renderContext, new Vector4(spotlightDescription->SpotlightInfo.Origin, 1.0f), spotlightDescription->SpotlightInfo.ProjViewCM, Matrix.Zero, false, false, 0);
            for (var pass = 0; pass < Techniques.ForwardDepthOnly.Passes; pass++) {
               Techniques.ForwardDepthOnly.BeginPass(renderContext, pass);
               renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
               renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);

               foreach (var batch in renderJobBatches) {
                  UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, 0, batch.MaterialIndexOverride);
                  var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, batch.Jobs);
                  
                  renderContext.SetVertexBuffer(1, instancingBuffer);
                  batch.Mesh.Draw(renderContext, batch.Jobs.Count);
                  renderContext.SetVertexBuffer(1, null);
               }
            }
         }

         // Prepare for scene render
         renderContext.Update(_materialDescriptionsBuffer, materials.store, 0, materials.Count);
         renderContext.Update(_spotlightDescriptionsBuffer, (IntPtr)spotlightDescriptions, spotlightInfos.Count);

         // Draw Scene
         bool forward = false;
         if (forward) {
            renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
            renderContext.SetViewportRect(new RectangleF(0, 0, backBufferRenderTargetView.Resolution.Width, backBufferRenderTargetView.Resolution.Height));
            renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);

            UpdateSceneConstantBuffer(renderContext, new Vector4(_cameraEye, 1.0f), _projView, _projViewInv, true, true, spotlightInfos.Count);
            for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
               Techniques.Forward.BeginPass(renderContext, pass);

               renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
               renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);
               renderContext.SetShaderResource(1, _whiteCubeMapShaderResourceView, RenderStage.Pixel);
               renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
               renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
               renderContext.SetShaderResource(12, _materialDescriptionsBufferSrv, RenderStage.Pixel);
               foreach (var batch in renderJobBatches) {
                  if (batch.Jobs.Count > 100)
                     renderContext.SetShaderResource(1, _flatColoredCubeMapShaderResourceView, RenderStage.Pixel);
                  else
                     renderContext.SetShaderResource(1, _whiteCubeMapShaderResourceView, RenderStage.Pixel);

                  UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal, batch.MaterialIndexOverride);
                  var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, batch.Jobs);

                  renderContext.SetVertexBuffer(1, instancingBuffer);
                  batch.Mesh.Draw(renderContext, batch.Jobs.Count);
                  renderContext.SetVertexBuffer(1, null);
               }
            }
         } else {
            var baseAndMaterialRtv = _gBufferRtvs[0];
            var normalRtv = _gBufferRtvs[1];
            renderContext.SetRenderTargets(_gBufferDsv, baseAndMaterialRtv, normalRtv);
            renderContext.SetViewportRect(new RectangleF(0, 0, baseAndMaterialRtv.Resolution.Width, baseAndMaterialRtv.Resolution.Height));
            renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
            renderContext.ClearRenderTargets(Color.Transparent, Color.Transparent);
            renderContext.ClearDepthBuffer(1.0f);

            UpdateSceneConstantBuffer(renderContext, new Vector4(_cameraEye, 1.0f), _projView, _projViewInv, true, true, spotlightInfos.Count);
            for (var pass = 0; pass < Techniques.DeferredToGBuffer.Passes; pass++) {
               Techniques.DeferredToGBuffer.BeginPass(renderContext, pass);

               renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
               renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);
               renderContext.SetShaderResource(1, _whiteCubeMapShaderResourceView, RenderStage.Pixel);
               renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
               renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
               renderContext.SetShaderResource(12, _materialDescriptionsBufferSrv, RenderStage.Pixel);
               foreach (var batch in renderJobBatches) {
                  if (batch.Jobs.Count > 100)
                     renderContext.SetShaderResource(1, _flatColoredCubeMapShaderResourceView, RenderStage.Pixel);
                  else
                     renderContext.SetShaderResource(1, _whiteCubeMapShaderResourceView, RenderStage.Pixel);

                  UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal, batch.MaterialIndexOverride);
                  var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, batch.Jobs);

                  renderContext.SetVertexBuffer(1, instancingBuffer);
                  batch.Mesh.Draw(renderContext, batch.Jobs.Count);
                  renderContext.SetVertexBuffer(1, null);
               }
            }

            // Restore render targets, merge gbuffers
            renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
            renderContext.SetViewportRect(new RectangleF(0, 0, backBufferRenderTargetView.Resolution.Width, backBufferRenderTargetView.Resolution.Height));
            renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);

            for (var pass = 0; pass < Techniques.DeferredFromGBuffer.Passes; pass++) {
               Techniques.DeferredFromGBuffer.BeginPass(renderContext, pass);

               renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
               renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
               renderContext.SetShaderResource(12, _materialDescriptionsBufferSrv, RenderStage.Pixel);

               var (proj, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, 0, 0, backBufferDepthStencilView.Resolution.Width, backBufferDepthStencilView.Resolution.Height, -2.0f);
               UpdateSceneConstantBuffer(renderContext, new Vector4(_cameraEye, 1.0f), _projView, _projViewInv, true, true, spotlightInfos.Count);
               UpdateBatchConstantBuffer(renderContext, proj, 0, kBatchNoMaterialIndexOverride);
               DrawScreenQuad(renderContext, world, _gBufferSrvs[0], _gBufferSrvs[1], _gBufferDepthSrv);
            }
         }

         // draw depth texture
         for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
            const int pipScale = 128; // height
            Techniques.Forward.BeginPass(renderContext, pass);

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVGrayscaleDerivative, kBatchNoMaterialIndexOverride);
            Matrix projView, world;
            for (var i = 0; i < 2; i++) {
               (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, pipScale * i, 0, pipScale, pipScale);
               UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, Matrix.Identity, false, false, 0);
               DrawScreenQuad(renderContext, world, _lightShaderResourceViews[i]);
            }

            var pipGBufferWidth = pipScale * backBufferRenderTargetView.Resolution.Width / backBufferRenderTargetView.Resolution.Height;

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVNoAlpha, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, 0, pipScale, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, _projViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferSrvs[0]);

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVNoAlpha, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, pipGBufferWidth, pipScale, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, _projViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferSrvs[1]);

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVUnpackMaterialW, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, 0, pipScale * 2, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, _projViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferSrvs[1]);

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVGrayscaleDerivative, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, pipGBufferWidth, pipScale * 2, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, _projViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferDepthSrv);
         }

         using (var commandList = renderContext.FinishCommandListAndFree()) {
            _graphicsDevice.ImmediateContext.ExecuteCommandList(commandList);
         }
         _graphicsDevice.ImmediateContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);

         _graphicsDevice.ImmediateContext.Present();
      }

      private (Matrix, Matrix) ComputeSceneQuadProjWorld(Size renderTargetSize, float x, float y, float w, float h, float z = -1.0f) {
         var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, renderTargetSize.Width, renderTargetSize.Height, 0.0f, 0.1f, 100.0f); // top-left origin
         var quadWorld = MatrixCM.Translation(x, y, 0) * MatrixCM.Scaling(w, h, 1) * MatrixCM.Translation(0.5f, 0.5f, z);
         return (orthoProj, quadWorld);
      }

      private void DrawScreenQuad(IRenderContext renderContext, Matrix world, IShaderResourceView textureSrv0, IShaderResourceView textureSrv1 = null, IShaderResourceView textureSrv2 = null) {
         var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, new RenderJobDescription { WorldTransform = world });

         renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
         renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);
         renderContext.SetShaderResource(0, textureSrv0, RenderStage.Pixel);
         renderContext.SetShaderResource(1, textureSrv1, RenderStage.Pixel);
         renderContext.SetShaderResource(2, textureSrv2, RenderStage.Pixel);

         renderContext.SetVertexBuffer(1, instancingBuffer);
         _graphicsDevice.MeshPresets.UnitPlaneXY.Draw(renderContext, 1);
         renderContext.SetVertexBuffer(1, null);
      }

      private void UpdateSceneConstantBuffer(IRenderContext renderContext, Vector4 cameraEye, Matrix projView, Matrix projViewInv, bool pbrEnabled, bool shadowTestEnabled, int numSpotlights) {
         renderContext.Update(_sceneBuffer, new SceneConstantBufferData {
            cameraEye = cameraEye,
            projView = projView,
            projViewInv = projViewInv,
            pbrEnabled = pbrEnabled ? 1 : 0,
            shadowTestEnabled = shadowTestEnabled ? 1 : 0,
            numSpotlights = numSpotlights,
            padding = 0
         });
      }

      private void UpdateBatchConstantBuffer(IRenderContext renderContext, Matrix batchTransform, DiffuseTextureSamplingMode diffuseSamplingMode, int batchMaterialIndexOverride) {
         renderContext.Update(_batchBuffer, new BatchConstantBufferData {
            batchTransform = batchTransform,
            diffuseSamplingMode = (int)diffuseSamplingMode,
            batchMaterialIndexOverride = batchMaterialIndexOverride,
            padding0 = 0,
            padding1 = 0,
         });
      }

      private IBuffer<RenderJobDescription> PickInstancingBuffer(int sz) {
         for (var i = 0; i <= _instancingBuffers.Count; i++) {
            var capacity = 1 << i;
            if (sz <= capacity) {
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
      private struct SceneConstantBufferData {
         public Vector4 cameraEye;
         public Matrix projView;
         public Matrix projViewInv;
         public int pbrEnabled;
         public int shadowTestEnabled;
         public int numSpotlights;
         public int padding;

         public const int Size = 16 + 64 * 2 + 4 * 3 + 4;
      }

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      private struct BatchConstantBufferData {
         public Matrix batchTransform;
         public int diffuseSamplingMode;
         public int batchMaterialIndexOverride; // -1 default
         public int padding0, padding1;

         public const int Size = 64 + 4 + 12;
      }

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      private struct AtlasLocation {
         public Vector3 Position;
         public Vector2 Size;

         public const int SIZE = 4 * (3 + 2);
      }

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      private struct SpotlightDescription {
         public SpotlightInfo SpotlightInfo;
         public AtlasLocation AtlasLocation;

         public const int Size = AtlasLocation.SIZE + SpotlightInfo.Size;
      }

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

         public const int Size = 3 * 4 * 2 + 4 * 4 * 1 + 4 * 4 + 64 * 1;
      }
   }

   public class RenderJobBatch {
      public Matrix BatchTransform = Matrix.Identity;
      public StructArrayList<RenderJobDescription> Jobs = new StructArrayList<RenderJobDescription>();
      public IMesh Mesh;
      public int MaterialIndexOverride = -1;
   }

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct RenderJobDescription {
      public Matrix WorldTransform;
      public int MaterialIndex;

      public const int Size = 4 * 4 * 4 * 1 + 4;
   }

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct MaterialDescription {
      public float Metallic;
      public float Roughness;

      public const int Size = 4 * 2;
   }
}
