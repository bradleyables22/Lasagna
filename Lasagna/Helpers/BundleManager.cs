using System.Text.Json;
using System.Text.Json.Serialization;
using Lasagna.Models;

namespace Lasagna.Helpers;

internal static class BundleManager
{
    private const string BundleManifestFileName = "bundle.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetBundlePath(string name)
    {
        ValidateName(name);

        var bundlesPath = StorageManager.GetBundlesPath();
        var requestedPath = Path.Combine(bundlesPath, name);

        return Directory
            .EnumerateDirectories(bundlesPath, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(
                Path.GetFileName(path),
                name,
                StringComparison.OrdinalIgnoreCase))
            ?? requestedPath;
    }

    public static BundleManifest Create(string name, IEnumerable<BundleItemReference> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var bundlePath = GetBundlePath(name);
        var references = NormalizeReferences(items);

        if (Directory.Exists(bundlePath))
            throw new IOException($"Bundle '{name}' already exists.");

        Directory.CreateDirectory(bundlePath);

        var manifest = new BundleManifest
        {
            Name = name,
            Items = references
        };

        WriteManifest(bundlePath, manifest);
        return manifest;
    }

    public static BundleManifest Read(string name)
    {
        var bundlePath = GetExistingBundlePath(name);
        var manifestPath = Path.Combine(bundlePath, BundleManifestFileName);

        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException(
                $"Bundle '{name}' is missing its manifest.");
        }

        var manifest = JsonSerializer.Deserialize<BundleManifest>(
            File.ReadAllText(manifestPath),
            JsonOptions);

        if (manifest is null)
            throw new InvalidDataException($"Bundle '{name}' has an empty manifest.");

        ValidateManifest(manifest, name);
        return manifest;
    }

    public static BundleManifest Update(string name, IEnumerable<BundleItemReference> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var bundlePath = GetExistingBundlePath(name);
        var manifest = new BundleManifest
        {
            Name = name,
            Items = NormalizeReferences(items)
        };

        WriteManifest(bundlePath, manifest);
        return manifest;
    }

    public static void Delete(string name)
    {
        var bundlePath = GetExistingBundlePath(name);
        Directory.Delete(bundlePath, recursive: true);
    }

    public static bool Exists(string name)
    {
        return Directory.Exists(GetBundlePath(name));
    }

    public static IReadOnlyList<BundleManifest> List()
    {
        return Directory
            .EnumerateDirectories(
                StorageManager.GetBundlesPath(),
                "*",
                SearchOption.TopDirectoryOnly)
            .Select(path => Read(new DirectoryInfo(path).Name))
            .ToArray();
    }

    public static BundleManifest AddItem(
        string bundleName, string itemName, string destination = ".",
        bool rewriteNamespace = true, string? targetNamespace = null)
    {
        var manifest = Read(bundleName);
        var items = manifest.Items.ToList();

        items.Add(new BundleItemReference
        {
            ItemName = itemName,
            Destination = destination,
            RewriteNamespace = rewriteNamespace,
            TargetNamespace = targetNamespace
        });

        return Update(bundleName, items);
    }

    public static BundleManifest RemoveItem(string bundleName, string itemName, string? destination = null)
    {
        var manifest = Read(bundleName);
        var items = manifest.Items
            .Where(item =>
                !item.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                (destination is not null &&
                 !item.Destination.Equals(
                     NormalizeDestination(destination),
                     StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (items.Count == manifest.Items.Count)
            throw new InvalidOperationException($"Bundle '{bundleName}' does not contain item '{itemName}'.");

        return Update(bundleName, items);
    }

    private static string GetExistingBundlePath(string name)
    {
        var bundlePath = GetBundlePath(name);

        if (!Directory.Exists(bundlePath))
            throw new DirectoryNotFoundException($"Bundle '{name}' does not exist.");

        return bundlePath;
    }

    private static List<BundleItemReference> NormalizeReferences(IEnumerable<BundleItemReference> items)
    {
        var references = items
            .Select(item =>
            {
                ArgumentNullException.ThrowIfNull(item);

                if (!StorageManager.ItemExists(item.ItemName))
                {
                    throw new DirectoryNotFoundException(
                        $"Storage item '{item.ItemName}' does not exist.");
                }

                return new BundleItemReference
                {
                    ItemName = item.ItemName,
                    Destination = NormalizeDestination(item.Destination),
                    RewriteNamespace = item.RewriteNamespace,
                    TargetNamespace = string.IsNullOrWhiteSpace(item.TargetNamespace)
                        ? null
                        : item.TargetNamespace.Trim()
                };
            })
            .ToList();

        var duplicateReferences = references
            .GroupBy(
                item => $"{item.ItemName}|{item.Destination}",
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateReferences is not null)
            throw new InvalidOperationException(
                $"Bundle contains duplicate item reference '{duplicateReferences.Key}'.");

        return references;
    }

    private static void WriteManifest(string bundlePath, BundleManifest manifest)
    {
        var manifestPath = Path.Combine(bundlePath, BundleManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static void ValidateManifest(BundleManifest manifest, string expectedName)
    {
        ValidateName(manifest.Name);

        if (!manifest.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Bundle manifest name '{manifest.Name}' does not match '{expectedName}'.");

        NormalizeReferences(manifest.Items);
    }

    private static string NormalizeDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination) || destination == ".")
            return ".";

        var normalized = destination
            .Replace('\\', '/')
            .Trim('/');

        if (Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException("Bundle destinations must be relative paths.", nameof(destination));
        }

        return normalized;
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name is "." or ".." ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "Bundle names must be valid single folder names.",
                nameof(name));
        }
    }
}
