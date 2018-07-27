using System;
using System.Diagnostics;
using System.IO;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DShaderLoader {
      private readonly Device _device;

      public Direct3DShaderLoader(Device device) {
         _device = device;
      }

      public string BasePath => @"C:\my-repositories\miyu\derp\engine\src\Canvas3D\Assets";

      public IPixelShader LoadPixelShaderFromFile(string relativePath, string entryPoint = null) {
         var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "PS", "ps_5_0");
         var shader = new PixelShader(_device, bytecode);
         return new PixelShaderBox { Shader = shader };
      }

      public IVertexShader LoadVertexShaderFromFile(string relativePath, InputLayoutFormat inputLayoutFormat, string entryPoint = null) {
         var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "VS", "vs_5_0");
         var shader = new VertexShader(_device, bytecode);
         var signature = ShaderSignature.GetInputSignature(bytecode);
         var inputLayout = CreateInputLayout(inputLayoutFormat, signature);
         return new VertexShaderBox { Shader = shader, InputLayout = inputLayout };
      }

      public IHullShader LoadHullShaderFromFile(string relativePath, string entryPoint = null) {
         var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "HS", "hs_5_0");
         var shader = new HullShader(_device, bytecode);
         return new HullShaderBox { Shader = shader };
      }

      public IDomainShader LoadDomainShaderFromFile(string relativePath, string entryPoint = null) {
         var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "DS", "ds_5_0");
         var shader = new DomainShader(_device, bytecode);
         return new DomainShaderBox { Shader = shader };
      }

      public IGeometryShader LoadGeometryShaderFromFile(string relativePath, string entryPoint = null) {
         var bytecode = CompileShaderBytecodeFromFileOrThrow($"{BasePath}\\{relativePath}.hlsl", entryPoint ?? "GS", "gs_5_0");
         var shader = new GeometryShader(_device, bytecode);
         return new GeometryShaderBox { Shader = shader };
      }

      private InputLayout CreateInputLayout(InputLayoutFormat inputLayoutFormat, ShaderSignature signature) {
         if (inputLayoutFormat == InputLayoutFormat.PositionNormalColorTextureInstanced) {
            return new InputLayout(_device, signature, new[] {
               new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
               new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
               new InputElement("VERTEX_COLOR", 0, Format.R8G8B8A8_UNorm, 24, 0, InputClassification.PerVertexData, 0),
               new InputElement("TEXCOORD", 0, Format.R32G32_Float, 28, 0, InputClassification.PerVertexData, 0),
               new InputElement("INSTANCE_TRANSFORM", 0, Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_TRANSFORM", 1, Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_TRANSFORM", 2, Format.R32G32B32A32_Float, 32, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_TRANSFORM", 3, Format.R32G32B32A32_Float, 48, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_METALLIC", 0, Format.R32_Float, 64, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_ROUGHNESS", 0, Format.R32_Float, 68, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_MATERIAL_RESOURCES_INDEX", 0, Format.R32_SInt, 72, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_COLOR", 0, Format.R8G8B8A8_UNorm, 76, 1, InputClassification.PerInstanceData, 1)
            });
         } else if (inputLayoutFormat == InputLayoutFormat.WaterVertex) {
            return new InputLayout(_device, signature, new[] {
               new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
               new InputElement("INSTANCE_TRANSFORM", 0, Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_TRANSFORM", 1, Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_TRANSFORM", 2, Format.R32G32B32A32_Float, 32, 1, InputClassification.PerInstanceData, 1),
               new InputElement("INSTANCE_TRANSFORM", 3, Format.R32G32B32A32_Float, 48, 1, InputClassification.PerInstanceData, 1),
            });
         }
         throw new NotSupportedException("Unsupported Input Layout: " + inputLayoutFormat);
      }

      private byte[] CompileShaderBytecodeFromFileOrThrow(string path, string entryPoint, string profile) {
         Console.WriteLine($"Compiling shader '{path}' entry '{entryPoint}' profile '{profile}'");

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

   public class Direct3DTextureFactory {
      private readonly Device _device;

      public Direct3DTextureFactory(Device device) {
         _device = device;
      }

      public (ITexture2D, IShaderResourceView) CreateSolidTexture(Color4 c) {
         var texture = new Texture2D(_device, new Texture2DDescription {
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
         _device.ImmediateContext.MapSubresource(texture, 0, 0, MapMode.WriteDiscard, MapFlags.None, out stream);
         stream.Write(c);
         _device.ImmediateContext.UnmapSubresource(texture, 0);

         var srv = new ShaderResourceView(_device, texture);
         return (new Texture2DBox { Texture = texture }, new ShaderResourceViewBox { ShaderResourceView = srv });
      }

      public (ITexture2D, IShaderResourceView) CreateSolidCubeTexture(Color4 c) {
         return CreateSolidCubeTexture(c, c, c, c, c, c);
      }

      public unsafe (ITexture2D, IShaderResourceView) CreateSolidCubeTexture(Color4 posx, Color4 negx, Color4 posy, Color4 negy, Color4 posz, Color4 negz) {
         DataBox Wrap(Color4* p) => new DataBox(new IntPtr(p), 4 * 4, 0);

         var texture = new Texture2D(_device, new Texture2DDescription {
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

         var srv = new ShaderResourceView(_device, texture);
         return (new Texture2DBox { Texture = texture }, new ShaderResourceViewBox { ShaderResourceView = srv });
      }
   }
}