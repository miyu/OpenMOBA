using System;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DTechniqueCollection : ITechniqueCollection {
      private Direct3DTechniqueCollection() { }

      public ITechnique Forward { get; private set; }
      public ITechnique ForwardDepthOnly { get; private set; }
      public ITechnique ForwardWater { get; private set; }
      public ITechnique ForwardSkyFromAtmosphere { get; private set; }
      public ITechnique DeferredToGBuffer { get; private set; }
      public ITechnique DeferredFromGBuffer { get; private set; }

      internal static Direct3DTechniqueCollection Create(Direct3DShaderLoader shaderLoader) {
         var collection = new Direct3DTechniqueCollection();
         collection.Forward = new Technique {
            Passes = 1,
            VertexShader = shaderLoader.LoadVertexShaderFromFile("shaders/forward", InputLayoutFormat.PositionNormalColorTextureInstanced, "VSMain"),
            PixelShader = shaderLoader.LoadPixelShaderFromFile("shaders/forward", "PSMain"),
         };
         collection.ForwardDepthOnly = new Technique {
            Passes = 1,
            VertexShader = shaderLoader.LoadVertexShaderFromFile("shaders/forward_depth_only", InputLayoutFormat.PositionNormalColorTextureInstanced, "VSMain"),
            PixelShader = shaderLoader.LoadPixelShaderFromFile("shaders/forward_depth_only", "PSMain"),
         };
         collection.ForwardWater = new Technique {
            Passes = 1,
            VertexShader = shaderLoader.LoadVertexShaderFromFile("shaders/forward_water", InputLayoutFormat.WaterVertex, "VSMain"),
            HullShader = shaderLoader.LoadHullShaderFromFile("shaders/forward_water", "HSMain"),
            DomainShader = shaderLoader.LoadDomainShaderFromFile("shaders/forward_water", "DSMain"),
            GeometryShader = shaderLoader.LoadGeometryShaderFromFile("shaders/forward_water", "GSMain"),
            PixelShader = shaderLoader.LoadPixelShaderFromFile("shaders/forward_water", "PSMain"),
         };
         collection.ForwardSkyFromAtmosphere = new Technique {
            Passes = 1,
            VertexShader = shaderLoader.LoadVertexShaderFromFile("shaders/forward_skyfromatomsphere", InputLayoutFormat.PositionNormalColorTextureInstanced, "VSMain"),
            PixelShader = shaderLoader.LoadPixelShaderFromFile("shaders/forward_skyfromatomsphere", "PSMain"),
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
         public IVertexShader VertexShader { get; set; }
         public IHullShader HullShader { get; set; }
         public IDomainShader DomainShader { get; set; }
         public IGeometryShader GeometryShader { get; set; }
         public IPixelShader PixelShader { get; set; }

         public int Passes { get; set; }

         public void BeginPass(IDeviceContext deviceContext, int pass) {
            if (pass != 0) {
               throw new ArgumentOutOfRangeException();
            }

            deviceContext.SetVertexShader(VertexShader);
            deviceContext.SetHullShader(HullShader);
            deviceContext.SetDomainShader(DomainShader);
            deviceContext.SetGeometryShader(GeometryShader);
            deviceContext.SetPixelShader(PixelShader);
         }
      }
   }
}