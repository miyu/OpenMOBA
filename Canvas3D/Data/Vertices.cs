using System;
using System.Runtime.InteropServices;
using SharpDX;

namespace Canvas3D {
   public enum VertexLayout {
      PositionNormalColorTexture
   }

   public class VertexElementAttribute : Attribute {
      public string Name { get; }

      public VertexElementAttribute(string name) {
         Name = name;
      }
   }

   [StructLayout(LayoutKind.Sequential, Pack = 1)]
   public struct VertexPositionNormalColorTexture {
      public const VertexLayout Layout = VertexLayout.PositionNormalColorTexture;

      [VertexElement("POSITION")]
      public Vector3 Position;

      [VertexElement("NORMAL")]
      public Vector3 Normal;

      [VertexElement("COLOR")]
      public Color Color;

      [VertexElement("TEXCOORD")]
      public Vector2 UV;

      public const int Size = 3 * 4 + 3 * 4 + 1 * 4 + 2 * 4;

      public VertexPositionNormalColorTexture(Vector3 position, Vector3 normal, Color color, Vector2 uv) {
         Position = position;
         Normal = normal;
         Color = color;
         UV = uv;
      }

      public override string ToString() => $"{Position} {Normal} {Color} {UV}";
   }
}
