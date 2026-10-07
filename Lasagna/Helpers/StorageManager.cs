namespace Lasagna.Helpers;

internal static class StorageManager
{
    private const string StorageFolderName = "Lasagna";

    public static string GetAppDataPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            StorageFolderName);

        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetStoragePath(string name)
    {
        ValidateStorageName(name);
        return Path.Combine(GetAppDataPath(), name);
    }

    public static string Create(string name, IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var storagePath = GetStoragePath(name);
        var sources = sourcePaths.Select(Path.GetFullPath).ToArray();

        ValidateSources(sources);

        if (Directory.Exists(storagePath))
            throw new IOException($"Storage '{name}' already exists.");
        
        Directory.CreateDirectory(storagePath);
        CopySources(sources, storagePath, overwrite: false);
        return storagePath;
    }

    public static IReadOnlyList<string> Read(string name)
    {
        var storagePath = GetExistingStoragePath(name);

        return Directory.EnumerateFiles(storagePath, "*", SearchOption.AllDirectories).ToArray();
    }

    public static string Update(string name, IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var storagePath = GetExistingStoragePath(name);
        var sources = sourcePaths.Select(Path.GetFullPath).ToArray();

        ValidateSources(sources);
        EnsureSourcesAreOutsideStorage(sources, storagePath);
        ClearStorage(storagePath);
        CopySources(sources, storagePath, overwrite: false);

        return storagePath;
    }

    public static void Delete(string name)
    {
        var storagePath = GetExistingStoragePath(name);
        Directory.Delete(storagePath, recursive: true);
    }

    public static string CreateStorage(string name)=> Create(name, Array.Empty<string>());

	public static bool StorageExists(string name)=> Directory.Exists(GetStoragePath(name));

	public static IEnumerable<string> ListFolders()=> Directory.EnumerateDirectories(GetAppDataPath(), "*", SearchOption.TopDirectoryOnly);

	private static string GetExistingStoragePath(string name)
    {
        var storagePath = GetStoragePath(name);

        if (!Directory.Exists(storagePath))
            throw new DirectoryNotFoundException($"Storage '{name}' does not exist.");
        
        return storagePath;
    }

    private static void ClearStorage(string storagePath)
    {
        foreach (var file in Directory.EnumerateFiles(storagePath,"*",SearchOption.AllDirectories))
            File.Delete(file);
        

        foreach (var directory in Directory.EnumerateDirectories(storagePath,"*",SearchOption.AllDirectories).OrderByDescending(path => path.Length))
            Directory.Delete(directory);
        
    }

    private static void CopySources(IEnumerable<string> sourcePaths,string destinationRoot,bool overwrite)
    {
        foreach (var source in sourcePaths.Select(Path.GetFullPath))
        {
            if (File.Exists(source))
            {
                var destination = Path.Combine(destinationRoot,Path.GetFileName(source));

                File.Copy(source, destination, overwrite);
                continue;
            }

            if (Directory.Exists(source))
            {
                var directoryName = new DirectoryInfo(source).Name;
                var destination = Path.Combine(destinationRoot, directoryName);
                CopyDirectory(source, destination, overwrite);
                continue;
            }

            throw new FileNotFoundException($"The source path '{source}' was not found.", source);
        }
    }

    private static void ValidateSources(IEnumerable<string> sourcePaths)
    {
        foreach (var source in sourcePaths)
        {
            if (!File.Exists(source) && !Directory.Exists(source))
                throw new FileNotFoundException($"The source path '{source}' was not found.", source);
        }
    }

    private static void CopyDirectory(string source,string destination,bool overwrite)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var destinationFile = Path.Combine(destination, Path.GetFileName(file));

            File.Copy(file, destinationFile, overwrite);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var destinationDirectory = Path.Combine(destination,Path.GetFileName(directory));

            CopyDirectory(directory, destinationDirectory, overwrite);
        }
    }

    private static void EnsureSourcesAreOutsideStorage(IEnumerable<string> sourcePaths,string storagePath)
    {
        var normalizedStoragePath = NormalizePath(storagePath);

        foreach (var source in sourcePaths)
        {
            var normalizedSource = NormalizePath(source);

            if (normalizedSource.Equals(normalizedStoragePath,StringComparison.OrdinalIgnoreCase) 
                || normalizedSource.StartsWith(normalizedStoragePath + Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Storage sources cannot be inside the storage directory being updated.");
            }
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void ValidateStorageName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name is "." or ".." || name.Contains(Path.DirectorySeparatorChar) 
            || name.Contains(Path.AltDirectorySeparatorChar)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Storage names must be valid single folder names.",nameof(name));
        }
    }
}
