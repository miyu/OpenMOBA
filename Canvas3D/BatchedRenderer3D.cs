using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using Canvas3D.LowLevel;
using Canvas3D.LowLevel.Direct3D;
using Canvas3D.LowLevel.Helpers;
using SharpDX;
using SharpDX.Direct3D11;
using Color = SharpDX.Color;
using IDeviceContext = Canvas3D.LowLevel.IDeviceContext;
using RectangleF = SharpDX.RectangleF;

namespace Canvas3D {
   public interface IScene {
      void Clear();
      void SetCamera(Vector3 cameraEye, Matrix projView);
      int AddMaterialResources(MaterialResourcesDescription desc);
      void AddRenderable(IMesh mesh, Matrix worldCm, MaterialDescription material);
      void AddRenderable(IMesh mesh, Matrix worldCm, MaterialProperties materialProperties, int materialResourcesIndex);
      void AddRenderable(IMesh mesh, RenderJobDescription info);
      void AddRenderJobBatch(RenderJobBatch batch);
      void AddSpotlight(Vector3 position, Vector3 lookat, Vector3 up, float theta, Color color, float near, float far, float daRatioConstant, float daRatioLinear, float daRatioQuadratic, float edgeSpotlightAttenuationPercent = 1.0f / 256.0f);
      ISceneSnapshot ExportSnapshot();
      void ExportSnapshot(ISceneSnapshot snapshot);
   }

   public interface IRenderContext {
      void RenderScene(ISceneSnapshot snapshot);
   }

   public enum DiffuseTextureSamplingMode {
      FlatUV = 0,
      FlatUVGrayscale = 10,
      FlatUVGrayscaleDerivative = 11,
      FlatUVNoAlpha = 12,
      FlatUVUnpackMaterialW = 13,
      CubeObjectRelative = 20,
      CubeNormal = 21
   }

   public class Scene : IScene {
      private AddOnlyOrderedHashSet<IShaderResourceView> textures = new AddOnlyOrderedHashSet<IShaderResourceView>();
      private ExposedArrayList<InternalMaterialResourcesDescription> materials = new ExposedArrayList<InternalMaterialResourcesDescription>();
      private Dictionary<IMesh, RenderJobBatch> defaultRenderJobBatchesByMesh = new Dictionary<IMesh, RenderJobBatch>();
      private List<RenderJobBatch> renderJobBatches = new List<RenderJobBatch>();
      private List<SpotlightInfo> spotlightInfos = new List<SpotlightInfo>();

      private Vector3 _cameraEye;
      private Matrix _projView, _projViewInv;

