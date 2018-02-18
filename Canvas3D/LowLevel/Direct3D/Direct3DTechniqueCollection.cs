using System;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DTechniqueCollection : ITechniqueCollection {
      private Direct3DTechniqueCollection() { }

      public ITechnique Forward { get; private set; }
      public ITechnique ForwardDepthOnly { get; private set; }
      public ITechnique DeferredToGBuffer { get; private set; }
      public ITechnique DeferredFromGBuffer { get; private set; }

      internal static Direct3DTechniqueCollection Create(Direct3DShaderLoader shaderLoader) {
         var collection = new Direct3DTechniqueCollection();
         collection.Forward = new Technique {
            Passes = 1,
            PixelShader = shaderLoader.LoadPixelShaderFromFile("shaders/forward", "PSMain"),
            VertexShader = shaderLoader.LoadVertexShaderFromFile("shaders/forward", VertexLayout.PositionNormalColorTexture, "VSMain")
         };
         collection.ForwardDepthOnly = new Technique {
            Passes = 1,
            PixelShader = shaderLoader.LoadPixelShaderFromFile("shaders/forward_depth_only", "PSMain"),
            VertexShader = shaderLoader.LoadVertexShaderFromFile("shaders/forward_depth_only", VertexLayout.PositionNormalColorTexture, "VSMain")
         };
         //collection.DeferredToGBuffer = new Technique {
         //   Passes = 1,
         //   PixelShader = internalAssetLoader.LoadPixelShaderFromFile("shaders/deferred_to_gbuffer", "PSMain"),
         //   VertexShader = internalAssetLoader.LoadVertexShaderFromFile("shaders/deferred_to_gbuffer", VertexLayout.PositionNormalColorTexture, "VSMain")
         //};
         //collection.DeferredFromGBuffer = new Technique {
         //   Passes = 1,
         //   PixelShader = internalAssetLoader.LoadPixelShaderFromFile("shaders/deferred_from_gbuffer", "PSMain"),
         //   VertexShader = internalAssetLoader.LoadVertexShaderFromFile("shaders/deferred_from_gbuffer", VertexLayout.PositionNormalColorTexture, "VSMain")
         //};
         return collection;
      }

      private class Technique : ITechnique {
         public IPixelShader PixelShader { get; set; }
         public IVertexShader VertexShader { get; set; }

         public int Passes { get; set; }

         public void BeginPass(IDeviceContext deviceContext, int pass) {
            if (pass != 0) {
               throw new ArgumentOutOfRangeException();
            }

            deviceContext.SetPixelShader(PixelShader);
            deviceContext.SetVertexShader(VertexShader);
         }
      }
   }
}