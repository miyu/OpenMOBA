using System;
using System.Numerics;

namespace SharpSL {
   public abstract class Shader<TIn, TOut> {
      public abstract TOut Compute(TIn input);

      public Shader<TProxyIn, TOut> ProxyIn<TProxyIn>(Func<TProxyIn, TIn> mapInput) {
         return null;
      }

      public Shader<TProxyIn, TProxyOut> ProxyIn<TProxyIn, TProxyOut>(Func<TProxyIn, TIn> mapInput, Func<TOut, TProxyOut> mapOutput) {
         return null;
      }
   }

   public class LambdaShader<TIn, TOut> : Shader<TIn, TOut> {
      private readonly Func<TIn, TOut> cb;
      public LambdaShader(Func<TIn, TOut> cb) => this.cb = cb;
      public override TOut Compute(TIn input) => cb(input);
   }

   public static class Shader {
      public static Shader<TIn, TOut> Create<TIn, TOut>(Func<TIn, TOut> func) => new LambdaShader<TIn, TOut>(func);
   }
}
