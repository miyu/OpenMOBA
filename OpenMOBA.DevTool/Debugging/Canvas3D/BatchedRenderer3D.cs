using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using Shade;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Color = SharpDX.Color;
using Device = SharpDX.Direct3D11.Device;
using RectangleF = SharpDX.RectangleF;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.Direct3D11.Resource;

namespace OpenMOBA.DevTool.Debugging.Canvas3D {
   public class BatchedRenderer3D {
      private readonly Dictionary<ITechnique, List<RenderableInfo>> renderablesBySceneTechnique = new Dictionary<ITechnique, List<RenderableInfo>>();
      private readonly Dictionary<ITechnique, List<RenderableInfo>> renderablesByShadowTechnique = new Dictionary<ITechnique, List<RenderableInfo>>();
      private readonly List<SpotlightInfo> spotlightInfos = new List<SpotlightInfo>();

      private readonly IGraphicsDevice _graphicsDevice;
      private readonly Device _d3d;
      private readonly Buffer _constantBuffer;
      private readonly Buffer _shadowMapEntriesBuffer;
      private readonly ShaderResourceView _shadowMapEntriesBufferSrv;
      private readonly Texture2D _lightDepthBuffer;
      private readonly IDepthStencilView[] _lightDepthStencilViews;
      private readonly ShaderResourceView _lightShaderResourceView;
      private readonly ShaderResourceView[] _lightShaderResourceViews;
      private readonly Texture2D _whiteTexture;
      private readonly ShaderResourceView _whiteTextureShaderResourceView;
      private Matrix _projView;

