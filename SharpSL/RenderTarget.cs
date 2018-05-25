using System.Drawing;

namespace SharpSL {
   public class RenderTarget<T> {
      public T[] Data;
      public int Width;
      public int Height;

      public Size Size => new Size(Width, Height);
   }
}