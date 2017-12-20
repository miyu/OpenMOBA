using System;
using System.Collections.Generic;
using System.Diagnostics;
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
      private readonly Dictionary<IMesh, List<RenderJobDescription>> renderJobDescriptionsByMesh = new Dictionary<IMesh, List<RenderJobDescription>>();
      private readonly List<SpotlightInfo> spotlightInfos = new List<SpotlightInfo>();

      private readonly IGraphicsDevice _graphicsDevice;
      private readonly Device _d3d;
      private readonly Buffer _sceneBuffer;
      private readonly Buffer _objectBuffer;
      private readonly Buffer _instancingBuffer;
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

         _sceneBuffer = new Buffer(
            _d3d,
            ((Utilities.SizeOf<Matrix>() + Utilities.SizeOf<bool>() + Utilities.SizeOf<int>()) / 16 + 1) * 16,
            ResourceUsage.Dynamic, 
            BindFlags.ConstantBuffer, 
            CpuAccessFlags.Write, 
            ResourceOptionFlags.None,
            0);

         _objectBuffer = new Buffer(
            _d3d,
            1 * Utilities.SizeOf<Matrix>(),
            ResourceUsage.Dynamic,
            BindFlags.ConstantBuffer,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            0);

         _instancingBuffer = new Buffer(
            _d3d,
            256 * RenderJobDescription.Size,
            ResourceUsage.Dynamic,
            BindFlags.ConstantBuffer,
            CpuAccessFlags.Write,
            ResourceOptionFlags.None,
            RenderJobDescription.Size
         );

         var shadowMapEntriesBufferLength = 256;
         _shadowMapEntriesBuffer = new Buffer(
            _d3d,
            shadowMapEntriesBufferLength * SpotlightDescription.Size,
            ResourceUsage.Dynamic,
            BindFlags.ShaderResource,
            CpuAccessFlags.Write,
            ResourceOptionFlags.BufferStructured,
            SpotlightDescription.Size);
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
                  ElementWidth = SpotlightDescription.Size
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

         Trace.Assert(Utilities.SizeOf<SpotlightInfo>() == SpotlightInfo.Size);
         Trace.Assert(Utilities.SizeOf<AtlasLocation>() == AtlasLocation.SIZE);
         Trace.Assert(Utilities.SizeOf<SpotlightDescription>() == SpotlightDescription.Size);
      }

      public ITechniqueCollection Techniques => _graphicsDevice.TechniqueCollection;

      public void SetProjView(Matrix projView) {
         _projView = projView;
      }

      public void ClearScene() {
         foreach (var kvp in renderJobDescriptionsByMesh) {
            kvp.Value.Clear();
         }
         spotlightInfos.Clear();
      }

      public void AddRenderable(IMesh mesh, Matrix worldCm) {
         AddRenderable(mesh, new RenderJobDescription {
            WorldCM = worldCm,
         });
      }

      public void AddRenderable(IMesh mesh, RenderJobDescription info) {
         if (!renderJobDescriptionsByMesh.TryGetValue(mesh, out var renderables)) {
            renderables = renderJobDescriptionsByMesh[mesh] = new List<RenderJobDescription>();
         }
         renderables.Add(info);
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

      private void UpdateSceneConstantBuffer(Matrix projView, bool shadowTestEnabled, int numSpotlights) {
         var db = _d3d.ImmediateContext.MapSubresource(_sceneBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
         var off = db.DataPointer;
         off = Utilities.WriteAndPosition(off, ref projView);
         off = Utilities.WriteAndPosition(off, ref shadowTestEnabled);
         off += 3; // 4-byte alignment in HLSL struct
         off = Utilities.WriteAndPosition(off, ref numSpotlights);
         _d3d.ImmediateContext.UnmapSubresource(_sceneBuffer, 0);
      }

      private void UpdateObjectConstantBuffer(Matrix world) {
         var db = _d3d.ImmediateContext.MapSubresource(_objectBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
         var off = db.DataPointer;
         off = Utilities.WriteAndPosition(off, ref world);
         _d3d.ImmediateContext.UnmapSubresource(_objectBuffer, 0);
      }

      public unsafe void RenderScene() {
         var renderContext = _graphicsDevice.ImmediateContext;
         renderContext.ClearRenderTarget(Color.Gray);
         renderContext.ClearDepthBuffer(1.0f);

         renderContext.SetDepthConfiguration(DepthConfiguration.Enabled);

         // Store backbuffer render targets for screen draw
         renderContext.GetRenderTargets(out var backBufferDepthStencilView, out var backBufferRenderTargetView);

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

            UpdateSceneConstantBuffer(spotlightDescription->SpotlightInfo.ProjViewCM, false, 0);

            for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
               Techniques.Forward.BeginPass(renderContext, pass);
               _d3d.ImmediateContext.VertexShader.SetConstantBuffer(0, _sceneBuffer);
               _d3d.ImmediateContext.VertexShader.SetConstantBuffer(1, _objectBuffer);
               _d3d.ImmediateContext.PixelShader.SetConstantBuffer(0, _sceneBuffer);
               _d3d.ImmediateContext.PixelShader.SetConstantBuffer(1, _objectBuffer);

               foreach (var kvp in renderJobDescriptionsByMesh) {
                  var mesh = kvp.Key;
                  var jobs = kvp.Value;
                  mesh.Draw(renderContext);
//                  for (var i = 0; i < jobs.Count; i++) {
//                     var renderable = jobs[i];
//                     UpdateObjectConstantBuffer(renderable.WorldCM);
//
//                     mesh.Draw(renderContext);
//                  }
               }
            }
         }

         // Prepare for scene render
         var box = _d3d.ImmediateContext.MapSubresource(_shadowMapEntriesBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
         Utilities.CopyMemory(box.DataPointer, (IntPtr)spotlightDescriptions, spotlightInfos.Count * SpotlightDescription.Size);
         _d3d.ImmediateContext.UnmapSubresource(_shadowMapEntriesBuffer, 0);
         _d3d.ImmediateContext.PixelShader.SetShaderResource(10, _lightShaderResourceView);
         _d3d.ImmediateContext.PixelShader.SetShaderResource(11, _shadowMapEntriesBufferSrv);

         // Draw Scene
         renderContext.SetRenderTargets(backBufferDepthStencilView, backBufferRenderTargetView);
         renderContext.SetViewportRect(new RectangleF(0, 0, 1280, 720));
         renderContext.SetRasterizerConfiguration(RasterizerConfiguration.FillFront);

         UpdateSceneConstantBuffer(_projView, true, spotlightInfos.Count);
         for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
            Techniques.Forward.BeginPass(renderContext, pass);
            _d3d.ImmediateContext.VertexShader.SetConstantBuffer(0, _sceneBuffer);
            _d3d.ImmediateContext.VertexShader.SetConstantBuffer(1, _objectBuffer);
            _d3d.ImmediateContext.PixelShader.SetConstantBuffer(0, _sceneBuffer);
            _d3d.ImmediateContext.PixelShader.SetConstantBuffer(1, _objectBuffer);
            _d3d.ImmediateContext.PixelShader.SetShaderResource(0, _whiteTextureShaderResourceView);
            _d3d.ImmediateContext.PixelShader.SetShaderResource(10, _lightShaderResourceView);
            _d3d.ImmediateContext.PixelShader.SetShaderResource(11, _shadowMapEntriesBufferSrv);

            foreach (var kvp in renderJobDescriptionsByMesh) {
               var mesh = kvp.Key;
               var jobs = kvp.Value;
               for (var i = 0; i < jobs.Count; i++) {
                  var renderable = jobs[i];
                  UpdateObjectConstantBuffer(renderable.WorldCM);
                  mesh.Draw(renderContext);
               }
            }
         }

         // draw depth texture
         for (var pass = 0; pass < Techniques.Forward.Passes; pass++) {
            Techniques.Forward.BeginPass(renderContext, pass);
            for (var i = 0; i < 2; i++) {
               var orthoProj = MatrixCM.OrthoOffCenterRH(0.0f, 1280.0f, 720.0f, 0.0f, 0.1f, 100.0f); // top-left origin
               var quadWorld = MatrixCM.Scaling(256, 256, 0) * MatrixCM.Translation(0.5f + i, 0.5f, 0.0f);
               UpdateSceneConstantBuffer(orthoProj, false, 0);
               UpdateObjectConstantBuffer(quadWorld);

               _d3d.ImmediateContext.VertexShader.SetConstantBuffer(0, _sceneBuffer);
               _d3d.ImmediateContext.VertexShader.SetConstantBuffer(1, _objectBuffer);
               _d3d.ImmediateContext.PixelShader.SetConstantBuffer(0, _sceneBuffer);
               _d3d.ImmediateContext.PixelShader.SetConstantBuffer(1, _objectBuffer);
               _d3d.ImmediateContext.PixelShader.SetShaderResource(0, _lightShaderResourceViews[i]);
               _graphicsDevice.MeshPresets.UnitPlaneXY.Draw(renderContext);
            }
         }

         renderContext.Present();
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


      public struct RenderJobDescription {
         public Matrix WorldCM;
         public const int Size = (4 * 4 * 4) * 1;
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

         public const int Size = (3 * 4) * 2 + (4 * 4) * 1 + (4) * 4 + (4 * 4 * 4) * 1;
      }
   }
}