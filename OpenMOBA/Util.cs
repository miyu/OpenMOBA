using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenMOBA {
   // from dargon/commons
   public static unsafe class Util {
      public static T[] Generate<T>(int count, Func<T> generator) {
         return Generate(count, i => generator());
      }

      /// <summary>
      /// Creates an array using the given function N times.
      /// The function takes a parameter i, from 0 to count, and returns T.
      /// </summary>
      public static T[] Generate<T>(int count, Func<int, T> generator) {
         if (count < 0)
            throw new ArgumentOutOfRangeException("count < 0");
         if (generator == null)
            throw new ArgumentNullException("generator");

         T[] result = new T[count];
         for (int i = 0; i < count; i++)
            result[i] = generator(i);
         return result;
      }

      /// <summary>
      /// Creates an array using the given function N times.
      /// The function takes a parameter a from 0 to countA and a parameter b, from 0 to countB, and returns T.
      /// </summary>
      public static T[] Generate<T>(int countA, int countB, Func<int, int, T> generator) {
         if (countA < 0)
            throw new ArgumentOutOfRangeException("countA < 0");
         if (countB < 0)
            throw new ArgumentOutOfRangeException("countB < 0");
         if (generator == null)
            throw new ArgumentNullException("generator");

         T[] result = new T[countA * countB];
         for (int a = 0; a < countA; a++)
            for (int b = 0; b < countB; b++)
               result[a * countB + b] = generator(a, b);
         return result;
      }

      /// <summary>
      /// Creates an array using the given function N times.
      /// </summary>
      public static T[] Generate<T>(int countA, int countB, int countC, Func<int, int, int, T> generator) {
         if (countA < 0)
            throw new ArgumentOutOfRangeException("countA < 0");
         if (countB < 0)
            throw new ArgumentOutOfRangeException("countB < 0");
         if (countC < 0)
            throw new ArgumentOutOfRangeException("countC < 0");
         if (generator == null)
            throw new ArgumentNullException("generator");

         T[] result = new T[countA * countB * countC];
         int i = 0;
         for (int a = 0; a < countA; a++)
            for (int b = 0; b < countB; b++)
               for (int c = 0; c < countC; c++)
                  result[i++] = generator(a, b, c);
         return result;
      }

      /// <summary>
      /// Generates a given output.  Returns null if we are done after this loop.
      /// Throws GeneratorFinishedException if done.
      /// </summary>
      public delegate bool GeneratorDelegate<T>(int i, out T output);

      public static T[] Generate<T>(GeneratorDelegate<T> generator) where T : class {
         List<T> result = new List<T>();
         bool done = false;
         int i = 0;
         try {
            while (!done) {
               T output = null;
               done = generator(i++, out output);
               result.Add(output);
            }
         } catch (GeneratorExitException) {
         } catch (Exception e) {
            throw e;
         }
         return result.ToArray();
      }

      public static T[] Concat<T>(params object[] args) {
         var result = new List<T>();
         foreach (var element in args) {
            if (element is T)
               result.Add((T)element);
            else {
               foreach (var subElement in (IEnumerable<T>)element)
                  result.Add(subElement);
            }
         }
         return result.ToArray();
      }

      /// <summary>
      /// Creates a variable of the given value repeated [count] times.
      /// Note that this just copies reference if we have a Object.
      /// </summary>
      public static T[] Repeat<T>(int count, T t) => Generate(count, i => t);

      public static bool ByteArraysEqual(byte[] param1, byte[] param2) {
         return ByteArraysEqual(param1, 0, param1.Length, param2, 0, param2.Length);
      }

      public static bool ByteArraysEqual(byte[] a, int aOffset, byte[] b, int bOffset, int length) {
         return ByteArraysEqual(a, aOffset, length, b, bOffset, length);
      }

      public static bool ByteArraysEqual(byte[] a, int aOffset, int aLength, byte[] b, int bOffset, int bLength) {
         if (aOffset + aLength > a.Length) {
            throw new IndexOutOfRangeException("aOffset + aLength > a.Length");
         } else if (bOffset + bLength > b.Length) {
            throw new IndexOutOfRangeException("bOffset + bLength > b.Length");
         } else if (aOffset < 0) {
            throw new IndexOutOfRangeException("aOffset < 0");
         } else if (bOffset < 0) {
            throw new IndexOutOfRangeException("bOffset < 0");
         } else if (aLength < 0) {
            throw new IndexOutOfRangeException("aLength < 0");
         } else if (bLength < 0) {
            throw new IndexOutOfRangeException("bLength < 0");
         }

         if (aLength != bLength) {
            return false;
         } else if (a == b && aOffset == bOffset && aLength == bLength) {
            return true;
         }

         fixed (byte* pABase = a)
         fixed (byte* pBBase = b) {
            byte* pACurrent = pABase + aOffset, pBCurrent = pBBase + bOffset;
            return BuffersEqual(pACurrent, pBCurrent, aLength);
         }
      }

      public static unsafe bool BuffersEqual(byte* pACurrent, byte* pBCurrent, int aLength) {
         var length = aLength;
         int longCount = length / 8;
         for (var i = 0; i < longCount; i++) {
            if (*(ulong*)pACurrent != *(ulong*)pBCurrent) {
               return false;
            }
            pACurrent += 8;
            pBCurrent += 8;
         }
         if ((length & 4) != 0) {
            if (*(uint*)pACurrent != *(uint*)pBCurrent) {
               return false;
            }
            pACurrent += 4;
            pBCurrent += 4;
         }
         if ((length & 2) != 0) {
            if (*(ushort*)pACurrent != *(ushort*)pBCurrent) {
               return false;
            }
            pACurrent += 2;
            pBCurrent += 2;
         }
         if ((length & 1) != 0) {
            if (*pACurrent != *pBCurrent) {
               return false;
            }
            pACurrent += 1;
            pBCurrent += 1;
         }
         return true;
      }

      /// <summary>
      /// http://stackoverflow.com/questions/221925/creating-a-byte-array-from-a-stream
      /// </summary>
      /// <param name="input"></param>
      /// <returns></returns>
      public static byte[] ReadToEnd(Stream input) {
         byte[] buffer = new byte[16 * 1024];
         using (MemoryStream ms = new MemoryStream()) {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
               ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
         }
      }
      

      /// <summary>
      /// Gets the attribute of Enum value
      /// </summary>
      /// <typeparam name="TAttribute"></typeparam>
      /// <param name="enumValue"></param>
      /// <returns></returns>
      public static TAttribute GetAttributeOrNull<TAttribute>(this Enum enumValue)
         where TAttribute : Attribute {
         var enumType = enumValue.GetType();
         var memberInfo = enumType.GetTypeInfo().DeclaredMembers.First(member => member.Name.Equals(enumValue.ToString()));
         var attributes = memberInfo.GetCustomAttributes(typeof(TAttribute), false);
         return (TAttribute)attributes.FirstOrDefault();
      }

      public static TAttribute GetAttributeOrNull<TAttribute>(this object instance)
         where TAttribute : Attribute {
         var instanceType = instance as Type ?? instance.GetType();
         return GetAttributeOrNull<TAttribute>(instanceType);
      }

      public static TAttribute GetAttributeOrNull<TAttribute>(this Type type)
         where TAttribute : Attribute {
         var typeInfo = type.GetTypeInfo();
         return GetAttributeOrNull<TAttribute>(typeInfo);
      }


      public static TAttribute GetAttributeOrNull<TAttribute>(this TypeInfo typeInfo)
         where TAttribute : Attribute {
         var attributes = typeInfo.GetCustomAttributes(typeof(TAttribute), false);
         return (TAttribute)attributes.FirstOrDefault();
      }

      public static bool IsThrown<TException>(Action action) where TException : Exception {
         try {
            action();
            return false;
         } catch (TException) {
            return true;
         }
      }

      public static long GetUnixTimeMilliseconds() {
         return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
      }
   }
   public class BadInputException : Exception {
      public BadInputException() : base() { }
      public BadInputException(string message) : base(message) { }
   }
   public class InvalidStateException : Exception {
      public InvalidStateException() : base() { }
      public InvalidStateException(string message) : base(message) { }
   }
   public class GeneratorExitException : Exception {
      public GeneratorExitException() : base("The Generator is unable to produce more results.  Perhaps, there is nothing left to produce?") { }
   }
}