      public void Clear() {
         textures.Clear();
         materials.Clear();
         renderJobBatches.Clear();
         // Note: It's assumed safe to hold references to texture SRVs long-term; 
         // the graphics device will box an SRV, and disposing invalidates box contents.
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

      private int GetOrAddTextureIndex(IShaderResourceView textureSrv) {
         if (textureSrv == null) {
            return -1;
         }
         textures.TryAdd(textureSrv, out int baseTextureIndex);
         return baseTextureIndex;
      }

      public int AddMaterialResources(MaterialResourcesDescription desc) {
         materials.Add(desc.ToInternal(GetOrAddTextureIndex(desc.BaseTexture)));
         return materials.Count - 1;
      }

      public void AddRenderable(IMesh mesh, Matrix worldCm, MaterialDescription material) {
         AddRenderable(mesh, worldCm, material.Properties, AddMaterialResources(material.Resources));
      }

      public void AddRenderable(IMesh mesh, Matrix worldCm, MaterialProperties materialProperties, int materialResourcesIndex) {
         AddRenderable(mesh, new RenderJobDescription {
            WorldTransform = worldCm,
            MaterialProperties = materialProperties,
            MaterialResourcesIndex = materialResourcesIndex,
         });
      }

      public void AddRenderable(IMesh mesh, RenderJobDescription info) {
         if (!defaultRenderJobBatchesByMesh.TryGetValue(mesh, out var batch)) {
            batch = defaultRenderJobBatchesByMesh[mesh] = RenderJobBatch.Create(mesh);
            renderJobBatches.Add(batch);
         }
         batch.Jobs.Add(info);
      }

      public void AddRenderJobBatch(RenderJobBatch batch) {
         renderJobBatches.Add(batch);
      }

      public void AddSpotlight(Vector3 position, Vector3 lookat, Vector3 up, float theta, Color color, float near, float far, float daRatioConstant, float daRatioLinear, float daRatioQuadratic, float edgeSpotlightAttenuationPercent = 1.0f / 256.0f) {
         var projCm = MatrixCM.PerspectiveFovRH(theta, 1.0f, near, far);

         var viewCm = MatrixCM.ViewLookAtRH(position, lookat, up);

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

      public ISceneSnapshot ExportSnapshot() {
         var snapshot = new SceneSnapshot();
         ExportSnapshot(snapshot);
         return snapshot;
      }

      public void ExportSnapshot(ISceneSnapshot s) {
         var snapshot = (SceneSnapshot)s;
         snapshot.Textures.Clear();
         foreach (var tex in textures) snapshot.Textures.TryAdd(tex);

         snapshot.Materials.Clear();
         foreach (var mat in materials) snapshot.Materials.Add(mat);

         snapshot.RenderJobBatches.Clear();
         foreach (var batch in renderJobBatches) snapshot.RenderJobBatches.Add(batch);

         snapshot.SpotlightInfos.Clear();
         foreach (var spotlightInfo in spotlightInfos) snapshot.SpotlightInfos.Add(spotlightInfo);

         snapshot.CameraEye = _cameraEye;
         snapshot.ProjView = _projView;
         snapshot.ProjViewInv = _projViewInv;

         foreach (var batch in snapshot.RenderJobBatches)
            if (batch.MaterialResourcesIndexOverride != -1)
               foreach (var job in batch.Jobs)
                  if (job.MaterialResourcesIndex >= snapshot.Materials.Count)
                     throw new InvalidOperationException();
      }
   }

   internal class RenderContext : IRenderContext {
      private const int kShadowMapWidthHeight = 256;
      private const int kBatchNoMaterialIndexOverride = -1;
      private const int kBaseTextureSlotId = 48;
      private const int kTextureBindLimit = 80; // Slots [48, 127)
      private const int kMaterialBufferCount = 256; // Slots [48, 127)

      private readonly Device _d3d;
      private readonly IGraphicsDevice _graphicsDevice;
      private readonly IBuffer<SceneConstantBufferData> _sceneBuffer;
      private readonly IBuffer<BatchConstantBufferData> _batchBuffer;
      private readonly IBuffer<TextureDescriptorConstantBufferData> _textureDescriptorBuffer;
      private readonly List<IBuffer<RenderJobDescription>> _instancingBuffers;
      private readonly IRenderTargetView[] _gBufferRtvs;
      private readonly IShaderResourceView _gBufferSrv;
      private readonly IShaderResourceView[] _gBufferSrvs;
      private readonly IDepthStencilView _gBufferDsv;
      private readonly IShaderResourceView _gBufferDepthSrv;

      private readonly IBuffer<SpotlightDescription> _spotlightDescriptionsBuffer;
      private readonly IShaderResourceView _spotlightDescriptionsBufferSrv;
      private readonly IBuffer<InternalMaterialResourcesDescription> _materialResourcesBuffer;
      private readonly IShaderResourceView _materialResourcesBufferSrv;

      private readonly IDepthStencilView[] _lightDepthStencilViews;
      private readonly ITexture2D _lightDepthTexture;
      private readonly IShaderResourceView _lightShaderResourceView;
      private readonly IShaderResourceView[] _lightShaderResourceViews;

      internal RenderContext(IGraphicsDevice graphicsDevice) {
         _d3d = (graphicsDevice as Direct3DGraphicsDevice).InternalD3DDevice;
         _graphicsDevice = graphicsDevice;
         _sceneBuffer = _graphicsDevice.CreateConstantBuffer<SceneConstantBufferData>(1);
         _batchBuffer = _graphicsDevice.CreateConstantBuffer<BatchConstantBufferData>(1);
         _textureDescriptorBuffer = _graphicsDevice.CreateConstantBuffer<TextureDescriptorConstantBufferData>(kBaseTextureSlotId + kTextureBindLimit);
         _instancingBuffers = new List<IBuffer<RenderJobDescription>>();

         const int kMaxPreallocatedInstanceBufferPower = 18;
         for (var i = 0; i <= kMaxPreallocatedInstanceBufferPower; i++) {
            _instancingBuffers.Add(_graphicsDevice.CreateVertexBuffer<RenderJobDescription>(1 << i));
         }

         (_gBufferRtvs, _gBufferSrv, _gBufferSrvs) = _graphicsDevice.CreateScreenSizeRenderTarget(2);
         (_gBufferDsv, _gBufferDepthSrv) = _graphicsDevice.CreateScreenSizeDepthTarget();
         (_spotlightDescriptionsBuffer, _spotlightDescriptionsBufferSrv) = _graphicsDevice.CreateStructuredBufferAndView<SpotlightDescription>(256);
         (_materialResourcesBuffer, _materialResourcesBufferSrv) = _graphicsDevice.CreateStructuredBufferAndView<InternalMaterialResourcesDescription>(kMaterialBufferCount);
         (_lightDepthTexture, _lightDepthStencilViews, _lightShaderResourceView, _lightShaderResourceViews) = _graphicsDevice.CreateDepthTextureAndViews(10, new Size(kShadowMapWidthHeight, kShadowMapWidthHeight));

         Trace.Assert(Utilities.SizeOf<SpotlightInfo>() == SpotlightInfo.Size);
         Trace.Assert(Utilities.SizeOf<AtlasLocation>() == AtlasLocation.SIZE);
         Trace.Assert(Utilities.SizeOf<SpotlightDescription>() == SpotlightDescription.Size);
         Trace.Assert(Utilities.SizeOf<RenderJobDescription>() == RenderJobDescription.Size);
         Trace.Assert(Utilities.SizeOf<InternalMaterialResourcesDescription>() == InternalMaterialResourcesDescription.Size);
      }

      public ITechniqueCollection Techniques => _graphicsDevice.TechniqueCollection;

      public unsafe void RenderScene(ISceneSnapshot s) {
         var snapshot = (SceneSnapshot)s;

         // Store backbuffer render targets for screen draw
         _graphicsDevice.ImmediateContext.GetBackBufferViews(out var backBufferDepthStencilView, out var backBufferRenderTargetView);

         var renderContext = _graphicsDevice.ImmediateContext;
         //var renderContext = _graphicsDevice.CreateDeferredRenderContext();

         renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
         renderContext.SetDepthConfiguration(DepthConfiguration.Enabled);

         renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.PixelVertex);
         renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.PixelVertex);
         renderContext.SetConstantBuffer(2, _textureDescriptorBuffer, RenderStage.PixelVertex);

         // Draw spotlights
         var spotlightDescriptions = stackalloc SpotlightDescription[snapshot.SpotlightInfos.Count];
         ComputeSpotlightDescriptions(snapshot, spotlightDescriptions);

         //var lightDepthStencilViewCleared = stackalloc bool[_lightDepthStencilViews.Length];
         var lightDepthStencilViewCleared = new bool[_lightDepthStencilViews.Length];
         for (var i = 0; i < snapshot.SpotlightInfos.Count; i++) {
            var ldsvIndex = (int)spotlightDescriptions[i].AtlasLocation.Position.Z;
            if (lightDepthStencilViewCleared[ldsvIndex]) continue;
            lightDepthStencilViewCleared[ldsvIndex] = true;
            renderContext.SetRenderTargets(_lightDepthStencilViews[ldsvIndex], null);
            renderContext.ClearDepthBuffer(1.0f);
         }

         for (var spotlightIndex = 0; spotlightIndex < snapshot.SpotlightInfos.Count; spotlightIndex++) {
            var spotlightDescription = &spotlightDescriptions[spotlightIndex];
            renderContext.SetRenderTargets(_lightDepthStencilViews[(int)spotlightDescription->AtlasLocation.Position.Z], null);
            renderContext.SetViewportRect((Vector2)spotlightDescription->AtlasLocation.Position, kShadowMapWidthHeight * spotlightDescription->AtlasLocation.Size);

            UpdateSceneConstantBuffer(renderContext, new Vector4(spotlightDescription->SpotlightInfo.Origin, 1.0f), spotlightDescription->SpotlightInfo.ProjViewCM, Matrix.Zero, false, false, 0);
            for (var pass = 0; pass < Techniques.ForwardDepthOnly.Passes; pass++) {
               Techniques.ForwardDepthOnly.BeginPass(renderContext, pass);

               foreach (var batch in snapshot.RenderJobBatches) {
                  UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, 0, batch.MaterialResourcesIndexOverride);
                  var instancingBuffer = PickInstancingBuffer(batch.Jobs.Count);
                  renderContext.Update(instancingBuffer, batch.Jobs.store, 0, batch.Jobs.Count);

                  renderContext.SetVertexBuffer(1, instancingBuffer);
                  batch.Mesh.Draw(renderContext, batch.Jobs.Count);
                  renderContext.SetVertexBuffer(1, null);
               }
            }
         }

