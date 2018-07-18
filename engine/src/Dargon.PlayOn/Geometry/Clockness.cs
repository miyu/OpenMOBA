namespace Dargon.PlayOn.Geometry {
   // relative to screen coordinates, so top left origin, x+ right, y+ down.
   // clockwise goes from origin to x+ to x+/y+ to y+ to origin, like clockwise if
   // you were to stare at a clock on your screen
   //
   // That is, if you draw an angle between 3 points on your screen, the clockness of that
   // direction is the clockness this would return.
   public enum Clockness {
      Clockwise = -1,
      Neither = 0,
      CounterClockwise = 1
   }
}
