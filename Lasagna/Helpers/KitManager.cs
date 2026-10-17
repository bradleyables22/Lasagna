using System.Text.Json;
using System.Text.Json.Serialization;
using Lasagna.Models;
using Microsoft.AspNetCore.Http;

namespace Lasagna.Helpers;

internal static class KitManager
{
    private const string KitsFolderName = "kits";
    private const string ManifestFileName = "kit.json";

    private static readonly HashSet<string> DefaultExcludedDirectories = new(
        [".git", ".vs", "bin", "obj", "TestResults", ".idea"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DefaultExcludedFileExtensions = new(
        [".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".slnf"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetKitsPath()
    {
        var path = Path.Combine(StorageManager.GetAppDataPath(), KitsFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetKitPath(string name)
    {
        ValidateName(name, nameof(name));

        var kitsPath = GetKitsPath();
        var requestedPath = Path.Combine(kitsPath, name);

        return Directory
            .EnumerateDirectories(kitsPath, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(
                Path.GetFileName(path),
                name,
                StringComparison.OrdinalIgnoreCase))
            ?? requestedPath;
    }

    public static KitManifest Create(
        string name,
        string sourcePath,
        string? sourceNamespace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var sourceDirectory = DirectoryManager.GetDirectoryPath(sourcePath);
        var sourceFiles = EnumerateSourceFiles(sourceDirectory).ToArray();

        if (sourceFiles.Length == 0)
            throw new InvalidDataException(
                $"The directory '{sourcePath}' does not contain any files after default exclusions.");

        var kitPath = GetKitPath(name);

        if (Directory.Exists(kitPath))
            throw new IOException($"Kit '{name}' already exists.");

        Directory.CreateDirectory(kitPath);

        try
        {
            CopyFiles(sourceFiles, sourceDirectory, kitPath);

            var manifest = BuildManifest(
                name,
                kitPath,
                sourceNamespace,
                sourceFiles);

            WriteManifest(kitPath, manifest);
            return manifest;
        }
        catch
        {
            if (Directory.Exists(kitPath))
                Directory.Delete(kitPath, recursive: true);

            throw;
        }
    }

    public static KitManifest Update(
        string name,
        string sourcePath,
        string? sourceNamespace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var sourceDirectory = DirectoryManager.GetDirectoryPath(sourcePath);
        var sourceFiles = EnumerateSourceFiles(sourceDirectory).ToArray();

        if (sourceFiles.Length == 0)
            throw new InvalidDataException(
                $"The directory '{sourcePath}' does not contain any files after default exclusions.");

        var kitPath = GetExistingKitPath(name);
        ClearKit(kitPath);
        CopyFiles(sourceFiles, sourceDirectory, kitPath);

        var manifest = BuildManifest(
            name,
            kitPath,
            sourceNamespace,
            sourceFiles);

        WriteManifest(kitPath, manifest);
        return manifest;
    }

    public static KitManifest CreateUploadedFiles(
        string name,
        IEnumerable<IFormFile> uploadedFiles,
        string? sourceNamespace = null)
    {
        ArgumentNullException.ThrowIfNull(uploadedFiles);

        var normalizedUploads = uploadedFiles
            .Select(file => (File: file, RelativePath: NormalizeUploadPath(file.FileName)))
            .Where(upload => !IsExcludedPath(upload.RelativePath))
            .Where(upload => !IsExcludedFile(upload.RelativePath))
            .ToArray();

        var uploadRoot = FindCommonUploadRoot(normalizedUploads.Select(upload => upload.RelativePath));
        var uploads = normalizedUploads
            .Select(upload =>
            {
                var relativePath = uploadRoot is not null &&
                                   upload.RelativePath.StartsWith(
                                       uploadRoot + "/",
                                       StringComparison.OrdinalIgnoreCase)
                    ? upload.RelativePath[(uploadRoot.Length + 1)..]
                    : upload.RelativePath;

                if (relativePath.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "The kit manifest cannot be uploaded.",
                        nameof(uploadedFiles));
                }

                return (upload.File, RelativePath: relativePath);
            })
            .ToArray();

        if (uploads.Length == 0)
            throw new InvalidOperationException(
                "Choose a folder containing at least one non-generated file.");

        var duplicatePath = uploads
            .GroupBy(upload => upload.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePath is not null)
        {
            throw new InvalidOperationException(
                $"The folder contains the file '{duplicatePath.Key}' more than once.");
        }

        var kitPath = GetKitPath(name);

        if (Directory.Exists(kitPath))
            throw new IOException($"Kit '{name}' already exists.");

        Directory.CreateDirectory(kitPath);

        try
        {
            foreach (var upload in uploads)
            {
                var destination = Path.Combine(
                    kitPath,
                    upload.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                var parent = Path.GetDirectoryName(destination);

                if (parent is not null)
                    Directory.CreateDirectory(parent);

                using var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                upload.File.CopyTo(output);
            }

            var storedFiles = ReadFiles(name).ToArray();
            var manifest = BuildManifest(
                name,
                kitPath,
                sourceNamespace,
                storedFiles);

            WriteManifest(kitPath, manifest);
            return manifest;
        }
        catch
        {
            if (Directory.Exists(kitPath))
                Directory.Delete(kitPath, recursive: true);

            throw;
        }
    }

    public static KitManifest AddUploadedFiles(
        string name,
        IEnumerable<IFormFile> uploadedFiles)
    {
        ArgumentNullException.ThrowIfNull(uploadedFiles);

        var kitPath = GetExistingKitPath(name);
        var normalizedUploads = uploadedFiles
            .Select(file => (File: file, RelativePath: NormalizeUploadPath(file.FileName)))
            .Where(upload => !IsExcludedPath(upload.RelativePath))
            .Where(upload => !IsExcludedFile(upload.RelativePath))
            .ToArray();

        if (normalizedUploads.Length == 0)
            throw new InvalidOperationException(
                "Choose at least one non-generated file to add.");

        var duplicatePath = normalizedUploads
            .GroupBy(upload => upload.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePath is not null)
        {
            throw new InvalidOperationException(
                $"The selected files contain '{duplicatePath.Key}' more than once.");
        }

        var destinations = normalizedUploads
            .Select(upload =>
            {
                var destination = Path.Combine(
                    kitPath,
                    upload.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                return (upload.File, upload.RelativePath, Destination: destination);
            })
            .ToArray();

        var existing = destinations.FirstOrDefault(upload => File.Exists(upload.Destination));
        if (existing.Destination is not null)
        {
            throw new IOException(
                $"The kit already contains '{existing.RelativePath}'. Remove it first if you want to replace it.");
        }

        var createdFiles = new List<string>();

        try
        {
            foreach (var upload in destinations)
            {
                var parent = Path.GetDirectoryName(upload.Destination);

                if (parent is not null)
                    Directory.CreateDirectory(parent);

                using var output = new FileStream(
                    upload.Destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                upload.File.CopyTo(output);
                createdFiles.Add(upload.Destination);
            }

            var manifest = Read(name);
            var updatedManifest = BuildManifest(
                name,
                kitPath,
                manifest.SourceNamespace,
                ReadFiles(name));

            WriteManifest(kitPath, updatedManifest);
            return updatedManifest;
        }
        catch
        {
            foreach (var createdFile in createdFiles)
            {
                if (File.Exists(createdFile))
                    File.Delete(createdFile);
            }

            throw;
        }
    }

    public static KitManifest RemoveFile(string name, string relativePath)
    {
        var kitPath = GetExistingKitPath(name);
        var normalizedPath = NormalizeManifestPath(relativePath);

        if (normalizedPath.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The kit manifest cannot be removed.", nameof(relativePath));

        var filePath = Path.Combine(
            kitPath,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                $"The kit does not contain '{normalizedPath}'.",
                normalizedPath);

        File.Delete(filePath);
        DeleteEmptyParentDirectories(filePath, kitPath);

        var manifest = Read(name);
        var updatedManifest = BuildManifest(
            name,
            kitPath,
            manifest.SourceNamespace,
            ReadFiles(name));
        WriteManifest(kitPath, updatedManifest);
        return updatedManifest;
    }

    public static KitManifest Read(string name)
    {
        var kitPath = GetExistingKitPath(name);
        var manifestPath = Path.Combine(kitPath, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException(
                $"Kit '{name}' is missing its manifest.");
        }

        var manifest = JsonSerializer.Deserialize<KitManifest>(
            File.ReadAllText(manifestPath),
            JsonOptions);

        if (manifest is null)
            throw new InvalidDataException($"Kit '{name}' has an empty manifest.");

        ValidateManifest(manifest, name);
        return manifest;
    }

    public static IReadOnlyList<string> ReadFiles(string name)
    {
        var kitPath = GetExistingKitPath(name);

        return Directory
            .EnumerateFiles(kitPath, "*", SearchOption.AllDirectories)
            .Where(path => !PathsEqual(path, Path.Combine(kitPath, ManifestFileName)))
            .ToArray();
    }

    public static void Delete(string name)
    {
        var kitPath = GetExistingKitPath(name);
        Directory.Delete(kitPath, recursive: true);
    }

    public static bool Exists(string name)
    {
        return Directory.Exists(GetKitPath(name));
    }

    public static IReadOnlyList<KitManifest> List()
    {
        return Directory
            .EnumerateDirectories(
                GetKitsPath(),
                "*",
                SearchOption.TopDirectoryOnly)
            .Select(path => Read(new DirectoryInfo(path).Name))
            .ToArray();
    }

    private static KitManifest BuildManifest(
        string name,
        string kitPath,
        string? sourceNamespace,
        IEnumerable<string> sourceFiles)
    {
        var files = Directory
            .EnumerateFiles(kitPath, "*", SearchOption.AllDirectories)
            .Where(path => !PathsEqual(path, Path.Combine(kitPath, ManifestFileName)))
            .Select(path => NormalizeManifestPath(Path.GetRelativePath(kitPath, path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var fileArray = sourceFiles.ToArray();
        var detectedNamespace = string.IsNullOrWhiteSpace(sourceNamespace)
            ? NamespaceDetector.Detect(
                fileArray,
                fileArray.Length == 0
                    ? null
                    : DirectoryManager.GetProjectNamespace(fileArray[0]))
            : sourceNamespace.Trim();

        return new KitManifest
        {
            Name = name,
            SourceNamespace = string.IsNullOrWhiteSpace(detectedNamespace)
                ? null
                : detectedNamespace,
            Files = files
        };
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsExcludedFile(Path.GetFileName(file)))
                yield return file;
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (DefaultExcludedDirectories.Contains(Path.GetFileName(directory)))
                continue;

            foreach (var file in EnumerateSourceFiles(directory))
                yield return file;
        }
    }

    private static void CopyFiles(
        IEnumerable<string> sourceFiles,
        string sourceRoot,
        string destinationRoot)
    {
        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            var destination = Path.Combine(
                destinationRoot,
                relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            var parent = Path.GetDirectoryName(destination);

            if (parent is not null)
                Directory.CreateDirectory(parent);

            File.Copy(sourceFile, destination, overwrite: false);
        }
    }

    private static void ClearKit(string kitPath)
    {
        foreach (var file in Directory
                     .EnumerateFiles(kitPath, "*", SearchOption.AllDirectories)
                     .Where(path => !PathsEqual(path, Path.Combine(kitPath, ManifestFileName))))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory
                     .EnumerateDirectories(kitPath, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            Directory.Delete(directory);
        }
    }

    private static void DeleteEmptyParentDirectories(string filePath, string kitPath)
    {
        var current = Directory.GetParent(filePath);
        var root = Path.GetFullPath(kitPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        while (current is not null &&
               !string.Equals(
                   current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   root,
                   StringComparison.OrdinalIgnoreCase) &&
               !current.EnumerateFileSystemInfos().Any())
        {
            var parent = current.Parent;
            current.Delete();
            current = parent;
        }
    }

    private static void WriteManifest(string kitPath, KitManifest manifest)
    {
        var manifestPath = Path.Combine(kitPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static void ValidateManifest(KitManifest manifest, string expectedName)
    {
        ValidateName(manifest.Name, nameof(manifest.Name));

        if (!manifest.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Kit manifest name '{manifest.Name}' does not match '{expectedName}'.");

        foreach (var file in manifest.Files)
            NormalizeManifestPath(file);
    }

    private static string GetExistingKitPath(string name)
    {
        var kitPath = GetKitPath(name);

        if (!Directory.Exists(kitPath))
            throw new DirectoryNotFoundException($"Kit '{name}' does not exist.");

        return kitPath;
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
                "The kit manifest cannot be uploaded.",
                nameof(fileName));
        }

        return normalized;
    }

    private static string NormalizeManifestPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Replace('\\', '/').Trim('/');

        if (Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException(
                "Kit paths must be relative and remain inside the kit folder.",
                nameof(path));
        }

        return normalized;
    }

    private static bool IsExcludedPath(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments
            .Take(Math.Max(0, segments.Length - 1))
            .Any(DefaultExcludedDirectories.Contains);
    }

    private static bool IsExcludedFile(string relativePath)
    {
        return DefaultExcludedFileExtensions.Contains(Path.GetExtension(relativePath));
    }

    private static string? FindCommonUploadRoot(IEnumerable<string> paths)
    {
        var pathArray = paths.ToArray();
        if (pathArray.Length == 0)
            return null;

        var firstSegments = pathArray
            .Select(path => path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        if (firstSegments.Any(segments => segments.Length < 2))
            return null;

        var candidate = firstSegments[0][0];
        return firstSegments.All(segments =>
                segments[0].Equals(candidate, StringComparison.OrdinalIgnoreCase))
            ? candidate
            : null;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
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
                "Kit names must be valid single folder names.",
                parameterName);
        }
    }
}
