using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shade;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;

namespace OpenMOBA.DevTool.Debugging.Canvas3D {
   public interface IPixelShader { }
   public interface IVertexShader { }

   public interface IAssetManager {
      IPixelShader LoadPixelShaderFromFile(string relativePath, string entryPoint = null);
      IVertexShader LoadVertexShaderFromFile(string relativePath, string entryPoint = null);
   }

   public class ShaderCompilationException : Exception {
      public ShaderCompilationException(int code, string message) : base($"{message} (Code: {code})") { }
   }
}
