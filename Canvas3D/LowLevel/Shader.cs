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

   public interface IBufferUpdater<T> : IDisposable where T : struct {
      void Write(T val);
      void Write(ref T val);
      void Write(T[] vals);
      void Write(T[] vals, int offset, int count);
      void UpdateAndReset();
      void UpdateAndClose();
      void UpdateCloseAndDispose();
      void Reopen();
   }

   public interface ITexture2D { }

   public interface IMesh {
      VertexLayout VertexLayout { get; }

      void Draw(IDeviceContext deviceContext, int instances);
   }

   public interface IMesh<TVertex> : IMesh where TVertex : struct {
      IBuffer<TVertex> GetBuffer();
   }

   public interface ITechnique {
      int Passes { get; set; }
      void BeginPass(IDeviceContext deviceContext, int pass);
   }

   public interface ITechniqueCollection {
      ITechnique Forward { get; }
      ITechnique ForwardDepthOnly { get; }
      ITechnique DeferredToGBuffer { get; }
      ITechnique DeferredFromGBuffer { get; }
   }

   public interface IPresetCollection1<K, V> {
      V this[K key] { get; }
   }

   public interface IPresetCollection1And6<K, V> : IPresetCollection1<K, V> {
      V this[Color4 posx, Color4 negx, Color4 posy, Color4 negy, Color4 posz, Color4 negz] { get; }
   }

   public interface IPresetsStore {
      IMesh UnitCube { get; }
      IMesh UnitPlaneXY { get; }
      IMesh UnitSphere { get; }

      IMesh GetPresetMesh(MeshPreset preset);

      IPresetCollection1<Color4, IShaderResourceView> SolidTextures { get; }
      IPresetCollection1And6<Color4, IShaderResourceView> SolidCubeTextures { get; }
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
