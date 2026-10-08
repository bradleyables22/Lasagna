using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lasagna.Helpers;

internal static class NamespaceDetector
{
    private static readonly Regex RazorNamespacePattern = new(
        @"(?m)^\s*@namespace\s+(?<namespace>[A-Za-z_][A-Za-z0-9_.]*)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? Detect(
        IEnumerable<string> sourceFiles, string? projectNamespace = null)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);

        var declaredNamespaces = sourceFiles
            .SelectMany(GetDeclaredNamespaces)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(projectNamespace) 
            && (declaredNamespaces.Length == 0 || 
            declaredNamespaces.All(namespaceValue => IsNamespace(namespaceValue, projectNamespace))))
        {
            return projectNamespace.Trim();
        }

        return FindCommonNamespace(declaredNamespaces);
    }

    private static IEnumerable<string> GetDeclaredNamespaces(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var extension = Path.GetExtension(filePath);

        if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath));

            return tree
                .GetRoot()
                .DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(namespaceDeclaration => namespaceDeclaration.Name.ToString());
        }

        if (extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            return RazorNamespacePattern
                .Matches(File.ReadAllText(filePath))
                .Select(match => match.Groups["namespace"].Value.Trim());
        }

        return [];
    }

    private static string? FindCommonNamespace(IEnumerable<string> namespaces)
    {
        var parts = namespaces
            .Where(namespaceValue => !string.IsNullOrWhiteSpace(namespaceValue))
            .Select(namespaceValue => namespaceValue.Split('.'))
            .ToArray();

        if (parts.Length == 0)
            return null;

        var commonLength = 0;

        while (commonLength < parts[0].Length &&
               parts.All(namespaceParts =>
                   commonLength < namespaceParts.Length &&
                   namespaceParts[commonLength].Equals(
                       parts[0][commonLength],
                       StringComparison.Ordinal)))
        {
            commonLength++;
        }

        return commonLength == 0
            ? null
            : string.Join('.', parts[0].Take(commonLength));
    }

    private static bool IsNamespace(string value, string rootNamespace)
    {
        return value.Equals(rootNamespace, StringComparison.Ordinal) ||
               value.StartsWith(rootNamespace + ".", StringComparison.Ordinal);
    }
}
