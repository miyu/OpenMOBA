using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using Device = SharpDX.Direct3D11.Device;

namespace Canvas3D.LowLevel.Direct3D {
   public class Direct3DGraphicsFacade : IGraphicsFacade {
      public IGraphicsDevice Device { get; private set; }
      public ITechniqueCollection Techniques { get; private set; }
      public IPresetsStore Presets { get; private set; }

      public static Direct3DGraphicsFacade Create(RenderForm form) {
         // Low-level device
         var device = Direct3DGraphicsDevice.Create(form);

         // Load support defaults
         var shaderLoader = new Direct3DShaderLoader(device.InternalD3DDevice);
         var techniqueCollection = Direct3DTechniqueCollection.Create(shaderLoader);
         var textureFactory = new Direct3DTextureFactory(device.InternalD3DDevice);
         var presetsStore = Direct3DPresetsStore.Create(device, textureFactory);

         return new Direct3DGraphicsFacade {
            Device = device,
            Techniques = techniqueCollection,
            Presets = presetsStore
         };
      }
   }
}
