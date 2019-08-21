using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Canvas3D;
using Canvas3D.LowLevel;
using SharpDX;
using Color = SharpDX.Color;
using IDeviceContext = Canvas3D.LowLevel.IDeviceContext;
using RectangleF = System.Drawing.RectangleF;

namespace Dargon.Funkeln {
   public class RenderContext {
      private const int kShadowMapWidthHeight = 256 * 4;
      private const int kBatchNoMaterialIndexOverride = -1;
      private const int kBaseTextureSlotId = 48;
      private const int kTextureBindLimit = 80; // Slots [48, 127)
      private const int kMaterialBufferCount = 256; // Slots [48, 127)

      private readonly IGraphicsFacade _graphicsFacade;
      private readonly IGraphicsDevice _graphicsDevice;
      private readonly ITechniqueCollection _techniques;
      private readonly IPresetsStore _presets;

      private readonly IBuffer<SceneConstantBufferData> _sceneBuffer;
      private readonly IBuffer<BatchConstantBufferData> _batchBuffer;
      private readonly IBuffer<TextureDescriptorConstantBufferData> _textureDescriptorBuffer;

      private readonly IBuffer<SpotlightDescription> _spotlightDescriptionsBuffer;
      private readonly IShaderResourceView _spotlightDescriptionsBufferSrv;
      private readonly IShaderResourceView _materialResourcesBufferSrv;

      private readonly IDepthStencilView[] _lightDepthStencilViews;
      private readonly ITexture2D _lightDepthTexture;
      private readonly IShaderResourceView _lightShaderResourceView;
      private readonly IShaderResourceView[] _lightShaderResourceViews;

      internal RenderContext(IGraphicsFacade graphicsFacade) {
         _graphicsFacade = graphicsFacade;
         _graphicsDevice = graphicsFacade.Device;
         _techniques = graphicsFacade.Techniques;
         _presets = graphicsFacade.Presets;

         _sceneBuffer = _graphicsDevice.CreateConstantBuffer<SceneConstantBufferData>(1);
         _batchBuffer = _graphicsDevice.CreateConstantBuffer<BatchConstantBufferData>(1);
         _textureDescriptorBuffer = _graphicsDevice.CreateConstantBuffer<TextureDescriptorConstantBufferData>(kBaseTextureSlotId + kTextureBindLimit);

         (_spotlightDescriptionsBuffer, _spotlightDescriptionsBufferSrv) = _graphicsDevice.CreateStructuredBufferAndView<SpotlightDescription>(256);
         (_lightDepthTexture, _lightDepthStencilViews, _lightShaderResourceView, _lightShaderResourceViews) = _graphicsDevice.CreateDepthTextureAndViews(10, new Size(kShadowMapWidthHeight, kShadowMapWidthHeight));

         Trace.Assert(Utilities.SizeOf<SpotlightInfo>() == SpotlightInfo.Size);
         Trace.Assert(Utilities.SizeOf<AtlasLocation>() == AtlasLocation.SIZE);
         Trace.Assert(Utilities.SizeOf<SpotlightDescription>() == SpotlightDescription.Size);
         Trace.Assert(Utilities.SizeOf<RenderJobDescription>() == RenderJobDescription.Size);
      }

      public unsafe void RenderScene() {
         _graphicsDevice.ImmediateContext.GetBackBufferViews(out var backBufferDepthStencilView, out var backBufferRenderTargetView);
         var renderContext = _graphicsDevice.ImmediateContext; // : _graphicsDevice.CreateDeferredRenderContext();
         renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFrontBack);
         renderContext.SetDepthConfiguration(DepthConfiguration.Enabled);

         renderContext.SetConstantBuffer(0, _sceneBuffer, RenderStage.VertexDomainPixel);
         renderContext.SetConstantBuffer(1, _batchBuffer, RenderStage.VertexDomainPixel);
         renderContext.SetConstantBuffer(2, _textureDescriptorBuffer, RenderStage.VertexDomainPixel);

         RenderScene_Forward(renderContext, backBufferDepthStencilView, backBufferRenderTargetView);

#if DEBUG
         renderContext.SetConstantBuffer(0, null, RenderStage.VertexDomainPixel);
         renderContext.SetConstantBuffer(1, null, RenderStage.VertexDomainPixel);
         renderContext.SetConstantBuffer(2, null, RenderStage.VertexDomainPixel);
#endif

         _graphicsDevice.ImmediateContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         _graphicsDevice.ImmediateContext.Present();
      }

