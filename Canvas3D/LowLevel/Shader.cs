using System;
using System.Drawing;
using SharpDX;

namespace Canvas3D.LowLevel {
   public interface IDepthStencilView {
      Size Resolution { get; }
   }

   public interface IRenderTargetView {
      Size Resolution { get; }
   }
   public interface IShaderResourceView { }

   public interface IPixelShader { }
   public interface IVertexShader { }

   public interface IBuffer<T> where T : struct { }

   public interface ITexture2D { }

   public interface IMesh {
      VertexLayout VertexLayout { get; }

      void Draw(IRenderContext renderContext, int instances);
   }

   public interface ILowLevelAssetManager {
      IPixelShader LoadPixelShaderFromFile(string relativePath, string entryPoint = null);
      IVertexShader LoadVertexShaderFromFile(string relativePath, VertexLayout vertexLayout, string entryPoint = null);

      (ITexture2D, IShaderResourceView) CreateSolidTexture(Color4 c);
      (ITexture2D, IShaderResourceView) CreateSolidCubeTexture(Color4 c);
      (ITexture2D, IShaderResourceView) CreateSolidCubeTexture(Color4 posx, Color4 negx, Color4 posy, Color4 negy, Color4 posz, Color4 negz);
   }

   public interface ITechnique {
      int Passes { get; set; }
      void BeginPass(IRenderContext renderContext, int pass);
   }

   public interface ITechniqueCollection {
      ITechnique Forward { get; }
      ITechnique ForwardDepthOnly { get; }
      ITechnique DeferredToGBuffer { get; }
      ITechnique DeferredFromGBuffer { get; }
   }

   public interface IMeshPresets {
      IMesh UnitCube { get; }
      IMesh UnitPlaneXY { get; }
      IMesh UnitSphere { get; }
   }

   public interface ICommandList : IDisposable { }

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
