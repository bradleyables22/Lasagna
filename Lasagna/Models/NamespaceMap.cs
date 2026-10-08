using Microsoft.CodeAnalysis.CSharp;

namespace Lasagna.Models;

internal sealed record NamespaceMap(string Source, string Target)
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