         // Prepare for scene render
         renderContext.Update(_spotlightDescriptionsBuffer, (IntPtr)spotlightDescriptions, snapshot.SpotlightInfos.Count);

         // Draw Scene
         bool forward = true;
         if (forward) {
            renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
            renderContext.SetViewportRect(new RectangleF(0, 0, backBufferRenderTargetView.Resolution.Width, backBufferRenderTargetView.Resolution.Height));
            renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);

            renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
            renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
            renderContext.SetShaderResource(12, _materialResourcesBufferSrv, RenderStage.Pixel);

            renderContext.ClearRenderTarget(Color.Gray);
            renderContext.ClearDepthBuffer(1.0f);

            UpdateSceneConstantBuffer(renderContext, new Vector4(snapshot.CameraEye, 1.0f), snapshot.ProjView, snapshot.ProjViewInv, true, true, snapshot.SpotlightInfos.Count);
            for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
               Techniques.Forward.BeginPass(renderContext, pass);

               foreach (var batch in snapshot.RenderJobBatches) {
                  if (batch.MaterialResourcesIndexOverride == -1) {
                     //int* boundTextureSlotByTextureIndex = stackalloc int[snapshot.Textures.Count];
                     int[] boundTextureSlotByTextureIndex = new int[snapshot.Textures.Count];
                     for (var i = 0; i < snapshot.Textures.Count; i++) {
                        boundTextureSlotByTextureIndex[i] = -1;
                     }

                     //int* mriBound = stackalloc int[snapshot.Materials.Count];
                     int[] mriBound = new int[snapshot.Materials.Count];
                     for (var i = 0; i < snapshot.Materials.Count; i++) {
                        mriBound[i] = -1;
                     }

                     int boundTextures = 0;
                     int boundMaterialResourceDescriptions = 0;

                     var tdbUpdater = renderContext.TakeUpdater(_textureDescriptorBuffer);

                     var mrbUpdater = renderContext.TakeUpdater(_materialResourcesBuffer);
                     var instancingBuffer = PickInstancingBuffer(batch.Jobs.Count);
                     var instancingBufferUpdater = renderContext.TakeUpdater(instancingBuffer);

                     int instancesToRender = 0;

                     var whiteDefaultTextureBound = -1;

                     for (var i = 0; i < batch.Jobs.Count; i++) {
                        var materialResourcesIndex = batch.Jobs[i].MaterialResourcesIndex;
                        if (mriBound[materialResourcesIndex] == -1) {
                           if (boundMaterialResourceDescriptions + 1 == kMaterialBufferCount || boundTextures > kTextureBindLimit - 4) {
                              instancingBufferUpdater.UpdateAndClose();
                              tdbUpdater.UpdateAndClose();
                              mrbUpdater.UpdateAndClose();

                              // shouldnt be necessary
                              renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
                              renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
                              renderContext.SetShaderResource(12, _materialResourcesBufferSrv, RenderStage.Pixel);

                              // Prepare draw
                              UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal, -1);

                              // Set instance buffer, draw.
                              renderContext.SetVertexBuffer(1, instancingBuffer);
                              batch.Mesh.Draw(renderContext, instancesToRender);
                              renderContext.SetVertexBuffer(1, null);

                              // Reset state
                              for (var j = 0; j < snapshot.Textures.Count; j++) {
                                 boundTextureSlotByTextureIndex[j] = -1;
                              }

                              for (var j = 0; j < snapshot.Materials.Count; j++) {
                                 mriBound[j] = -1;
                              }

                              boundTextures = 0;
                              boundMaterialResourceDescriptions = 0;
                              instancesToRender = 0;

                              whiteDefaultTextureBound = -1;

                              instancingBufferUpdater.Reopen();
                              tdbUpdater.Reopen();
                              mrbUpdater.Reopen();
                           }

                           ref var material = ref snapshot.Materials.store[materialResourcesIndex];

                           // Ensure base texture bound
                           var isTextureBound = material.BaseTextureIndex == -1
                              ? whiteDefaultTextureBound != -1
                              : boundTextureSlotByTextureIndex[material.BaseTextureIndex] != -1;
                           if (!isTextureBound) {
                              var textureSlot = boundTextures + kBaseTextureSlotId;
                              if (material.BaseTextureIndex == -1) {
                                 var textureSrv = _graphicsDevice.PresetsStore.SolidCubeTextures[Color4.White];
                                 renderContext.SetShaderResource(textureSlot, textureSrv, RenderStage.Pixel);
                                 whiteDefaultTextureBound = textureSlot;
                              } else {
                                 var textureSrv = snapshot.Textures[material.BaseTextureIndex];
                                 renderContext.SetShaderResource(textureSlot, textureSrv, RenderStage.Pixel);
                                 boundTextureSlotByTextureIndex[material.BaseTextureIndex] = textureSlot;

                                 if (material.BaseTextureIndex >= snapshot.Textures.Count) {
                                    throw new IndexOutOfRangeException();
                                 }
                              }
                              boundTextures++;

                              tdbUpdater.Write(new TextureDescriptorConstantBufferData { isCubeMap = 1 });
                           }

                           mrbUpdater.Write(material.Resolve(
                              material.BaseTextureIndex == -1
                                 ? whiteDefaultTextureBound
                                 : boundTextureSlotByTextureIndex[material.BaseTextureIndex]
                           ));

                           if (materialResourcesIndex >= snapshot.Materials.Count) {
                              throw new IndexOutOfRangeException();
                           }
                           mriBound[materialResourcesIndex] = boundMaterialResourceDescriptions;
                           boundMaterialResourceDescriptions++;
                        }
                        instancingBufferUpdater.Write(batch.Jobs[i].Resolve(mriBound[materialResourcesIndex]));
                        instancesToRender++;
                     }
                     instancingBufferUpdater.UpdateCloseAndDispose();
                     tdbUpdater.UpdateCloseAndDispose();
                     mrbUpdater.UpdateCloseAndDispose();

                     // Prepare draw
                     UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal, -1);

