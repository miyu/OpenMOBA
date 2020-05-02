namespace Dargon.PlayOn.Geometry {
   // Relative to human graph paper coordinates, NOT screen coordinates.
   // So bottom left origin, x+ right, y+ up.
   // clockwise goes from origin to x+ to x+/y- to y- to origin.
   //
   // Awkward casing so CCW / CW work in autocomplete.
   public enum Clockness {
      CounterClockWise = -1,
      Neither = 0,
      ClockWise = 1
   }
}
