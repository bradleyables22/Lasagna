using System.Text.Json;
using System.Text.Json.Serialization;
using Lasagna.Models;
using Microsoft.AspNetCore.Http;

namespace Lasagna.Helpers;

internal static class StorageManager
{
    private const string StorageFolderName = "Lasagna";
    private const string ItemsFolderName = "items";
    private const string BundlesFolderName = "bundles";
    private const string ManifestFileName = "item.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetAppDataPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            StorageFolderName);

        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetItemsPath()
    {
        var path = Path.Combine(GetAppDataPath(), ItemsFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetBundlesPath()
    {
        var path = Path.Combine(GetAppDataPath(), BundlesFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetStoragePath(string name)
    {
        return GetItemPath(name);
    }

    public static string GetItemPath(string name)
    {
        ValidateName(name, nameof(name));

        var itemsPath = GetItemsPath();
        var requestedPath = Path.Combine(itemsPath, name);

        return Directory
            .EnumerateDirectories(itemsPath, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(
                Path.GetFileName(path),
                name,
                StringComparison.OrdinalIgnoreCase))
            ?? requestedPath;
    }

    public static ItemManifest Create(
        string name, IEnumerable<string> sourcePaths,
        string? sourceNamespace = null, IEnumerable<string>? entryPoints = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var itemPath = GetItemPath(name);
        var sources = sourcePaths.Select(Path.GetFullPath).ToArray();

        ValidateSources(sources);

        if (Directory.Exists(itemPath))
            throw new IOException($"Storage item '{name}' already exists.");

        Directory.CreateDirectory(itemPath);
        CopySources(sources, itemPath, overwrite: false);

        var manifest = BuildManifest(
            name,
            itemPath,
            sourceNamespace,
            entryPoints);

        WriteManifest(itemPath, manifest);
        return manifest;
    }

    public static ItemManifest Read(string name)
    {
        var itemPath = GetExistingItemPath(name);
        var manifestPath = Path.Combine(itemPath, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException(
                $"Storage item '{name}' is missing its manifest.");
        }

        var manifest = JsonSerializer.Deserialize<ItemManifest>(
            File.ReadAllText(manifestPath),
            JsonOptions);

        if (manifest is null)
        {
            throw new InvalidDataException(
                $"Storage item '{name}' has an empty manifest.");
        }

        ValidateManifest(manifest, name);
        return manifest;
    }

    public static IReadOnlyList<string> ReadFiles(string name)
    {
        var itemPath = GetExistingItemPath(name);

        return Directory
            .EnumerateFiles(itemPath, "*", SearchOption.AllDirectories)
            .Where(path => !PathsEqual(path, Path.Combine(itemPath, ManifestFileName)))
            .ToArray();
    }

    public static ItemManifest AddFiles(string name, IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var itemPath = GetExistingItemPath(name);
        var sources = sourcePaths.Select(Path.GetFullPath).ToArray();

        ValidateSources(sources);
        EnsureSourcesAreOutsideStorage(sources, itemPath);
        CopySources(sources, itemPath, overwrite: false);

        var existingManifest = Read(name);
        var updatedManifest = BuildManifest(
            existingManifest.Name,
            itemPath,
            existingManifest.SourceNamespace,
            existingManifest.EntryPoints);

        WriteManifest(itemPath, updatedManifest);
        return updatedManifest;
    }

    public static ItemManifest AddUploadedFiles(
        string name,
        IEnumerable<IFormFile> uploadedFiles)
    {
        ArgumentNullException.ThrowIfNull(uploadedFiles);

        var itemPath = GetExistingItemPath(name);
        var existingManifest = Read(name);
        var uploads = PrepareUploadedFiles(uploadedFiles);
        WriteUploadedFiles(itemPath, uploads);

        var updatedManifest = BuildManifest(
            existingManifest.Name,
            itemPath,
            existingManifest.SourceNamespace,
            existingManifest.EntryPoints);

        WriteManifest(itemPath, updatedManifest);
        return updatedManifest;
    }

    public static ItemManifest CreateUploadedFiles(
        string name,
        IEnumerable<IFormFile> uploadedFiles,
        string? sourceNamespace = null)
    {
        ArgumentNullException.ThrowIfNull(uploadedFiles);

        var itemPath = GetItemPath(name);
        if (Directory.Exists(itemPath))
            throw new IOException($"Storage item '{name}' already exists.");

        var uploads = PrepareUploadedFiles(uploadedFiles);
        Directory.CreateDirectory(itemPath);

        try
        {
            WriteUploadedFiles(itemPath, uploads);

            var manifest = BuildManifest(
                name,
                itemPath,
                sourceNamespace,
                entryPoints: null);
            WriteManifest(itemPath, manifest);
            return manifest;
        }
        catch
        {
            if (Directory.Exists(itemPath))
                Directory.Delete(itemPath, recursive: true);

            throw;
        }
    }

    public static ItemManifest RemoveFiles(string name, IEnumerable<string> relativePaths)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);

        var itemPath = GetExistingItemPath(name);
        var manifest = Read(name);
        var manifestPath = Path.Combine(itemPath, ManifestFileName);
        var itemRoot = Path.GetFullPath(itemPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var filesToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in relativePaths)
        {
            var normalizedPath = NormalizeManifestPath(relativePath);
            var requestedPath = Path.GetFullPath(Path.Combine(
                itemPath,
                normalizedPath.Replace('/', Path.DirectorySeparatorChar)));

            if (!requestedPath.StartsWith(itemRoot, StringComparison.OrdinalIgnoreCase) ||
                PathsEqual(requestedPath, manifestPath))
            {
                throw new ArgumentException(
                    $"The item path '{relativePath}' is not a removable file or directory.",
                    nameof(relativePaths));
            }

            if (File.Exists(requestedPath))
            {
                filesToRemove.Add(requestedPath);
                continue;
            }

            if (Directory.Exists(requestedPath))
            {
                foreach (var file in Directory.EnumerateFiles(
                             requestedPath,
                             "*",
                             SearchOption.AllDirectories))
                {
                    if (!PathsEqual(file, manifestPath))
                        filesToRemove.Add(file);
                }

                continue;
            }

            throw new FileNotFoundException(
                $"The item path '{relativePath}' was not found in item '{name}'.",
                requestedPath);
        }

        if (filesToRemove.Count == 0)
            throw new InvalidOperationException(
                $"No files were selected for removal from item '{name}'.");

        var currentFiles = ReadFiles(name);
        if (currentFiles.Count - filesToRemove.Count < 1)
        {
            throw new InvalidOperationException(
                "An item must contain at least one file. Add another file before removing the last one.");
        }

        foreach (var file in filesToRemove)
            File.Delete(file);

        RemoveEmptyDirectories(itemPath);

        var updatedManifest = BuildManifest(
            manifest.Name,
            itemPath,
            manifest.SourceNamespace,
            manifest.EntryPoints);

        WriteManifest(itemPath, updatedManifest);
        return updatedManifest;
    }

    public static ItemManifest Update(string name, IEnumerable<string> sourcePaths,string? sourceNamespace = null, IEnumerable<string>? entryPoints = null)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var itemPath = GetExistingItemPath(name);
        var sources = sourcePaths.Select(Path.GetFullPath).ToArray();

        ValidateSources(sources);
        EnsureSourcesAreOutsideStorage(sources, itemPath);
        ClearItem(itemPath);
        CopySources(sources, itemPath, overwrite: false);

        var manifest = BuildManifest(
            name,
            itemPath,
            sourceNamespace,
            entryPoints);

        WriteManifest(itemPath, manifest);
        return manifest;
    }

    public static void Delete(string name)
    {
        var itemPath = GetExistingItemPath(name);
        var referencingBundles = BundleManager.FindReferencingBundles(name);

        if (referencingBundles.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete item '{name}' because it is still used by: {string.Join(", ", referencingBundles)}. " +
                "Remove it from those bundles first.");
        }

        Directory.Delete(itemPath, recursive: true);
    }

