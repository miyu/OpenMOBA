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
   public struct Direct3DVertexPositionColor {
      [VertexElement("POSITION")]
      public Vector3 Position;

      [VertexElement("COLOR")]
      public Color Color;

      public const int Size = 16;

      public Direct3DVertexPositionColor(Vector3 position, Color color) {
         Position = position;
         Color = color;
      }
   }
}
