namespace Lasagna.Models;

internal sealed class ItemManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public string? SourceNamespace { get; set; }

    public List<string> EntryPoints { get; set; } = [];

    public List<string> Files { get; set; } = [];
}