    public static bool ItemExists(string name)
    {
        return Directory.Exists(GetItemPath(name));
    }

    public static bool StorageExists(string name)
    {
        return ItemExists(name);
    }

    public static IReadOnlyList<ItemManifest> List()
    {
        return Directory
            .EnumerateDirectories(GetItemsPath(), "*", SearchOption.TopDirectoryOnly)
            .Select(path => Read(new DirectoryInfo(path).Name))
            .ToArray();
    }

    public static IEnumerable<string> ListFolders()
    {
        return Directory.EnumerateDirectories(
            GetItemsPath(),
            "*",
            SearchOption.TopDirectoryOnly);
    }

    public static ItemManifest CreateStorage(string name)
    {
        return Create(name, Array.Empty<string>());
    }

    private static string GetExistingItemPath(string name)
    {
        var itemPath = GetItemPath(name);

        if (!Directory.Exists(itemPath))
        {
            throw new DirectoryNotFoundException(
                $"Storage item '{name}' does not exist.");
        }

        return itemPath;
    }

    private static ItemManifest BuildManifest(string name, string itemPath, string? sourceNamespace,IEnumerable<string>? entryPoints)
    {
        var files = Directory
            .EnumerateFiles(itemPath, "*", SearchOption.AllDirectories)
            .Where(path => !PathsEqual(path, Path.Combine(itemPath, ManifestFileName)))
            .Select(path => ToManifestPath(Path.GetRelativePath(itemPath, path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var entries = (entryPoints ?? Array.Empty<string>())
            .Select(NormalizeManifestPath)
            .Where(file => files.Contains(file, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ItemManifest
        {
            Name = name,
            SourceNamespace = string.IsNullOrWhiteSpace(sourceNamespace)
                ? null
                : sourceNamespace.Trim(),
            Files = files,
            EntryPoints = entries
        };
    }

    private static void WriteManifest(string itemPath, ItemManifest manifest)
    {
        var manifestPath = Path.Combine(itemPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static void ValidateManifest(ItemManifest manifest, string expectedName)
    {
        ValidateName(manifest.Name, nameof(manifest.Name));

        if (!manifest.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Storage manifest name '{manifest.Name}' does not match '{expectedName}'.");
        }

        foreach (var file in manifest.Files.Concat(manifest.EntryPoints))
        {
            NormalizeManifestPath(file);
        }
    }

    private static void ValidateSources(IEnumerable<string> sourcePaths)
    {
        foreach (var source in sourcePaths)
        {
            if (!File.Exists(source) && !Directory.Exists(source))
            {
                throw new FileNotFoundException(
                    $"The source path '{source}' was not found.",
                    source);
            }
        }
    }

    private static void CopySources(IEnumerable<string> sourcePaths, string destinationRoot, bool overwrite)
    {
        foreach (var source in sourcePaths)
        {
            if (File.Exists(source))
            {
                var destination = Path.Combine(
                    destinationRoot,
                    Path.GetFileName(source));

                File.Copy(source, destination, overwrite);
                continue;
            }

            var directoryName = new DirectoryInfo(source).Name;
            var destinationDirectory = Path.Combine(destinationRoot, directoryName);
            CopyDirectory(source, destinationDirectory, overwrite);
        }
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var destinationFile = Path.Combine(
                destination,
                Path.GetFileName(file));

            File.Copy(file, destinationFile, overwrite);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var destinationDirectory = Path.Combine(
                destination,
                Path.GetFileName(directory));

            CopyDirectory(directory, destinationDirectory, overwrite);
        }
    }

    private static void ClearItem(string itemPath)
    {
        foreach (var file in Directory.EnumerateFiles(
                     itemPath,
                     "*",
                     SearchOption.AllDirectories)
                 .Where(path => !PathsEqual(path, Path.Combine(itemPath, ManifestFileName))))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(
                     itemPath,
                     "*",
                     SearchOption.AllDirectories)
                 .OrderByDescending(path => path.Length))
        {
            Directory.Delete(directory);
        }
    }

    private static void RemoveEmptyDirectories(string itemPath)
    {
        foreach (var directory in Directory
                     .EnumerateDirectories(itemPath, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
    }

    private static void EnsureSourcesAreOutsideStorage(IEnumerable<string> sourcePaths, string storagePath)
    {
        var normalizedStoragePath = NormalizePath(storagePath);

        foreach (var source in sourcePaths)
        {
            var normalizedSource = NormalizePath(source);

            if (normalizedSource.Equals(normalizedStoragePath, StringComparison.OrdinalIgnoreCase) ||
                normalizedSource.StartsWith(
                    normalizedStoragePath + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Storage sources cannot be inside the storage item being updated.");
        }
    }

    private static string NormalizeManifestPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = ToManifestPath(path);

        if (Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException(
                "Manifest paths must be relative and remain inside the item folder.",
                nameof(path));
        }

        return normalized;
    }

    private static string NormalizeUploadPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var normalized = fileName.Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(normalized))
            normalized = Path.GetFileName(normalized);

        normalized = NormalizeManifestPath(normalized);
        if (normalized.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The item manifest cannot be uploaded.",
                nameof(fileName));
        }

        return normalized;
    }

    private static (IFormFile File, string RelativePath)[] PrepareUploadedFiles(
        IEnumerable<IFormFile> uploadedFiles)
    {
        var uploads = uploadedFiles
            .Select(file => (File: file, RelativePath: NormalizeUploadPath(file.FileName)))
            .ToArray();

        if (uploads.Length == 0)
            throw new InvalidOperationException("Choose at least one file.");

        var duplicatePath = uploads
            .GroupBy(upload => upload.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePath is not null)
        {
            throw new InvalidOperationException(
                $"The upload contains the file '{duplicatePath.Key}' more than once.");
        }

        return uploads;
    }

    private static void WriteUploadedFiles(
        string itemPath,
        IReadOnlyList<(IFormFile File, string RelativePath)> uploads)
    {
        var destinations = uploads
            .Select(upload => (
                upload.File,
                upload.RelativePath,
                FullPath: Path.Combine(
                    itemPath,
                    upload.RelativePath.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        var uploadPaths = destinations
            .Select(destination => destination.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var destination in destinations)
        {
            if (File.Exists(destination.FullPath) ||
                Directory.Exists(destination.FullPath))
            {
                throw new IOException(
                    $"The item already contains '{destination.RelativePath}'.");
            }

            var segments = destination.RelativePath.Split('/');
            var parentPath = itemPath;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                parentPath = Path.Combine(parentPath, segments[index]);
                var relativeParent = string.Join('/', segments.Take(index + 1));

                if (uploadPaths.Contains(relativeParent) || File.Exists(parentPath))
                {
                    throw new IOException(
                        $"The item cannot create the folder for '{destination.RelativePath}'.");
                }
            }
        }

        foreach (var destination in destinations)
        {
            var parent = Path.GetDirectoryName(destination.FullPath);
            if (parent is not null)
                Directory.CreateDirectory(parent);

            using var output = new FileStream(
                destination.FullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            destination.File.CopyTo(output);
        }
    }

    private static string ToManifestPath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateName(string name, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name is "." or ".." ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "Names must be valid single folder names.",
                parameterName);
        }
    }
}
