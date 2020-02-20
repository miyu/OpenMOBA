using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dargon.Luna.Transpiler.Deoop {
   public class Deooper {
      public Deooper(SemanticModelCache semanticModelCache) { }

      public void Plan() {
         // var transpilationFileTree = CSharpSyntaxTree.ParseText("class GeneratedClass { }");
         // var transpilation = Compilation.AddSyntaxTrees(transpilationFileTree);
      }
   }
}
