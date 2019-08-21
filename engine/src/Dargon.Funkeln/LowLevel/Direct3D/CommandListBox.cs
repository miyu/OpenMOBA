using System;
using System.Threading;
using SharpDX;
using SharpDX.Direct3D11;

namespace Canvas3D.LowLevel.Direct3D {
   public class CommandListBox : ICommandList {
      private static int instanceCount = 0;
      private int disposed = 0;
      public CommandList CommandList;

      public CommandListBox() {
         if (Interlocked.Increment(ref instanceCount) > 512) {
            Console.Error.WriteLine("Warning: Make sure CommandListBox is being disposed! (>512 unfreed instances)");
         }
         //Console.WriteLine(instanceCount);
      }

      ~CommandListBox() {
         if (CommandList != null) {
            Console.Error.WriteLine("Warning: Finalizer called on undisposed command list. This will leak memory (SharpDX lacks a finalizer on CommandList).");
         }
         Dispose();
      }

      public void Dispose() {
         if (Interlocked.CompareExchange(ref disposed, 1, 0) == 0) {
            Utilities.Dispose<CommandList>(ref CommandList);
            Interlocked.Decrement(ref instanceCount);
         }
      }
   }
}