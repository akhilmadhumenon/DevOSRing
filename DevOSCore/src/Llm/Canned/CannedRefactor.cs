using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevOSRing.Core.Llm.Canned;

/// <summary>
/// LLM-less refactorer used when the user has not configured an API key. Performs
/// conservative, syntax-preserving cleanups so the "demo" mode still operates on the
/// user's actual file rather than overwriting it with a fixed snippet.
/// <para>
/// C# gets a Roslyn pass that collapses nested <c>if</c>s into <c>&amp;&amp;</c> chains and
/// simplifies <c>if (x) return true; else return false;</c> patterns. Other languages get
/// regex-based whitespace tidying only.
/// </para>
/// </summary>
public static class CannedRefactor
{
    public static string Apply(string language, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return source;
        return language?.ToLowerInvariant() switch
        {
            "csharp" or "cs"   => RefactorCSharp(source),
            "typescript" or "javascript" or "ts" or "js" => TidyWhitespace(source),
            "python" or "py"   => TidyWhitespace(source),
            _                  => TidyWhitespace(source),
        };
    }

    // ---- C# (Roslyn) ----
    private static string RefactorCSharp(string source)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();
            var rewriter = new CSharpRefactorRewriter();
            var rewritten = rewriter.Visit(root);
            // NormalizeWhitespace avoids the Microsoft.CodeAnalysis.Workspaces dependency
            // while still producing reasonable formatting for rewritten nodes.
            return rewritten.NormalizeWhitespace(indentation: "    ", eol: "\n").ToFullString();
        }
        catch
        {
            return source;
        }
    }

    private sealed class CSharpRefactorRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            var visited = (BlockSyntax)base.VisitBlock(node)!;

            if (visited.Statements.Count == 1 &&
                visited.Statements[0] is IfStatementSyntax outer &&
                outer.Else is null &&
                outer.Statement is BlockSyntax outerBody &&
                outerBody.Statements.Count == 1 &&
                outerBody.Statements[0] is IfStatementSyntax inner &&
                inner.Else is null)
            {
                var combined = SyntaxFactory.BinaryExpression(
                    SyntaxKind.LogicalAndExpression,
                    Paren(outer.Condition),
                    Paren(inner.Condition));
                var collapsed = SyntaxFactory.IfStatement(combined, inner.Statement);
                return SyntaxFactory.Block(collapsed)
                    .WithTriviaFrom(visited);
            }

            if (visited.Statements.Count == 2 &&
                visited.Statements[0] is IfStatementSyntax ifReturn &&
                ifReturn.Else is null &&
                ReturnsLiteral(ifReturn.Statement, true) &&
                ReturnsLiteral(visited.Statements[1], false))
            {
                var ret = SyntaxFactory.ReturnStatement(ifReturn.Condition)
                    .WithLeadingTrivia(ifReturn.GetLeadingTrivia());
                return SyntaxFactory.Block(ret).WithTriviaFrom(visited);
            }

            return visited;
        }

        private static ExpressionSyntax Paren(ExpressionSyntax e) =>
            e is BinaryExpressionSyntax ? SyntaxFactory.ParenthesizedExpression(e) : e;

        private static bool ReturnsLiteral(StatementSyntax s, bool value)
        {
            ReturnStatementSyntax? ret = s switch
            {
                ReturnStatementSyntax r => r,
                BlockSyntax b when b.Statements.Count == 1 && b.Statements[0] is ReturnStatementSyntax rb => rb,
                _ => null,
            };
            return ret?.Expression is LiteralExpressionSyntax lit &&
                   lit.Kind() == (value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
        }
    }

    // ---- Generic whitespace tidy ----
    private static string TidyWhitespace(string source)
    {
        var normalised = source.Replace("\r\n", "\n");
        var lines = normalised.Split('\n');
        // Split("a\n") -> ["a", ""]; drop the trailing empty element so we don't
        // emit an extra blank line for an input that already ended with '\n'.
        var lastIdx = lines.Length;
        while (lastIdx > 0 && lines[lastIdx - 1].Length == 0) lastIdx--;

        var sb = new StringBuilder(source.Length);
        var blankRun = 0;
        for (int i = 0; i < lastIdx; i++)
        {
            var trimmed = lines[i].TrimEnd();
            if (trimmed.Length == 0)
            {
                blankRun++;
                if (blankRun <= 1) sb.Append('\n');
            }
            else
            {
                blankRun = 0;
                sb.Append(trimmed).Append('\n');
            }
        }
        return sb.ToString();
    }
}