                     // shouldnt be necessary
                     renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
                     renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
                     renderContext.SetShaderResource(12, _materialResourcesBufferSrv, RenderStage.Pixel);

                     // Set instance buffer, draw.
                     renderContext.SetVertexBuffer(1, instancingBuffer);
                     batch.Mesh.Draw(renderContext, instancesToRender);
                     renderContext.SetVertexBuffer(1, null);
                  } else {
                     ref var material = ref snapshot.Materials.store[batch.MaterialResourcesIndexOverride];

                     // shouldnt be necessary
                     renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
                     renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
                     renderContext.SetShaderResource(12, _materialResourcesBufferSrv, RenderStage.Pixel);

                     // Bind textures
                     renderContext.SetShaderResource(30, snapshot.Textures[material.BaseTextureIndex], RenderStage.Pixel);

                     // Write to material resource index 0
                     var mrbu = renderContext.TakeUpdater(_materialResourcesBuffer);
                     mrbu.Write(material.Resolve(30));
                     mrbu.UpdateCloseAndDispose();
                     renderContext.Update(_materialResourcesBuffer, material.Resolve(30));

                     // Prepare draw
                     UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal, 0);

                     // Pick instancing buffer, update (jobs fully correct, job resource indices will be ignored).
                     var instancingBuffer = PickInstancingBuffer(batch.Jobs.Count);
                     renderContext.Update(instancingBuffer, batch.Jobs.store, 0, batch.Jobs.Count);

