using SharpDX;

namespace Canvas3D.LowLevel {
   public static class HardcodedMeshPresets {
      public static VertexPositionNormalColorTexture[] ColoredCubeVertices { get; } = {
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)), // Front
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)), // BACK
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(0, 0)), // Top
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0, 1, 0), Color.White, new Vector2(1, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(0, 0)), // Bottom
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0, -1, 0), Color.White, new Vector2(1, 1)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(1, 1)), // Left
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(-1, 0, 0), Color.White, new Vector2(1, 0)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(1, 1)), // Right
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, -1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, -1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(0, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 1.0f), new Vector3(1, 0, 0), Color.White, new Vector2(0, 0)),
      };

      public static VertexPositionNormalColorTexture[] PlaneXYVertices { get; } = {
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 1)), // Back
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, 1), Color.White, new Vector2(1, 1)),

         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 1)), // Front
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(-1.0f, -1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(0, 0)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, 1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 1)),
         new VertexPositionNormalColorTexture(0.5f * new Vector3(1.0f, -1.0f, 0.0f), new Vector3(0, 0, -1), Color.White, new Vector2(1, 0))
      };
   }
}
