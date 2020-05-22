using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Dargon.Commons;

namespace Dargon.Terragami.Tests {
   public class ColorMap {

      // ripped from https://bhaskarvk.github.io/colormap/reference/colormap.html lul
      public static readonly Color[] ViridisSamples = @"
440154ff 440558ff 450a5cff 450e60ff 451465ff 461969ff 
461d6dff 462372ff 472775ff 472c7aff 46307cff 45337dff 
433880ff 423c81ff 404184ff 3f4686ff 3d4a88ff 3c4f8aff 
3b518bff 39558bff 37598cff 365c8cff 34608cff 33638dff 
31678dff 2f6b8dff 2d6e8eff 2c718eff 2b748eff 29788eff 
287c8eff 277f8eff 25848dff 24878dff 238b8dff 218f8dff 
21918dff 22958bff 23988aff 239b89ff 249f87ff 25a186ff 
25a584ff 26a883ff 27ab82ff 29ae80ff 2eb17dff 35b479ff 
3cb875ff 42bb72ff 49be6eff 4ec16bff 55c467ff 5cc863ff 
61c960ff 6bcc5aff 72ce55ff 7cd04fff 85d349ff 8dd544ff 
97d73eff 9ed93aff a8db34ff b0dd31ff b8de30ff c3df2eff 
cbe02dff d6e22bff e1e329ff eae428ff f5e626ff fde725ff ".Split(' ', StringSplitOptions.RemoveEmptyEntries).Map(x => x.Trim())
                                                       .Map(hex => {
                                                          int r = int.Parse(hex[0..2], System.Globalization.NumberStyles.HexNumber); // jfc
                                                          int g = int.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber); // jfc
                                                          int b = int.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber); // jfc
                                                          return Color.FromArgb(r, g, b);
                                                       });
   }
}
