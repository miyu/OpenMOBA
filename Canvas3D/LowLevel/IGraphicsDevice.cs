namespace Canvas3D.LowLevel {
   public interface IGraphicsDevice {
      IImmediateRenderContext ImmediateContext { get; }
      IAssetManager AssetManager { get; }
      ITechniqueCollection TechniqueCollection { get; }
      IMeshPresets MeshPresets { get; }

      void DoEvents();
      IDeferredRenderContext CreateDeferredRenderContext();
   }
}