      private void RenderScene_Forward(IDeviceContext context, IDepthStencilView backBufferDepthStencilView, IRenderTargetView backBufferRenderTargetView) {
         // RenderShadowMaps(context, scene);

         // Restore backbuffer rendertarget + scene constant buffer + srvs.
         context.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         context.SetViewportRect(new SharpDX.RectangleF(0, 0, backBufferRenderTargetView.Resolution.Width, backBufferRenderTargetView.Resolution.Height));
         // UpdateSceneConstantBuffer(context, new Vector4(scene.CameraEye, 1.0f), scene.ProjView, scene.ProjViewInv, scene.ProjView, scene.ProjViewInv, true, true, scene.SpotlightInfos.Count, scene.Time);

         // Clear render/depth, bind srvs after setrendertargets
         context.ClearRenderTarget(SharpDX.Color.Gray);
         context.ClearDepthBuffer(1.0f);
         BindCommonShaderResourceViews(context);

         // Forward render pass
         // for (var pass = 0; pass < _techniques.Forward.Passes; pass++) {
         //    _techniques.Forward.BeginPass(context, pass);
         //    foreach (var batch in scene.RenderJobBatches) {
         //       RenderBatch(context, scene, batch);
         //    }
         // }
      }

      // private void RenderBatch(IDeviceContext context, SceneSnapshot scene, RenderJobBatch batch) {
      //    context.SetRasterizerConfiguration(batch.Wireframe ? RasterizerConfiguration.WireFrontBack : RasterizerConfiguration.FillFrontBack);
      //
      //    ref var material = ref scene.Materials.store[batch.MaterialResourcesIndexOverride];
      //
      //    // Bind textures
      //    context.SetShaderResource(30, scene.Textures[material.BaseTextureIndex], RenderStage.Pixel);
      //
      //    // Write to material resource index 0
      //    var mrbu = context.TakeUpdater(_materialResourcesBuffer);
      //    mrbu.Write(material.Resolve(30));
      //    mrbu.UpdateCloseAndDispose();
      //    context.Update(_materialResourcesBuffer, material.Resolve(30));
      //
      //    // Prepare draw
      //    UpdateBatchConstantBuffer(context, batch.BatchTransform, DiffuseTextureSamplingMode.CubeNormal, 0);
      //
      //    // Pick instancing buffer, update (jobs fully correct, job resource indices will be ignored).
      //    var instancingBuffer = PickInstancingBuffer(batch.Jobs.Count);
      //    context.Update(instancingBuffer, batch.Jobs.store, 0, batch.Jobs.Count);
      //
      //    // Set instance buffer, draw.
      //    context.SetVertexBuffer(1, instancingBuffer);
      //    batch.Mesh.Draw(context, batch.Jobs.Count);
      //    context.SetVertexBuffer(1, null);
      // }
//       
//
//       private unsafe void RenderShadowMaps(IDeviceContext context, SceneSnapshot scene) {
//          // Allocate shadow map atlas
//          var spotlightDescriptions = stackalloc SpotlightDescription[scene.SpotlightInfos.Count];
//          for (var i = 0; i < scene.SpotlightInfos.Count; i++) {
//             spotlightDescriptions[i].AtlasLocation = new AtlasLocation {
//                Position = new Vector3(0, 0, i),
//                Size = new Vector2(1, 1)
//             };
//             spotlightDescriptions[i].SpotlightInfo = scene.SpotlightInfos[i];
//          }
//
//          // Batch shadow map descriptor to gpu (used in lighting passes after this function call)
//          context.Update(_spotlightDescriptionsBuffer, (IntPtr)spotlightDescriptions, scene.SpotlightInfos.Count);
//
//          // Clear shadow map buffers.
// #if PERMIT_STACKALLOC_OPTIMIZATIONS
//          var lightDepthStencilViewCleared = stackalloc bool[_lightDepthStencilViews.Length];
// #else
//          var lightDepthStencilViewCleared = new bool[_lightDepthStencilViews.Length];
// #endif
//          for (var i = 0; i < scene.SpotlightInfos.Count; i++) {
//             var ldsvIndex = (int)spotlightDescriptions[i].AtlasLocation.Position.Z;
//             if (ldsvIndex >= _lightDepthStencilViews.Length) throw new IndexOutOfRangeException();
//             if (lightDepthStencilViewCleared[ldsvIndex]) continue;
//             lightDepthStencilViewCleared[ldsvIndex] = true;
//             context.SetRenderTargets(_lightDepthStencilViews[ldsvIndex], null);
//             context.ClearDepthBuffer(1.0f);
//          }
//
//          // shadow passes
//          for (var spotlightIndex = 0; spotlightIndex < scene.SpotlightInfos.Count; spotlightIndex++) {
//             var spotlightDescription = &spotlightDescriptions[spotlightIndex];
//             context.SetRenderTargets(_lightDepthStencilViews[(int)spotlightDescription->AtlasLocation.Position.Z], null);
//             context.SetViewportRect((Vector2)spotlightDescription->AtlasLocation.Position, kShadowMapWidthHeight * spotlightDescription->AtlasLocation.Size);
//
//             UpdateSceneConstantBuffer(context, new Vector4(spotlightDescription->SpotlightInfo.Origin, 1.0f), spotlightDescription->SpotlightInfo.ProjViewCM, scene.ProjView, scene.ProjViewInv, Matrix.Zero, false, false, 0, scene.Time);
//             for (var pass = 0; pass < _techniques.ForwardDepthOnly.Passes; pass++) {
//                _techniques.ForwardDepthOnly.BeginPass(context, pass);
//
//                foreach (var batch in scene.RenderJobBatches) {
//                   UpdateBatchConstantBuffer(context, batch.BatchTransform, 0, batch.MaterialResourcesIndexOverride);
//
//                   var instancingBuffer = PickInstancingBuffer(batch.Jobs.Count);
//                   context.Update(instancingBuffer, batch.Jobs.store, 0, batch.Jobs.Count);
//                   context.SetVertexBuffer(1, instancingBuffer);
//                   batch.Mesh.Draw(context, batch.Jobs.Count);
//                   context.SetVertexBuffer(1, null);
//                }
//             }
//          }
//       }

