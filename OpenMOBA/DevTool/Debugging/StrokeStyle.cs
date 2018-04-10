using System.Drawing;

namespace OpenMOBA.DevTool.Debugging {
   public class StrokeStyle {
      public static float[] Dash5 = new[] { 5.0f, 5.0f };
      public static StrokeStyle LimeHairLineSolid = new StrokeStyle(Color.Lime);
      public static StrokeStyle LimeHairLineDashed5 = new StrokeStyle(Color.Lime, 1.0f, Dash5);
      public static StrokeStyle LimeThick5Solid = new StrokeStyle(Color.Lime, 5.0f);
      public static StrokeStyle LimeThick25Solid = new StrokeStyle(Color.Lime, 25.0f);
      public static StrokeStyle RedHairLineSolid = new StrokeStyle(Color.Red);
      public static StrokeStyle RedHairLineDashed5 = new StrokeStyle(Color.Red, 1.0f, Dash5);
      public static StrokeStyle RedThick5Solid = new StrokeStyle(Color.Red, 5.0f);
      public static StrokeStyle RedThick10Solid = new StrokeStyle(Color.Red, 10.0f);
      public static StrokeStyle RedThick25Solid = new StrokeStyle(Color.Red, 25.0f);
      public static StrokeStyle DarkRedHairLineSolid = new StrokeStyle(Color.DarkRed);
      public static StrokeStyle DarkRedHairLineDashed5 = new StrokeStyle(Color.DarkRed, 1.0f, Dash5);
      public static StrokeStyle DarkRedThick5Solid = new StrokeStyle(Color.DarkRed, 5.0f);
      public static StrokeStyle DarkRedThick25Solid = new StrokeStyle(Color.DarkRed, 25.0f);
      public static StrokeStyle CyanHairLineSolid = new StrokeStyle(Color.Cyan);
      public static StrokeStyle CyanHairLineDashed5 = new StrokeStyle(Color.Cyan, 1.0f, Dash5);
      public static StrokeStyle CyanThick3Solid = new StrokeStyle(Color.Cyan, 3.0f);
      public static StrokeStyle CyanThick5Solid = new StrokeStyle(Color.Cyan, 5.0f);
      public static StrokeStyle CyanThick25Solid = new StrokeStyle(Color.Cyan, 25.0f);
      public static StrokeStyle BlackHairLineSolid = new StrokeStyle(Color.Black);
      public static StrokeStyle BlackHairLineDashed5 = new StrokeStyle(Color.Black, 1.0f, Dash5);
      public static StrokeStyle BlackThick3Solid = new StrokeStyle(Color.Black, 3.0f);
      public static StrokeStyle BlackThick5Solid = new StrokeStyle(Color.Black, 5.0f);
      public static StrokeStyle BlackThick25Solid = new StrokeStyle(Color.Black, 25.0f);
      public static StrokeStyle OrangeHairLineSolid = new StrokeStyle(Color.Orange, 1.0f);
      public static StrokeStyle OrangeThick35Solid = new StrokeStyle(Color.Orange, 35.0f);
      public static StrokeStyle MagentaHairLineSolid = new StrokeStyle(Color.Magenta);
      public static StrokeStyle None = new StrokeStyle(Color.Transparent, 0);

      public StrokeStyle(Color? color = null, double thickness = 1.0, float[] dashPattern = null) {
         Color = color ?? Color.Black;
         Thickness = thickness;
         DashPattern = dashPattern;
      }

      public Color Color;
      public double Thickness;
      public float[] DashPattern;
      public bool DisableStrokePerspective;
   }
}