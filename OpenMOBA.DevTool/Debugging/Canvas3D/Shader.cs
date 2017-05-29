using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shade;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;

namespace OpenMOBA.DevTool.Debugging.Canvas3D {
   public interface IDepthStencilView { }
   public interface IRenderTargetView { }

   public interface IPixelShader { }
   public interface IVertexShader { }

   public interface IMesh {
      void Draw(IRenderContext renderContext);

      ITechnique DefaultRenderTechnique { get; }
      ITechnique DefaultDepthOnlyRenderTechnique { get; }
   }

   public interface IVertexBuffer { }

   public enum InputLayoutType {
      PositionColor,
      PositionColorTexture
   }

   public interface IAssetManager {
      IPixelShader LoadPixelShaderFromFile(string relativePath, string entryPoint = null);
      IVertexShader LoadVertexShaderFromFile(string relativePath, InputLayoutType inputLayoutType, string entryPoint = null);
   }

   public interface ITechnique {
      int Passes { get; set; }
      void BeginPass(IRenderContext renderContext, int pass);
   }

   public interface ITechniqueCollection {
      ITechnique DefaultPositionColor { get; }
      ITechnique DefaultPositionColorShadow { get; }
      ITechnique DefaultPositionColorTexture { get; }
      ITechnique DefaultPositionColorTextureShadow { get; }
      ITechnique DefaultPositionColorTextureDerivative { get; }
   }

   public interface IMeshPresets {
      IMesh UnitCube { get; }
      IMesh UnitCubeColor { get; }
      IMesh UnitPlaneXY { get; }
   }

   public enum DepthConfiguration {
      Uninitialized = 0,

      /// <summary>
      /// Depth test enabled, less comparison function, stencil disabled
      /// </summary>
      Enabled
   }

   public enum RasterizerConfiguration {
      Uninitialized = 0,

      /// <summary>
      /// Cull backfaces, fill polys
      /// </summary>
      Fill
   }

   public class ShaderCompilationException : Exception {
      public ShaderCompilationException(int code, string message) : base($"{message} (Code: {code})") { }
   }
}
