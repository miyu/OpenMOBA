using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class VertexShaderBox : IVertexShader {
      public VertexShader Shader;
      public InputLayout InputLayout;
   }
   public class HullShaderBox : IHullShader {
      public HullShader Shader;
   }
   public class DomainShaderBox : IDomainShader {
      public DomainShader Shader;
   }
   public class GeometryShaderBox : IGeometryShader {
      public GeometryShader Shader;
   }
}