      private void BindCommonShaderResourceViews(IDeviceContext context) {
         context.SetShaderResource(10, _lightShaderResourceView, RenderStage.Pixel);
         context.SetShaderResource(11, _spotlightDescriptionsBufferSrv, RenderStage.Pixel);
         context.SetShaderResource(12, _materialResourcesBufferSrv, RenderStage.Pixel);
      }

      private (Matrix, Matrix) ComputeSceneQuadProjWorld(Size renderTargetSize, float x, float y, float w, float h, float z = -1.0f) {
         var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, renderTargetSize.Width, renderTargetSize.Height, 0.0f, 0.1f, 100.0f); // top-left origin
         var quadWorld = MatrixCM.Translation(x, y, 0) * MatrixCM.Scaling(w, h, 1) * MatrixCM.Translation(0.5f, 0.5f, z);
         return (orthoProj, quadWorld);
      }

      // private void DrawScreenQuad(IDeviceContext deviceContext, Matrix world, IShaderResourceView textureSrv0, IShaderResourceView textureSrv1 = null, IShaderResourceView textureSrv2 = null) {
      //    var instancingBuffer = PickInstancingBuffer(1);
      //    deviceContext.Update(instancingBuffer, new RenderJobDescription { WorldTransform = world });
      //
      //    deviceContext.SetShaderResource(0, textureSrv0, RenderStage.Pixel);
      //    deviceContext.SetShaderResource(1, textureSrv1, RenderStage.Pixel);
      //    deviceContext.SetShaderResource(2, textureSrv2, RenderStage.Pixel);
      //
      //    deviceContext.SetVertexBuffer(1, instancingBuffer);
      //    _presets.UnitPlaneXY.Draw(deviceContext, 1);
      //    deviceContext.SetVertexBuffer(1, null);
      // }

      // private void UpdateSceneConstantBuffer(IDeviceContext deviceContext, Vector4 cameraEye, Matrix projViewCamera, Matrix projViewCameraInv, Matrix projViewMain, Matrix projViewMainInv, bool pbrEnabled, bool shadowTestEnabled, int numSpotlights, float time) {
      //    deviceContext.Update(_sceneBuffer, new SceneConstantBufferData {
      //       cameraEye = cameraEye,
      //       projViewCamera = projViewCamera,
      //       projViewCameraInv = projViewCameraInv,
      //       projViewMain = projViewMain,
      //       projViewMainInv = projViewMainInv,
      //       pbrEnabled = pbrEnabled ? 1 : 0,
      //       shadowTestEnabled = shadowTestEnabled ? 1 : 0,
      //       numSpotlights = numSpotlights,
      //       time = time
      //    });
      // }

      [StructLayout(LayoutKind.Sequential, Pack = 1)]
      private struct SceneConstantBufferData {
         public Vector4 cameraEye;
         public Matrix projViewCamera;
         public Matrix projViewCameraInv;
         public Matrix projViewMain; // projview of main camera (if projViewCamera is for something else like screenspace quad)
         public Matrix projViewMainInv; // projview of main camera (if projViewCamera is for something else like screenspace quad)
         public int pbrEnabled;
         public int shadowTestEnabled;
         public int numSpotlights;
         public float time;

         public const int Size = 16 + 64 * 4 + 4 * 3 + 4;
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

   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct RenderJobDescription {
      public Matrix WorldTransform;
      public Color Color;

      public const int Size = 4 * 4 * 4 * 1 + 4;

      public RenderJobDescription Resolve(int resolvedMaterialResourcesIndex) {
         return new RenderJobDescription {
            WorldTransform = WorldTransform,
            Color = Color
         };
      }
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
