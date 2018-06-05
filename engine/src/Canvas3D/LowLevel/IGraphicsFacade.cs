using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas3D.LowLevel {
   public interface IGraphicsFacade {
      IGraphicsDevice Device { get; }
      ITechniqueCollection Techniques { get; }
      IPresetsStore Presets { get; }

      IMesh<TVertex> CreateMesh<TVertex>(TVertex[] data) where TVertex : struct;
   }
}
