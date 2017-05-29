using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;

namespace OpenMOBA.DevTool.Debugging.Canvas3D {
   public static class HardcodedMeshPresets {
      public static VertexPositionColorTexture[] ColoredCubeVertices { get; } = {
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Red, new Vector2(0, 0)), // Front
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Red, new Vector2(0, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Red, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Red, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Red, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Red, new Vector2(1, 0)),

         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Lime, new Vector2(1, 1)), // BACK
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Lime, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Lime, new Vector2(1, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Lime, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Lime, new Vector2(0, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Lime, new Vector2(0, 0)),

         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Blue, new Vector2(0, 0)), // Top
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Blue, new Vector2(0, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Blue, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Blue, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Blue, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Blue, new Vector2(1, 0)),

         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Yellow, new Vector2(0, 0)), // Bottom
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Yellow, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Yellow, new Vector2(0, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Yellow, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Yellow, new Vector2(1, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Yellow, new Vector2(1, 1)),

         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Magenta, new Vector2(1, 1)), // Left
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), Color.Magenta, new Vector2(0, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Magenta, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), Color.Magenta, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), Color.Magenta, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), Color.Magenta, new Vector2(1, 0)),

         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Cyan, new Vector2(1, 1)), // Right
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Cyan, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), Color.Cyan, new Vector2(1, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), Color.Cyan, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), Color.Cyan, new Vector2(0, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), Color.Cyan, new Vector2(0, 0)),
      };

      public static VertexPositionColorTexture[] PlaneXYVertices { get; } = {
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), Color.White, new Vector2(0, 1)), // Back
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, 0.0f), Color.White, new Vector2(1, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),

         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), Color.White, new Vector2(0, 1)), // Front
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), Color.White, new Vector2(0, 0)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), Color.White, new Vector2(1, 1)),
         new VertexPositionColorTexture(0.5f * new Vector3(1.0f, -1.0f, 0.0f), Color.White, new Vector2(1, 0))
      };
   }
}
