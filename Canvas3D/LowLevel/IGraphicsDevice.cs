namespace Canvas3D.LowLevel {
   public interface IGraphicsDevice {
      IImmediateRenderContext ImmediateContext { get; }
      ILowLevelAssetManager LowLevelAssetManager { get; }
      ITechniqueCollection TechniqueCollection { get; }
      IMeshPresets MeshPresets { get; }

      void DoEvents();
      IDeferredRenderContext CreateDeferredRenderContext();
   }
}