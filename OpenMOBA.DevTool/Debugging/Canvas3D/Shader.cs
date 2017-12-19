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
   }

   public interface IVertexBuffer { }

   public enum InputLayoutType {
      PositionColor,
      PositionColorTexture,
      PositionNormalColorTexture
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
      ITechnique Forward { get; }
      ITechnique ForwardDepthOnly { get; }
      ITechnique Derivative { get; }
   }

   public interface IMeshPresets {
      IMesh UnitCube { get; }
      IMesh UnitPlaneXY { get; }
   }

   public enum DepthConfiguration {
      Uninitialized = 0,

      Disabled,

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
      FillFront,

      /// <summary>
      /// Cull frontfaces, fill polys
      /// </summary>
      FillBack,
   }

   public class ShaderCompilationException : Exception {
      public ShaderCompilationException(int code, string message) : base($"{message} (Code: {code})") { }
   }
}
