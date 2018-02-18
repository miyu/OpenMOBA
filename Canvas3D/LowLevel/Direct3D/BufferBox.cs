using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class BufferBox<T> : IBuffer<T> where T : struct {
      public Buffer Buffer;
      public int Count;
      public int Stride;
   }
}