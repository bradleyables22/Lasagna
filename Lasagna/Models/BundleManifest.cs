namespace Lasagna.Models;

internal sealed class BundleManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public List<BundleItemReference> Items { get; set; } = [];
}

internal sealed class BundleItemReference
{
    public string ItemName { get; set; } = string.Empty;

    public string Destination { get; set; } = ".";

    public bool RewriteNamespace { get; set; } = true;

    public string? TargetNamespace { get; set; }
}
