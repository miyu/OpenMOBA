using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dargon.PlayOn.Geometry;

namespace Dargon.Terragami {
   public static unsafe class NativeUtils {
      public static ApiResult LoadPrequeryAnySegmentIntersections(IntLineSegment2[] segments, out IntPtr handle) {
         var buffer = stackalloc seg2i16[segments.Length];
         
         var current = buffer;
         foreach (var b in segments) {
            current->x1 = (short)b.X1;
            current->y1 = (short)b.Y1;
            current->x2 = (short)b.X2;
            current->y2 = (short)b.Y2;
            current++;
         }

         return LoadPrequeryAnySegmentIntersections(buffer, segments.Length, out handle);
      }

      [DllImport("nativeutils", EntryPoint = "NativeApi_" + nameof(GetVersion))]
      public static extern ApiResult GetVersion(out int version);

      [DllImport("nativeutils", EntryPoint = "NativeApi_" + nameof(LoadPrequeryAnySegmentIntersections))]
      public static extern ApiResult LoadPrequeryAnySegmentIntersections(seg2i16* barriers, int numBarriers, out IntPtr handle);

      [DllImport("nativeutils", EntryPoint = "NativeApi_" + nameof(QueryAnySegmentIntersections))]
      public static extern ApiResult QueryAnySegmentIntersections(IntPtr prequeryStateHandle, seg2i16* queries, int numQueries, byte* results);

      [DllImport("nativeutils", EntryPoint = "NativeApi_" + nameof(FreePrequeryAnySegmentIntersections))]
      public static extern ApiResult FreePrequeryAnySegmentIntersections(IntPtr prequeryStateHandle);
   }

   public enum ApiResult : int {
      Success = 0,
      ErrorUnknownHandle = -100,
   }

   [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
   public struct seg2i16 {
      public short x1;
      public short y1;
      public short x2;
      public short y2;

      public seg2i16(IntLineSegment2 s) {
         x1 = (short)s.X1;
         y1 = (short)s.Y1;
         x2 = (short)s.X2;
         y2 = (short)s.Y2;
      }
   }
}
