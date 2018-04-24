using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX.DirectInput;

namespace Canvas3D {
   public class InputSomethingOSDJFH {
      private bool[] lastKeyStates = new bool[256];
      private bool[] currentKeyStates = new bool[256];
      private bool[] lastMouseStates = new bool[3];
      private bool[] currentMouseStates = new bool[3];
      private Point currentMousePosition;
      private Point lastMousePosition;
      private Point deltaMousePosition;
      private readonly Form form;

      public InputSomethingOSDJFH(Form form) {
         this.form = form;

         form.KeyDown += (s, e) => HandleKeyDownUp(e, true);
         form.KeyUp += (s, e) => HandleKeyDownUp(e, false);

         form.MouseDown += (s, e) => HandleMouseDownUp(e, true);
         form.MouseUp += (s, e) => HandleMouseDownUp(e, false);
         form.MouseMove += (s, e) => {
            currentMousePosition = e.Location;
            deltaMousePosition = new Point(
               currentMousePosition.X - lastMousePosition.X,
               currentMousePosition.Y - lastMousePosition.Y);
         };
      }

      public int X => currentMousePosition.X;
      public int Y => currentMousePosition.Y;
      public int DeltaX => deltaMousePosition.X;
      public int DeltaY => deltaMousePosition.Y;

      public void HandlePreWindowingEvents() {
         for (var i = 0; i < currentKeyStates.Length; i++) {
            lastKeyStates[i] = currentKeyStates[i];
         }
         for (var i = 0; i < currentMouseStates.Length; i++) {
            lastMouseStates[i] = currentMouseStates[i];
         }
      }

      public void HandleFrameEnter() {
         deltaMousePosition = new Point(
            currentMousePosition.X - lastMousePosition.X,
            currentMousePosition.Y - lastMousePosition.Y);
         lastMousePosition = currentMousePosition;
      }

      private void HandleKeyDownUp(KeyEventArgs e, bool value) {
         if ((int)e.KeyCode >= currentKeyStates.Length) return;
         currentKeyStates[(int)e.KeyCode] = value;
      }

      private void HandleMouseDownUp(MouseEventArgs e, bool value) {
         if (TryConvertMouseButtonToIndex(e.Button, out var index)) {
            currentMouseStates[index] = value;
         }
      }

      private static bool TryConvertMouseButtonToIndex(MouseButtons mb, out int index) {
         if (mb == MouseButtons.Left) {
            index = 0;
            return true;
         } else if (mb == MouseButtons.Middle) {
            index = 1;
            return true;
         }
         else if (mb == MouseButtons.Right) {
            index = 2;
            return true;
         }
         index = -1;
         return false;
      }

      public bool IsKeyJustDown(Keys key) {
         if ((int)key >= currentKeyStates.Length) throw new ArgumentException();
         return !lastKeyStates[(int)key] && currentKeyStates[(int)key];
      }

      public bool IsKeyJustUp(Keys key) {
         if ((int)key >= currentKeyStates.Length) throw new ArgumentException();
         return lastKeyStates[(int)key] && !currentKeyStates[(int)key];
      }

      public bool IsKeyDown(Keys key) {
         if ((int)key >= currentKeyStates.Length) throw new ArgumentException();
         return currentKeyStates[(int)key];
      }

      public bool IsKeyUp(Keys key) {
         if ((int)key >= currentKeyStates.Length) throw new ArgumentException();
         return !currentKeyStates[(int)key];
      }

      public bool IsMouseJustDown(MouseButtons button) {
         if (!TryConvertMouseButtonToIndex(button, out var index)) throw new ArgumentException();
         return !lastMouseStates[index] && currentMouseStates[index];
      }

      public bool IsMouseJustUp(MouseButtons button) {
         if (!TryConvertMouseButtonToIndex(button, out var index)) throw new ArgumentException();
         return lastMouseStates[index] && !currentMouseStates[index];
      }

      public bool IsMouseDown(MouseButtons button) {
         if (!TryConvertMouseButtonToIndex(button, out var index)) throw new ArgumentException();
         return currentMouseStates[index];
      }

      public bool IsMouseUp(MouseButtons button) {
         if (!TryConvertMouseButtonToIndex(button, out var index)) throw new ArgumentException();
         return !currentMouseStates[index];
      }
   }
}
