namespace OpenMOBA.Geometry {
   // relative to screen coordinates, so top left origin, x+ right, y+ down.
   // clockwise goes from origin to x+ to x+/y+ to y+ to origin, like clockwise if
   // you were to stare at a clock on your screen
   public enum Clockness {
      Clockwise = -1,
      Neither = 0,
      CounterClockwise = 1
   }
}
