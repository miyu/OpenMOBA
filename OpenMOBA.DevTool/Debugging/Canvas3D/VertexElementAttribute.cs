using System;
using System.Runtime.InteropServices;
using SharpDX;

namespace OpenMOBA.DevTool.Debugging.Canvas3D {
   public class VertexElementAttribute : Attribute {
      public string Name { get; }

      public VertexElementAttribute(string name) {
         Name = name;
      }
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionColor {
      [VertexElement("POSITION")]
      public Vector3 Position;

      [VertexElement("COLOR")]
      public Color Color;

      public const int Size = 16;

      public VertexPositionColor(Vector3 position, Color color) {
         Position = position;
         Color = color;
      }
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionColorTexture {
      [VertexElement("POSITION")]
      public Vector3 Position;

      [VertexElement("COLOR")]
      public Color Color;

      [VertexElement("TEXCOORD")]
      public Vector2 UV;

      public const int Size = 3 * 4 + 1 * 4 + 2 * 4;

      public VertexPositionColorTexture(Vector3 position, Color color, Vector2 uv) {
         Position = position;
         Color = color;
         UV = uv;
      }
   }
}
