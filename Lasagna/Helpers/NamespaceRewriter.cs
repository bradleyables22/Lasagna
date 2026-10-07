using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lasagna.Helpers;

internal enum NamespaceRewriteStatus
{
    Unchanged,
    Rewritten,
    NotApplicable,
    Unsupported,
    ParseError
}

internal sealed record NamespaceRewriteResult(
    string FilePath,
    string Content,
    NamespaceRewriteStatus Status,
    IReadOnlyList<string> Changes,
    string? Message = null)
{
    public bool Changed => Status == NamespaceRewriteStatus.Rewritten;

    public bool Succeeded => Status is
        NamespaceRewriteStatus.Unchanged or
        NamespaceRewriteStatus.Rewritten or
        NamespaceRewriteStatus.NotApplicable;
}

internal static class NamespaceRewriter
{
    private static readonly Regex RazorDirectivePattern = new(
        @"(?m)^(?<indent>[ \t]*)" +
        @"@(?<directive>namespace|using|inherits|implements|inject|model)\b" +
        @"(?<spacing>[ \t]+)(?<value>[^\r\n]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RazorCodeBlockPattern = new(
        @"(?m)^[ \t]*@(?<directive>code|functions)\s*\{",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static NamespaceRewriteResult RewriteFile(
        string filePath, string sourceNamespace, string targetNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"The file '{filePath}' was not found.",
                filePath);
        }

