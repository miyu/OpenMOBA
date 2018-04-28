using SharpDX.Direct3D;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DMesh<TVertex> : IMesh<TVertex> where TVertex : struct {
      public IBuffer<TVertex> VertexBuffer;
      public int Vertices;
      public int VertexBufferOffset;

      public InputLayoutFormat InputLayoutFormat { get; internal set; }

      public void Draw(IDeviceContext deviceContext, int instances) {
         deviceContext.SetPrimitiveTopology(PrimitiveTopology.TriangleList);
         deviceContext.SetVertexBuffer(0, VertexBuffer);
         deviceContext.DrawInstanced(Vertices, VertexBufferOffset, instances, 0);
      }

      public IBuffer<TVertex> GetBuffer() => VertexBuffer;
   }
}