                     // Set instance buffer, draw.
                     renderContext.SetVertexBuffer(1, instancingBuffer);
                     batch.Mesh.Draw(renderContext, batch.Jobs.Count);
                     renderContext.SetVertexBuffer(1, null);
                  }
               }
            }
         } else {
            //            var baseAndMaterialRtv = _gBufferRtvs[0];
            //            var normalRtv = _gBufferRtvs[1];
            //            renderContext.SetRenderTargets(_gBufferDsv, baseAndMaterialRtv, normalRtv);
            //            renderContext.SetViewportRect(new RectangleF(0, 0, baseAndMaterialRtv.Resolution.Width, baseAndMaterialRtv.Resolution.Height));
            //            renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
            //
            //            renderContext.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
            //            renderContext.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
            //            renderContext.SetShaderResource(12, _materialResourcesBufferSrv, RenderStage.Pixel);
            //
            //            renderContext.ClearRenderTargets(Color.Transparent, Color.Transparent);
            //            renderContext.ClearDepthBuffer(1.0f);
            //
            //            UpdateSceneConstantBuffer(renderContext, new Vector4(_cameraEye, 1.0f), _projView, _projViewInv, true, true, spotlightInfos.Count);
            //            for (var pass = 0; pass < Techniques.DeferredToGBuffer.Passes; pass++) {
            //
            //               Techniques.DeferredToGBuffer.BeginPass(renderContext, pass);
            //               foreach (var batch in renderJobBatches) {
            //                  renderContext.SetShaderResource(1, batch.BaseTexture, RenderStage.Pixel);
            //
            //                  UpdateBatchConstantBuffer(renderContext, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal, batch.MaterialTexturesIndexOverride);
            //                  var instancingBuffer = PickAndUpdateInstancingBuffer(renderContext, batch.Jobs);
            //
            //                  renderContext.SetVertexBuffer(1, instancingBuffer);
            //                  batch.Mesh.Draw(renderContext, batch.Jobs.Count);
            //                  renderContext.SetVertexBuffer(1, null);
            //               }
            //            }
            //
            //            // Restore render targets, merge gbuffers
            //            renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
            //            renderContext.ClearDepthBuffer(1.0f);
            //            renderContext.SetViewportRect(new RectangleF(0, 0, backBufferRenderTargetView.Resolution.Width, backBufferRenderTargetView.Resolution.Height));
            //            renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);
            //
            //            for (var pass = 0; pass < Techniques.DeferredFromGBuffer.Passes; pass++) {
            //               Techniques.DeferredFromGBuffer.BeginPass(renderContext, pass);
            //
            //               var (proj, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, 0, 0, backBufferDepthStencilView.Resolution.Width, backBufferDepthStencilView.Resolution.Height, -2.0f);
            //               UpdateSceneConstantBuffer(renderContext, new Vector4(_cameraEye, 1.0f), _projView, _projViewInv, true, true, spotlightInfos.Count);
            //               UpdateBatchConstantBuffer(renderContext, proj, 0, kBatchNoMaterialIndexOverride);
            //               DrawScreenQuad(renderContext, world, _gBufferSrvs[0], _gBufferSrvs[1], _gBufferDepthSrv);
            //            }
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

            if (forward) continue;
            var pipGBufferWidth = pipScale * backBufferRenderTargetView.Resolution.Width / backBufferRenderTargetView.Resolution.Height;
            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVNoAlpha, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, 0, pipScale, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, snapshot.ProjViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferSrvs[0]);

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVNoAlpha, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, pipGBufferWidth, pipScale, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, snapshot.ProjViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferSrvs[1]);

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVUnpackMaterialW, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, 0, pipScale * 2, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, snapshot.ProjViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferSrvs[1]);

            UpdateBatchConstantBuffer(renderContext, Matrix.Identity, DiffuseTextureSamplingMode.FlatUVGrayscaleDerivative, kBatchNoMaterialIndexOverride);
            (projView, world) = ComputeSceneQuadProjWorld(backBufferDepthStencilView.Resolution, pipGBufferWidth, pipScale * 2, pipGBufferWidth, pipScale);
            UpdateSceneConstantBuffer(renderContext, Vector4.Zero, projView, snapshot.ProjViewInv, false, false, 0);
            DrawScreenQuad(renderContext, world, _gBufferDepthSrv);
         }

         //using (var commandList = renderContext.FinishCommandListAndFree()) {
         //   _graphicsDevice.ImmediateContext.ExecuteCommandList(commandList);
         //}
         _graphicsDevice.ImmediateContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);

         _graphicsDevice.ImmediateContext.Present();
      }

      private (Matrix, Matrix) ComputeSceneQuadProjWorld(Size renderTargetSize, float x, float y, float w, float h, float z = -1.0f) {
         var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, renderTargetSize.Width, renderTargetSize.Height, 0.0f, 0.1f, 100.0f); // top-left origin
         var quadWorld = MatrixCM.Translation(x, y, 0) * MatrixCM.Scaling(w, h, 1) * MatrixCM.Translation(0.5f, 0.5f, z);
         return (orthoProj, quadWorld);
      }

      private void DrawScreenQuad(IDeviceContext deviceContext, Matrix world, IShaderResourceView textureSrv0, IShaderResourceView textureSrv1 = null, IShaderResourceView textureSrv2 = null) {
         var instancingBuffer = PickInstancingBuffer(1);
         deviceContext.Update(instancingBuffer, new RenderJobDescription { WorldTransform = world });

         deviceContext.SetShaderResource(0, textureSrv0, RenderStage.Pixel);
         deviceContext.SetShaderResource(1, textureSrv1, RenderStage.Pixel);
         deviceContext.SetShaderResource(2, textureSrv2, RenderStage.Pixel);

         deviceContext.SetVertexBuffer(1, instancingBuffer);
         _graphicsDevice.PresetsStore.UnitPlaneXY.Draw(deviceContext, 1);
         deviceContext.SetVertexBuffer(1, null);
      }

      private void UpdateSceneConstantBuffer(IDeviceContext deviceContext, Vector4 cameraEye, Matrix projView, Matrix projViewInv, bool pbrEnabled, bool shadowTestEnabled, int numSpotlights) {
         deviceContext.Update(_sceneBuffer, new SceneConstantBufferData {
            cameraEye = cameraEye,
            projView = projView,
            projViewInv = projViewInv,
            pbrEnabled = pbrEnabled ? 1 : 0,
            shadowTestEnabled = shadowTestEnabled ? 1 : 0,
            numSpotlights = numSpotlights,
            padding = 0
         });
      }

      private void UpdateBatchConstantBuffer(IDeviceContext deviceContext, Matrix batchTransform, DiffuseTextureSamplingMode diffuseSamplingMode, int batchMaterialIndexOverride) {
         deviceContext.Update(_batchBuffer, new BatchConstantBufferData {
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

      private unsafe void ComputeSpotlightDescriptions(SceneSnapshot snapshot, SpotlightDescription* res) {
         for (var i = 0; i < snapshot.SpotlightInfos.Count; i++) {
            res[i].AtlasLocation = new AtlasLocation {
               Position = new Vector3(0, 0, i),
               Size = new Vector2(1, 1)
            };
            res[i].SpotlightInfo = snapshot.SpotlightInfos[i];
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

      [StructLayout(LayoutKind.Sequential, Pack = 4)]
      private struct TextureDescriptorConstantBufferData {
         public int isCubeMap;

         public const int Size = 4;
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
   }

   public interface ISceneSnapshot { }

   internal class SceneSnapshot : ISceneSnapshot {
      public AddOnlyOrderedHashSet<IShaderResourceView> Textures = new AddOnlyOrderedHashSet<IShaderResourceView>();
      public ExposedArrayList<InternalMaterialResourcesDescription> Materials = new ExposedArrayList<InternalMaterialResourcesDescription>();
      // public Dictionary<IMesh, RenderJobBatch> DefaultRenderJobBatchesByMesh = new Dictionary<IMesh, RenderJobBatch>();
      public List<RenderJobBatch> RenderJobBatches = new List<RenderJobBatch>();
      public List<SpotlightInfo> SpotlightInfos = new List<SpotlightInfo>();

      public Vector3 CameraEye;
      public Matrix ProjView;
      public Matrix ProjViewInv;
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

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct MaterialProperties {
      public float Metallic;
      public float Roughness;

      public const int Size = 4 * 2;
   }

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   internal struct InternalMaterialResourcesDescription {
      public int BaseTextureIndex;
      public int padding0, padding1, padding2;
      public Color4 BaseColor;

      public const int Size = 4 * 4 + 4 * 4;

      public InternalMaterialResourcesDescription Resolve(int resolvedBaseTextureIndex) {
         return new InternalMaterialResourcesDescription {
            BaseTextureIndex = resolvedBaseTextureIndex,
            BaseColor = BaseColor
         };
      }
   }

   public struct MaterialResourcesDescription {
      public IShaderResourceView BaseTexture;
      public Color4 BaseColor;

      internal InternalMaterialResourcesDescription ToInternal(int baseTextureIndex) {
         return new InternalMaterialResourcesDescription {
            BaseTextureIndex = baseTextureIndex,
            BaseColor = Fallback(BaseColor, Color4.White)
         };
      }

      private static T Fallback<T>(T val, T fallback) where T : struct => val.Equals(default(T)) ? fallback : val;

      public override bool Equals(object obj) {
         return obj is MaterialResourcesDescription o &&
                BaseTexture == o.BaseTexture &&
                BaseColor == o.BaseColor;
      }

      public override int GetHashCode() {
         var hashCode = -1531171848;
         hashCode = hashCode * -1521134295 + (BaseTexture?.GetHashCode() ?? 27);
         hashCode = hashCode * -1521134295 + BaseColor.GetHashCode();
         return hashCode;
      }
   }

   public struct MaterialDescription {
      public MaterialProperties Properties;
      public MaterialResourcesDescription Resources;
   }
   
   public struct RenderJobBatch {
      public IMesh Mesh;
      public Matrix BatchTransform;
      public ExposedArrayList<RenderJobDescription> Jobs;
      public int MaterialResourcesIndexOverride;

      public static RenderJobBatch Create(IMesh mesh = null) {
         return new RenderJobBatch {
            BatchTransform = Matrix.Identity,
            Jobs = new ExposedArrayList<RenderJobDescription>(),
            Mesh = mesh,
            MaterialResourcesIndexOverride = -1
         };
      }
   }

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct RenderJobDescription {
      public Matrix WorldTransform;
      public MaterialProperties MaterialProperties;
      public int MaterialResourcesIndex;

      public const int Size = 4 * 4 * 4 * 1 + MaterialProperties.Size + 4;

      public RenderJobDescription Resolve(int resolvedMaterialResourcesIndex) {
         return new RenderJobDescription {
            WorldTransform = WorldTransform,
            MaterialProperties = MaterialProperties,
            MaterialResourcesIndex = resolvedMaterialResourcesIndex
         };
      }
   }
}