        return RewriteContent(
            filePath,
            File.ReadAllText(filePath),
            sourceNamespace,
            targetNamespace);
    }

    public static IReadOnlyList<NamespaceRewriteResult> RewriteFiles(
        IEnumerable<string> filePaths, string sourceNamespace, string targetNamespace)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        return filePaths
            .Select(path => RewriteFile(path, sourceNamespace, targetNamespace))
            .ToArray();
    }

    public static NamespaceRewriteResult RewriteContent(
        string filePath, string content, string sourceNamespace, string targetNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        var map = NamespaceMap.Create(sourceNamespace, targetNamespace);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (IsGeneratedFile(filePath))
        {
            return NotApplicable(
                filePath,
                content,
                "Generated files are not rewritten.");
        }

        return extension switch
        {
            ".cs" => RewriteCSharp(filePath, content, map),
            ".razor" or ".cshtml" => RewriteRazor(filePath, content, map),
            ".vb" => Unsupported(filePath, content, "Visual Basic rewriting is not implemented yet."),
            ".fs" or ".fsx" => Unsupported(filePath, content, "F# rewriting is not implemented yet."),
            ".xaml" => Unsupported(filePath, content, "XAML rewriting is not implemented yet."),
            ".proto" => Unsupported(filePath, content, "Protocol Buffer namespace rewriting is not implemented yet."),
            _ => NotApplicable(filePath, content, "This file type has no namespace rewrite rule.")
        };
    }

    private static NamespaceRewriteResult RewriteCSharp(
        string filePath, string content, NamespaceMap map)
    {
        var changes = new HashSet<string>(StringComparer.Ordinal);
        var rewritten = RewriteCSharpContent(filePath, content, map, changes, out var parseError);

        if (parseError is not null)
        {
            return new NamespaceRewriteResult(
                filePath,
                content,
                NamespaceRewriteStatus.ParseError,
                Array.Empty<string>(),
                parseError);
        }

        return ResultFor(
            filePath,
            content,
            rewritten,
            changes);
    }

    private static NamespaceRewriteResult RewriteRazor(
        string filePath, string content, NamespaceMap map)
    {
        var changes = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();

        var rewritten = RazorDirectivePattern.Replace(
            content,
            match => RewriteRazorDirective(match, map, changes));

        rewritten = RewriteRazorCodeBlocks(
            rewritten,
            map,
            changes,
            warnings);

        var message = warnings.Count == 0
            ? null
            : string.Join(" ", warnings);

        var result = ResultFor(
            filePath,
            content,
            rewritten,
            changes);

        return message is null
            ? result
            : result with { Message = message };
    }

    private static string RewriteRazorDirective(
        Match match, NamespaceMap map, ISet<string> changes)
    {
        var directive = match.Groups["directive"].Value;
        var value = match.Groups["value"].Value;
        var rewrittenValue = RewriteRazorDirectiveValue(
            directive,
            value,
            map,
            changes);

        if (rewrittenValue == value)
            return match.Value;

        var valueOffset = match.Groups["value"].Index - match.Index;

        return string.Concat(
            match.Value[..valueOffset],
            rewrittenValue,
            match.Value[(valueOffset + value.Length)..]);
    }

    private static string RewriteRazorDirectiveValue(
        string directive, string value, NamespaceMap map, ISet<string> changes)
    {
        var leadingLength = value.Length - value.TrimStart().Length;
        var leading = value[..leadingLength];
        var body = value[leadingLength..];

        if (directive.Equals("using", StringComparison.OrdinalIgnoreCase) &&
            body.StartsWith("static ", StringComparison.OrdinalIgnoreCase))
        {
            var staticPrefix = body[..7];
            return leading + staticPrefix + RewriteFirstToken(
                body[7..],
                map,
                changes);
        }

        if (directive.Equals("using", StringComparison.OrdinalIgnoreCase))
        {
            var aliasSeparator = body.IndexOf('=');

            if (aliasSeparator >= 0)
            {
                return leading +
                       body[..(aliasSeparator + 1)] +
                       RewriteFirstToken(body[(aliasSeparator + 1)..], map, changes);
            }
        }

        return leading + RewriteFirstToken(body, map, changes);
    }

    private static string RewriteFirstToken(string value, NamespaceMap map, ISet<string> changes)
    {
        var leadingLength = value.Length - value.TrimStart().Length;
        var leading = value[..leadingLength];
        var body = value[leadingLength..];
        var tokenLength = 0;

        while (tokenLength < body.Length && !char.IsWhiteSpace(body[tokenLength]))
        {
            tokenLength++;
        }

        if (tokenLength == 0)
            return value;

        var token = body[..tokenLength];
        var rewrittenToken = RewriteTextToken(token, map, changes);

        return leading + rewrittenToken + body[tokenLength..];
    }

    private static string RewriteTextToken(string token, NamespaceMap map, ISet<string> changes)
    {
        var rewritten = map.Rewrite(token);

        if (rewritten is null || rewritten == token)
            return token;

        changes.Add($"{token} -> {rewritten}");
        return rewritten;
    }

    private static string RewriteRazorCodeBlocks(
        string content, NamespaceMap map, ISet<string> changes, ICollection<string> warnings)
    {
        var matches = RazorCodeBlockPattern
            .Matches(content)
            .Cast<Match>()
            .Reverse()
            .ToArray();

        if (matches.Length == 0)
            return content;

        var rewritten = new StringBuilder(content);

        foreach (var match in matches)
        {
            var openingBrace = content.IndexOf('{', match.Index, match.Length);

            if (openingBrace < 0)
                continue;

            var closingBrace = FindClosingBrace(content, openingBrace);

            if (closingBrace < 0)
            {
                warnings.Add("A Razor code block had unbalanced braces and was not rewritten.");
                continue;
            }

            var body = content[(openingBrace + 1)..closingBrace];
            var rewrittenBody = RewriteCSharpFragment(
                body,
                map,
                changes,
                out var parseError);

            if (parseError is not null)
            {
                warnings.Add("A Razor code block could not be parsed and was not rewritten.");
                continue;
            }

            if (rewrittenBody != body)
            {
                rewritten.Remove(openingBrace + 1, body.Length);
                rewritten.Insert(openingBrace + 1, rewrittenBody);
            }
        }

        return rewritten.ToString();
    }

    private static string RewriteCSharpFragment(
        string content, NamespaceMap map, ISet<string> changes, out string? parseError)
    {
        const string classPrefix = "class __LasagnaRazorCodeBlock\n{\n";
        const string classSuffix = "\n}";
        var wrapped = classPrefix + content + classSuffix;
        var rewritten = RewriteCSharpContent(
            "<razor-code-block>",
            wrapped,
            map,
            changes,
            out parseError);

        if (parseError is not null)
            return content;

        if (!rewritten.StartsWith(classPrefix, StringComparison.Ordinal) ||
            !rewritten.EndsWith(classSuffix, StringComparison.Ordinal))
        {
            parseError = "The Razor code block wrapper could not be read.";
            return content;
        }

        return rewritten[classPrefix.Length..^classSuffix.Length];
    }

    private static int FindClosingBrace(string content, int openingBrace)
    {
        var depth = 0;
        var state = ScannerState.Normal;

        for (var index = openingBrace; index < content.Length; index++)
        {
            var current = content[index];
            var next = index + 1 < content.Length ? content[index + 1] : '\0';

            if (state == ScannerState.LineComment)
            {
                if (current is '\r' or '\n')
                    state = ScannerState.Normal;

                continue;
            }

            if (state == ScannerState.BlockComment)
            {
                if (current == '*' && next == '/')
                {
                    state = ScannerState.Normal;
                    index++;
                }

                continue;
            }

            if (state == ScannerState.String)
            {
                if (current == '\\')
                    index++;
                else if (current == '"')
                    state = ScannerState.Normal;

                continue;
            }

            if (state == ScannerState.VerbatimString)
            {
                if (current == '"')
                {
                    if (next == '"')
                        index++;
                    else
                        state = ScannerState.Normal;
                }

                continue;
            }

            if (state == ScannerState.Character)
            {
                if (current == '\\')
                    index++;
                else if (current == '\'')
                    state = ScannerState.Normal;

                continue;
            }

            if (current == '/' && next == '/')
            {
                state = ScannerState.LineComment;
                index++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                state = ScannerState.BlockComment;
                index++;
                continue;
            }

            if (current == '@' && next == '"')
            {
                state = ScannerState.VerbatimString;
                index++;
                continue;
            }

            if (current == '"')
            {
                state = ScannerState.String;
                continue;
            }

            if (current == '\'')
            {
                state = ScannerState.Character;
                continue;
            }

            if (current == '{')
                depth++;
            else if (current == '}' && --depth == 0)
                return index;
        }

        return -1;
    }

    private static string RewriteCSharpContent(
        string filePath, string content, NamespaceMap map,
        ISet<string> changes, out string? parseError)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithDocumentationMode(DocumentationMode.Parse);
        var tree = CSharpSyntaxTree.ParseText(content, parseOptions, path: filePath);
        var errors = tree
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Take(3)
            .ToArray();

        if (errors.Length > 0)
        {
            parseError = string.Join(" ", errors.Select(error => error.GetMessage()));
            return content;
        }

        parseError = null;
        var root = tree.GetRoot();
        var rewriter = new CSharpNamespaceSyntaxRewriter(map, changes);
        return rewriter.Visit(root)?.ToFullString() ?? content;
    }

    private static NamespaceRewriteResult ResultFor(
        string filePath, string original, string rewritten, IEnumerable<string> changes)
    {
        var changeList = changes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(change => change, StringComparer.Ordinal)
            .ToArray();

        return new NamespaceRewriteResult(
            filePath,
            rewritten,
            changeList.Length == 0
                ? NamespaceRewriteStatus.Unchanged
                : NamespaceRewriteStatus.Rewritten,
            changeList,
            null);
    }

    private static NamespaceRewriteResult NotApplicable(
        string filePath, string content, string message)
    {
        return new NamespaceRewriteResult(
            filePath,
            content,
            NamespaceRewriteStatus.NotApplicable,
            Array.Empty<string>(),
            message);
    }

    private static NamespaceRewriteResult Unsupported(
        string filePath, string content, string message)
    {
        return new NamespaceRewriteResult(
            filePath,
            content,
            NamespaceRewriteStatus.Unsupported,
            Array.Empty<string>(),
            message);
    }

    private static bool IsGeneratedFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CSharpNamespaceSyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly NamespaceMap _map;
        private readonly ISet<string> _changes;

        public CSharpNamespaceSyntaxRewriter(NamespaceMap map, ISet<string> changes)
        {
            _map = map;
            _changes = changes;
        }

        public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var rewritten = node.WithName(RewriteName(node.Name));
            return base.VisitNamespaceDeclaration(rewritten);
        }

        public override SyntaxNode? VisitFileScopedNamespaceDeclaration(
            FileScopedNamespaceDeclarationSyntax node)
        {
            var rewritten = node.WithName(RewriteName(node.Name));
            return base.VisitFileScopedNamespaceDeclaration(rewritten);
        }

        public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
        {
            var rewritten = node.Name is null
                ? node
                : node.WithName(RewriteName(node.Name));

            return base.VisitUsingDirective(rewritten);
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            var rewritten = RewriteName(node);

            return rewritten is QualifiedNameSyntax qualified
                ? base.VisitQualifiedName(qualified)
                : rewritten;
        }

        public override SyntaxNode? VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
        {
            var rewritten = RewriteName(node);

            return rewritten is AliasQualifiedNameSyntax aliasQualified
                ? base.VisitAliasQualifiedName(aliasQualified)
                : rewritten;
        }

        private NameSyntax RewriteName(NameSyntax name)
        {
            var rewritten = _map.Rewrite(name.ToString());

            if (rewritten is null || rewritten == name.ToString())
                return name;

            _changes.Add($"{name} -> {rewritten}");

            return SyntaxFactory
                .ParseName(rewritten)
                .WithTriviaFrom(name);
        }
    }

    private sealed record NamespaceMap(string Source, string Target)
    {
        public static NamespaceMap Create(string sourceNamespace, string targetNamespace)
        {
            return new NamespaceMap(
                Normalize(sourceNamespace, nameof(sourceNamespace)),
                Normalize(targetNamespace, nameof(targetNamespace)));
        }

        public string? Rewrite(string value)
        {
            var globalPrefix = value.StartsWith(
                "global::",
                StringComparison.Ordinal)
                ? "global::"
                : string.Empty;
            var namespaceValue = value[globalPrefix.Length..];

            if (namespaceValue.Equals(Source, StringComparison.Ordinal) ||
                namespaceValue.StartsWith(
                    Source + ".",
                    StringComparison.Ordinal))
            {
                return globalPrefix +
                       Target +
                       namespaceValue[Source.Length..];
            }

            return null;
        }

        private static string Normalize(string value, string parameterName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            var normalized = value.Trim();

            if (normalized.StartsWith("global::", StringComparison.Ordinal))
                normalized = normalized[8..];

            var parts = normalized.Split('.');

            if (parts.Any(part => !SyntaxFacts.IsValidIdentifier(part)))
            {
                throw new ArgumentException(
                    $"'{value}' is not a valid C# namespace.",
                    parameterName);
            }

            return normalized;
        }
    }

    private enum ScannerState
    {
        Normal,
        LineComment,
        BlockComment,
        String,
        VerbatimString,
        Character
    }
}