      public BatchedRenderer3D(IGraphicsDevice graphicsDevice) {
         _graphicsDevice = graphicsDevice;
         _d3d = ((Direct3DGraphicsDevice)graphicsDevice).InternalD3DDevice;
         _constantBuffer = new Buffer(
            _d3d, 
            3 * Utilities.SizeOf<Matrix>(),
            ResourceUsage.Default, 
            BindFlags.ConstantBuffer, 
            CpuAccessFlags.None, 
            ResourceOptionFlags.None,
            0);

         var shadowMapEntriesBufferLength = 256;
         _shadowMapEntriesBuffer = new Buffer(
            _d3d,
            shadowMapEntriesBufferLength * ShadowMapEntry.Size,
            ResourceUsage.Dynamic,
            BindFlags.ShaderResource,
            CpuAccessFlags.Write,
            ResourceOptionFlags.BufferStructured,
            ShadowMapEntry.Size);
         _shadowMapEntriesBufferSrv = new ShaderResourceView(
            _d3d, 
            _shadowMapEntriesBuffer,
            new ShaderResourceViewDescription{
               Dimension = ShaderResourceViewDimension.Buffer,
               Format = Format.Unknown,
               Buffer = {
                  ElementCount = shadowMapEntriesBufferLength,
                  FirstElement = 0,
                  ElementOffset = 0,
                  ElementWidth = ShadowMapEntry.Size
               }
            });

         var shadowMapResolution = new Size(2048, 2048);
         var shadowMapBufferCount = 10;
         _lightDepthBuffer = new Texture2D(_d3d,
            new Texture2DDescription {
               Format = Format.R16_Typeless,
               ArraySize = shadowMapBufferCount,
               MipLevels = 1,
               Width = shadowMapResolution.Width,
               Height = shadowMapResolution.Height,
               SampleDescription = new SampleDescription(1, 0),
               Usage = ResourceUsage.Default,
               BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
               CpuAccessFlags = CpuAccessFlags.None,
               OptionFlags = ResourceOptionFlags.None
            });
         _lightDepthStencilViews = new IDepthStencilView[shadowMapBufferCount];
         for (var i = 0; i < shadowMapBufferCount; i++) {
            _lightDepthStencilViews[i] = new Direct3DGraphicsDevice.DepthStencilViewBox {
               DepthStencilView = new DepthStencilView(_d3d, _lightDepthBuffer,
                  new DepthStencilViewDescription {
                     Format = Format.D16_UNorm,
                     Dimension = DepthStencilViewDimension.Texture2DArray,
                     Texture2DArray = {
                        ArraySize = 1,
                        FirstArraySlice = i,
                        MipSlice = 0
                     }
                  })
            };
         }
         _lightShaderResourceView = new ShaderResourceView(_d3d, _lightDepthBuffer,
            new ShaderResourceViewDescription {
               Format = Format.R16_UNorm,
               Dimension = ShaderResourceViewDimension.Texture2DArray,
               Texture2DArray = {
                  MipLevels = 1,
                  MostDetailedMip = 0,
                  ArraySize = shadowMapBufferCount,
                  FirstArraySlice = 0
               }
            });

         _lightShaderResourceViews = new ShaderResourceView[shadowMapBufferCount];
         for (var i = 0; i < shadowMapBufferCount; i++) {
            _lightShaderResourceViews[i] = new ShaderResourceView(_d3d, _lightDepthBuffer,
               new ShaderResourceViewDescription {
                  Format = Format.R16_UNorm,
                  Dimension = ShaderResourceViewDimension.Texture2DArray,
                  Texture2DArray = {
                     MipLevels = 1,
                     MostDetailedMip = 0,
                     ArraySize = 1,
                     FirstArraySlice = i
                  }
               });
         }

         _whiteTexture = new Texture2D(_d3d, new Texture2DDescription{
            Format = Format.R8G8B8A8_UNorm,
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
         _d3d.ImmediateContext.MapSubresource(_whiteTexture, 0, 0, MapMode.WriteDiscard, MapFlags.None, out stream);
         stream.Write(Color.White);
         _d3d.ImmediateContext.UnmapSubresource(_whiteTexture, 0);

         _whiteTextureShaderResourceView = new ShaderResourceView(_d3d, _whiteTexture);
      }

      public void SetProjView(Matrix projView) {
         _projView = projView;
      }

      public void ClearScene() {
         foreach (var kvp in renderablesBySceneTechnique) {
            kvp.Value.Clear();
         }
         foreach (var kvp in renderablesByShadowTechnique) {
            kvp.Value.Clear();
         }
         spotlightInfos.Clear();
      }

      public void AddRenderable(Matrix worldCm, IMesh mesh) {
         AddRenderable(mesh.DefaultRenderTechnique, mesh.DefaultDepthOnlyRenderTechnique, new RenderableInfo {
            WorldCM = worldCm,
            Mesh = mesh
         });
      }

      public void AddRenderable(ITechnique sceneTechnique, ITechnique shadowTechnique, RenderableInfo info) {
         List<RenderableInfo> infos;
         if (!renderablesBySceneTechnique.TryGetValue(sceneTechnique, out infos)) {
            infos = new List<RenderableInfo>();
            renderablesBySceneTechnique[sceneTechnique] = infos;
         }
         infos.Add(info);

         if (!renderablesByShadowTechnique.TryGetValue(shadowTechnique, out infos)) {
            infos = new List<RenderableInfo>();
            renderablesByShadowTechnique[shadowTechnique] = infos;
         }
         infos.Add(info);
      }

      public void AddSpotlight(Vector3 position, Vector3 lookat, float theta, Color color) {
         var proj = MatrixCM.PerspectiveFovRH(theta, 1.0f, 0.1f, 100.0f);

         var up = Vector3.Up; // todo: handle degenerate
         var view = MatrixCM.LookAtRH(position, lookat, up);
         AddSpotlight(new SpotlightInfo { ProjViewCM = proj * view, Color = color });
      }

      public void AddSpotlight(SpotlightInfo info) {
         spotlightInfos.Add(info);
      }

      public void RenderScene() {
         var renderContext = _graphicsDevice.ImmediateContext;
         renderContext.ClearRenderTarget(Color.Gray);
         renderContext.ClearDepthBuffer(1.0f);

         renderContext.SetDepthConfiguration(DepthConfiguration.Enabled);
         renderContext.SetRasterizerConfiguration(RasterizerConfiguration.Fill);

         // Store backbuffer render targets for screen draw
         IDepthStencilView backBufferDepthStencilView;
         IRenderTargetView backBufferRenderTargetView;
         renderContext.GetRenderTargets(out backBufferDepthStencilView, out backBufferRenderTargetView);

         // Draw spotlights
         var shadowMapEntries = BuildSpotlightShadowMapPlan();

         foreach (var lsdv in _lightDepthStencilViews) {
            renderContext.SetRenderTargets(lsdv, null);
            renderContext.ClearDepthBuffer(1.0f);
         }

         foreach (var shadowMapEntry in shadowMapEntries) {
            renderContext.SetRenderTargets(_lightDepthStencilViews[(int)shadowMapEntry.AtlasLocation.Position.Z], null);
            renderContext.ClearDepthBuffer(1.0f);
         
            var atlasLocation = shadowMapEntry.AtlasLocation;
            renderContext.SetViewportRect(new RectangleF(atlasLocation.Position.X, atlasLocation.Position.Y, 2048 * atlasLocation.Size.X, 2048 * atlasLocation.Size.Y));
         
            foreach (var techniqueAndRenderables in renderablesByShadowTechnique) {
               var renderables = techniqueAndRenderables.Value;
         
               foreach (var renderable in renderables) {
                  var world = renderable.WorldCM;
                  _d3d.ImmediateContext.UpdateSubresource(new[] { shadowMapEntry.SpotlightInfo.ProjViewCM, world, Matrix.Identity }, _constantBuffer, 0);
                  _d3d.ImmediateContext.PixelShader.SetConstantBuffer(0, _constantBuffer);
                  _d3d.ImmediateContext.VertexShader.SetConstantBuffer(0, _constantBuffer);
         
                  renderable.Mesh.Draw(renderContext);
               }
            }
         }

         // Prepare for scene render
//         _d3d.ImmediateContext.UpdateSubresource(shadowMapEntries, _shadowMapEntriesBuffer);
         var box = _d3d.ImmediateContext.MapSubresource(_shadowMapEntriesBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
         var cur = box.DataPointer;
         for (var i = 0; i < shadowMapEntries.Length; i++) {
            var nextCur = Utilities.WriteAndPosition(cur, ref shadowMapEntries[i]);
//            Console.WriteLine("OFfset: " + (nextCur.ToInt64() - cur.ToInt64() + " " + ShadowMapEntry.Size));
            cur = nextCur;
         }
         _d3d.ImmediateContext.UnmapSubresource(_shadowMapEntriesBuffer, 0);
         _d3d.ImmediateContext.PixelShader.SetShaderResource(10, _lightShaderResourceView);
         _d3d.ImmediateContext.PixelShader.SetShaderResource(11, _shadowMapEntriesBufferSrv);

         // Draw Scene
         renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         renderContext.SetViewportRect(new RectangleF(0, 0, 1280, 720));

         foreach (var techniqueAndRenderables in renderablesBySceneTechnique) {
            var technique = techniqueAndRenderables.Key;
            var renderables = techniqueAndRenderables.Value;
         
            for (var pass = 0; pass < technique.Passes; pass++) {
               technique.BeginPass(renderContext, pass);
         
               foreach (var renderable in renderables) {
                  var world = renderable.WorldCM;
                  _d3d.ImmediateContext.UpdateSubresource(new[] { _projView, world, Matrix.Identity }, _constantBuffer, 0);
                  _d3d.ImmediateContext.PixelShader.SetConstantBuffer(0, _constantBuffer);
                  _d3d.ImmediateContext.VertexShader.SetConstantBuffer(0, _constantBuffer);
                  _d3d.ImmediateContext.PixelShader.SetShaderResource(0, _whiteTextureShaderResourceView);
                  _d3d.ImmediateContext.PixelShader.SetShaderResource(10, _lightShaderResourceView);
                  _d3d.ImmediateContext.PixelShader.SetShaderResource(11, _shadowMapEntriesBufferSrv);

                  renderable.Mesh.Draw(renderContext);
               }
            }
         }

         // draw depth texture
         _graphicsDevice.TechniqueCollection.DefaultPositionColorTexture.BeginPass(renderContext, 0);
         for (var i = 0; i < 2 ; i++)
         {
            var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, 1280.0f, 720.0f, 0.0f, 0.1f, 100.0f); // top-left origin
            var quadWorld = MatrixCM.Scaling(256, 256, 0) * MatrixCM.Translation(0.5f + i, 0.5f, 0.0f);
            _d3d.ImmediateContext.UpdateSubresource(new[] { orthoProj, quadWorld, Matrix.Identity }, _constantBuffer, 0);
            _d3d.ImmediateContext.PixelShader.SetShaderResource(0, _lightShaderResourceViews[i]);
            _d3d.ImmediateContext.PixelShader.SetConstantBuffer(0, _constantBuffer);
            _d3d.ImmediateContext.VertexShader.SetConstantBuffer(0, _constantBuffer);
            _graphicsDevice.MeshPresets.UnitPlaneXY.Draw(renderContext);
         }

         renderContext.Present();
      }

      private ShadowMapEntry[] BuildSpotlightShadowMapPlan() {
         var result = new ShadowMapEntry[spotlightInfos.Count];
         for (var i = 0; i < spotlightInfos.Count; i++) {
            result[i].AtlasLocation = new AtlasLocation {
               Position = new Vector3(0, 0, i),
               Size = new Vector2(1, 1)
            };
            result[i].SpotlightInfo = spotlightInfos[i];
         }
         return result;
      }

      [StructLayout(LayoutKind.Sequential)]
      struct AtlasLocation {
         public Vector3 Position;
         public Vector2 Size;

         public const int SIZE = 4 * (3 + 2);
      };

      [StructLayout(LayoutKind.Sequential)]
      struct ShadowMapEntry {
         public AtlasLocation AtlasLocation;
         public SpotlightInfo SpotlightInfo;

         public const int Size = AtlasLocation.SIZE + SpotlightInfo.Size;
      };


      public struct RenderableInfo {
         public Matrix WorldCM;
         public IMesh Mesh;
      }

      [StructLayout(LayoutKind.Sequential)]
      public struct SpotlightInfo {
         public Matrix ProjViewCM;
         public Color4 Color;

         public const int Size = 4 * 4 * 4 + 4 * 4;
      }
   }
}