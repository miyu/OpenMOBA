using System;
using Canvas3D.LowLevel.Helpers;
using SharpDX;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DPresetsStore : IPresetsStore {
      private Direct3DPresetsStore(Direct3DTextureFactory textureFactory) {
         SolidTextures = new SolidTexturesPresetCollection(textureFactory);
         SolidCubeTextures = new SolidCubeTexturesPresetCollection(textureFactory);
      }

      public IMesh UnitCube { get; private set; }
      public IMesh UnitPlaneXY { get; private set; }
      public IMesh UnitSphere { get; private set; }

      public IMesh GetPresetMesh(MeshPreset preset) {
         if (preset == MeshPreset.UnitCube) return UnitCube;
         else if (preset == MeshPreset.UnitPlaneXY) return UnitPlaneXY;
         else if (preset == MeshPreset.UnitSphere) return UnitSphere;
         else throw new NotSupportedException();
      }

      public IPresetCollection1<Color4, IShaderResourceView> SolidTextures { get; }
      public IPresetCollection1And6<Color4, IShaderResourceView> SolidCubeTextures { get; }

      private abstract class PresetCollectionBase<K, V> : IPresetCollection1<K, V> {
         private readonly CopyOnAddDictionary<K, V> store = new CopyOnAddDictionary<K, V>();
         private readonly Func<K, V> constructFunc;

         protected PresetCollectionBase() {
            constructFunc = Construct; // avoid delegate alloc
         }

         protected abstract V Construct(K key);

         public V this[K key] => store.GetOrAdd(key, constructFunc);
      }

      private class SolidTexturesPresetCollection : PresetCollectionBase<Color4, IShaderResourceView> {
         private readonly Direct3DTextureFactory textureFactory;

         public SolidTexturesPresetCollection(Direct3DTextureFactory textureFactory) {
            this.textureFactory = textureFactory;
         }

         protected override IShaderResourceView Construct(Color4 key) {
            // TODO: Texture leak.
            var (tex, srv) = textureFactory.CreateSolidTexture(key);
            return srv;
         }
      }

      private class SolidCubeTexturesPresetCollection
         : PresetCollectionBase<(Color4, Color4, Color4, Color4, Color4, Color4), IShaderResourceView>,
            IPresetCollection1And6<Color4, IShaderResourceView> {
         private readonly Direct3DTextureFactory textureFactory;

         public SolidCubeTexturesPresetCollection(Direct3DTextureFactory textureFactory) {
            this.textureFactory = textureFactory;
         }

         protected override IShaderResourceView Construct((Color4, Color4, Color4, Color4, Color4, Color4) key) {
            // TODO: Texture leak.
            var (tex, srv) = textureFactory.CreateSolidCubeTexture(key.Item1, key.Item2, key.Item3, key.Item4, key.Item5, key.Item6);
            return srv;
         }

         IShaderResourceView IPresetCollection1<Color4, IShaderResourceView>.this[Color4 key]
            => this[(key, key, key, key, key, key)];

         IShaderResourceView IPresetCollection1And6<Color4, IShaderResourceView>.this[Color4 posx, Color4 negx, Color4 posy, Color4 negy, Color4 posz, Color4 negz]
            => this[(posx, negx, posy, negy, posz, negz)];
      }

      public static Direct3DPresetsStore Create(Direct3DGraphicsDevice device, Direct3DTextureFactory textureFactory) {
         var presets = new Direct3DPresetsStore(textureFactory);

         presets.UnitCube = new Direct3DMesh<VertexPositionNormalColorTexture> {
            VertexBuffer = device.CreateVertexBuffer(
               ConvertLeftHandToRightHandTriangleList(HardcodedMeshPresets.ColoredCubeVertices)),
            Vertices = HardcodedMeshPresets.ColoredCubeVertices.Length,
            VertexBufferOffset = 0
         };

         presets.UnitPlaneXY = new Direct3DMesh<VertexPositionNormalColorTexture> {
            VertexBuffer = device.CreateVertexBuffer(
               ConvertLeftHandToRightHandTriangleList(HardcodedMeshPresets.PlaneXYVertices)),
            Vertices = HardcodedMeshPresets.PlaneXYVertices.Length,
            VertexBufferOffset = 0
         };

         presets.UnitSphere = new Direct3DMesh<VertexPositionNormalColorTexture> {
            VertexBuffer = device.CreateVertexBuffer(HardcodedMeshPresets.Sphere),
            Vertices = HardcodedMeshPresets.Sphere.Length,
            VertexBufferOffset = 0
         };

         return presets;
      }

      private static VertexPositionNormalColorTexture[] ConvertLeftHandToRightHandTriangleList(VertexPositionNormalColorTexture[] vertices) {
         var results = new VertexPositionNormalColorTexture[vertices.Length];
         for (var i = 0; i < vertices.Length; i++) {
            results[i] = new VertexPositionNormalColorTexture(
               new Vector3(
                  vertices[i].Position.X,
                  vertices[i].Position.Y,
                  -vertices[i].Position.Z),
               new Vector3(
                  vertices[i].Normal.X,
                  vertices[i].Normal.Y,
                  -vertices[i].Normal.Z),
               vertices[i].Color,
               vertices[i].UV);
         }
         return results;
      }
   }
}