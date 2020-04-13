using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dargon.Luna.Transpiler.Deoop {
   public static class TranspilerInternalStatics {
      public const string SELF_TOKEN = "self";

      public static void WarningConstantAttributeInTypeButNotField(TypeSyntax type, FieldDeclarationSyntax field) {
         Warning(nameof(WarningConstantAttributeInTypeButNotField), $"Type {type} Field {field}", field);
      }

      public static void WarningConstantAttributeInFieldButNotType(FieldDeclarationSyntax field, TypeSyntax type) {
         Warning(nameof(WarningConstantAttributeInFieldButNotType), $"Type {type} Field {field}", field);
      }

      public static void Warning(string code, string reason, SyntaxNode node) {
         Console.WriteLine($"/* === WARNING {code}: {reason} === */");
         throw Diag.DumpExpressionAndSyntaxTreeThenReturnThrow(node);
      }

      public static Exception Abort(string reason, SyntaxNode node) {
         Console.WriteLine("=== " + reason + " ===");
         throw Diag.DumpExpressionAndSyntaxTreeThenReturnThrow(node);
      }

      public static Exception AbortNotSupported(string what, SyntaxNode node)
         => Abort("Not Supported: " + what, node);
   }
}
