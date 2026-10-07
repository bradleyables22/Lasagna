using System.Xml.Linq;

namespace Lasagna.Helpers;

internal static class DirectoryManager
{
    public static string GetWorkingDirectory()=> Directory.GetCurrentDirectory();
    public static string ResolvePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path, GetWorkingDirectory());
        return ResolveExistingSegments(fullPath);
    }

    public static string GetExistingPath(string path)
    {
        var resolvedPath = ResolvePath(path);

        if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
            throw new FileNotFoundException( $"The path '{path}' was not found in the working directory.", resolvedPath);
        
        return resolvedPath;
    }

    public static string GetFilePath(string path)
    {
        var resolvedPath = ResolvePath(path);

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"The file '{path}' was not found in the working directory.",resolvedPath);
        
        return resolvedPath;
    }

    public static string GetDirectoryPath(string path = ".")
    {
        var resolvedPath = ResolvePath(path);

        if (!Directory.Exists(resolvedPath))
            throw new DirectoryNotFoundException($"The directory '{path}' was not found in the working directory.");
        
        return resolvedPath;
    }

    public static string CreateFile(string path, string contents = "")
    {
        var resolvedPath = ResolvePath(path);

        if (File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
            throw new IOException($"The path '{path}' already exists.");

        CreateParentDirectory(resolvedPath);
        File.WriteAllText(resolvedPath, contents);
        return resolvedPath;
    }

    public static string ReadFile(string path)=> File.ReadAllText(GetFilePath(path));

	public static string UpdateFile(string path, string contents)
    {
        var resolvedPath = GetFilePath(path);
        File.WriteAllText(resolvedPath, contents);
        return resolvedPath;
    }

    public static void DeleteFile(string path)=> File.Delete(GetFilePath(path));

	public static string CreateDirectory(string path)
    {
        var resolvedPath = ResolvePath(path);
        Directory.CreateDirectory(resolvedPath);
        return resolvedPath;
    }

    public static IReadOnlyList<string> ReadDirectory(string path = ".")
    {
        return Directory.EnumerateFileSystemEntries(GetDirectoryPath(path)).ToArray();
    }

    public static string RenameDirectory(string path, string newPath)
    {
        var sourcePath = GetDirectoryPath(path);
        var destinationPath = ResolvePath(newPath);

        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
            throw new IOException($"The path '{newPath}' already exists.");
        
        CreateParentDirectory(destinationPath);
        Directory.Move(sourcePath, destinationPath);
        return destinationPath;
    }

    public static void DeleteDirectory(string path, bool recursive = false)
    {
        var resolvedPath = GetDirectoryPath(path);

        if (PathsEqual(resolvedPath, GetWorkingDirectory()))
            throw new InvalidOperationException("The working directory cannot be deleted.");
        
        Directory.Delete(resolvedPath, recursive);
    }

    public static IReadOnlyList<string> ListFiles(string searchPattern = "*")
    {
        return Directory.EnumerateFiles(GetWorkingDirectory(), searchPattern, SearchOption.TopDirectoryOnly).ToArray();
    }

    public static string? FindProjectFile()
    {
        var directory = new DirectoryInfo(GetWorkingDirectory());

        while (directory is not null)
        {
            var projects = directory
                .EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly)
                .ToArray();

            if (projects.Length == 1)
                return projects[0].FullName;

            if (projects.Length > 1)
                return null;

            directory = directory.Parent;
        }

        return null;
    }

    public static string? GetProjectNamespace()
    {
        var projectPath = FindProjectFile();

        if (projectPath is null)
            return null;

        try
        {
            var document = XDocument.Load(projectPath);
            var rootNamespace = document
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "RootNamespace")
                ?.Value
                .Trim();

            return string.IsNullOrWhiteSpace(rootNamespace)
                ? Path.GetFileNameWithoutExtension(projectPath)
                : rootNamespace;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return Path.GetFileNameWithoutExtension(projectPath);
        }
    }

    public static IReadOnlyList<string> CollectFiles(
        IEnumerable<string> paths,
        bool includeRazorCompanions = true)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var resolvedPath = GetExistingPath(path);

            if (File.Exists(resolvedPath))
            {
                AddFile(files, resolvedPath);

                if (includeRazorCompanions)
                    AddRazorCompanions(files, resolvedPath);

                continue;
            }

            foreach (var file in Directory.EnumerateFiles(
                         resolvedPath,
                         "*",
                         SearchOption.AllDirectories))
                AddFile(files, file);
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> CollectSources(
        IEnumerable<string> paths,
        bool includeRazorCompanions = true)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var resolvedPath = GetExistingPath(path);

            if (Directory.Exists(resolvedPath))
            {
                sources.Add(resolvedPath);
                continue;
            }

            AddFile(sources, resolvedPath);

            if (includeRazorCompanions)
                AddRazorCompanions(sources, resolvedPath);
        }

        return sources.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string CopyToWorkingDirectory(string sourcePath,string destinationPath,bool overwrite = false)
    {
        var source = Path.GetFullPath(sourcePath);

        if (!File.Exists(source))
            throw new FileNotFoundException($"The source file '{sourcePath}' was not found.",source);
        
        var destination = ResolvePath(destinationPath);

        if (Directory.Exists(destination))
            throw new IOException($"The destination '{destinationPath}' is a directory.");
        
        CreateParentDirectory(destination);
        File.Copy(source, destination, overwrite);
        return destination;
    }

    private static void AddRazorCompanions(ISet<string> files, string selectedFile)
    {
        var razorFile = GetRazorFile(selectedFile);

        if (razorFile is null)
            return;

        AddFileIfExists(files, razorFile);
        AddFileIfExists(files, razorFile + ".cs");
        AddFileIfExists(files, razorFile + ".css");
        AddFileIfExists(files, razorFile + ".js");
    }

    private static string? GetRazorFile(string path)
    {
        var fileName = Path.GetFileName(path);

        if (fileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            return path;

        if (fileName.EndsWith(".razor.cs", StringComparison.OrdinalIgnoreCase))
            return path[..^3];

        if (fileName.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase))
            return path[..^4];

        if (fileName.EndsWith(".razor.js", StringComparison.OrdinalIgnoreCase))
            return path[..^3];

        return null;
    }

    private static void AddFile(ISet<string> files, string path)
    {
        files.Add(Path.GetFullPath(path));
    }

    private static void AddFileIfExists(ISet<string> files, string path)
    {
        if (File.Exists(path))
            AddFile(files, path);
    }

    private static void CreateParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
        
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveExistingSegments(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);

        if (string.IsNullOrEmpty(root))
            return fullPath;

        var segments = fullPath[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var segment in segments)
        {
            if (!Directory.Exists(current))
            {
                current = Path.Combine(current, segment);
                continue;
            }

            var matchingEntry = Directory
                .EnumerateFileSystemEntries(current)
                .FirstOrDefault(entry => string.Equals(
                    Path.GetFileName(entry),
                    segment,
                    StringComparison.OrdinalIgnoreCase));

            current = matchingEntry ?? Path.Combine(current, segment);
        }

        return current;
    }
}
