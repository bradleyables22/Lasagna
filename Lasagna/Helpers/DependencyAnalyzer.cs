using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Lasagna.Models;

namespace Lasagna.Helpers;

internal static class DependencyAnalyzer
{
    private static readonly HashSet<string> ExcludedDirectories = new(
        [".git", ".vs", "bin", "obj", "TestResults", ".idea", "packages"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProjectExtensions = new(
        [".csproj", ".fsproj", ".vbproj"],
        StringComparer.OrdinalIgnoreCase);

    public static DependencyReport BuildReport(string scope = ".")
    {
        var rootPath = DirectoryManager.GetDirectoryPath(scope);
        var configFiles = DiscoverNuGetConfigFiles(rootPath);
        var packageSources = ReadPackageSources(configFiles);
        var projectPaths = EnumerateFiles(rootPath, ProjectExtensions)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var solutionPaths = EnumerateFiles(rootPath, [".sln", ".slnx"])
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var solutionMembership = ReadSolutionMembership(solutionPaths, rootPath);
        var solutionConfigurations = solutionPaths.ToDictionary(
            path => path,
            ReadSolutionConfigurations,
            StringComparer.OrdinalIgnoreCase);
        var descriptors = projectPaths
            .Select(path => ReadProjectDescriptor(path, rootPath))
            .ToDictionary(descriptor => descriptor.Path, StringComparer.OrdinalIgnoreCase);

        var nodes = new Dictionary<string, DependencyNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new HashSet<DependencyEdge>();
        var issues = new List<DependencyIssue>();
        var projects = new List<DependencyProject>();
        var assetsByProject = new Dictionary<string, AssetGraph>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors.Values)
        {
            var projectKey = ProjectKey(descriptor.Path);
            AddNode(nodes, new DependencyNode(
                projectKey,
                descriptor.Name,
                DependencyNodeKind.Project,
                Path: descriptor.Path));

            var assetsPath = Path.Combine(
                Path.GetDirectoryName(descriptor.Path) ?? rootPath,
                "obj",
                "project.assets.json");
            var assets = TryReadAssets(assetsPath, packageSources, issues);

            if (assets is not null)
                assetsByProject[descriptor.Path] = assets;
            else
                issues.Add(new DependencyIssue(
                    "info",
                    "No restored project.assets.json was found; direct project-file references are still shown.",
                    descriptor.Path));

            projects.Add(new DependencyProject(
                projectKey,
                descriptor.Name,
                descriptor.Path,
                descriptor.TargetFrameworks,
                descriptor.Configurations,
                solutionMembership.TryGetValue(descriptor.Path, out var solutionNames)
                    ? solutionNames
                    : [],
                assets is not null));
        }

        foreach (var descriptor in descriptors.Values)
        {
            var projectKey = ProjectKey(descriptor.Path);
            var assets = assetsByProject.GetValueOrDefault(descriptor.Path);
            var centralVersions = ReadCentralPackageVersions(descriptor.Path, rootPath);
            var packageReferences = CollectPackageReferences(descriptor, centralVersions);

            foreach (var projectReference in descriptor.ProjectReferences)
            {
                var targetPath = Path.GetFullPath(
                    projectReference.Path,
                    Path.GetDirectoryName(descriptor.Path) ?? rootPath);
                var targetKey = ProjectKey(targetPath);

                if (!descriptors.ContainsKey(targetPath))
                {
                    issues.Add(new DependencyIssue(
                        "warning",
                        $"Project reference '{projectReference.Path}' could not be found within the scan scope.",
                        descriptor.Path));
                    AddNode(nodes, new DependencyNode(
                        targetKey,
                        Path.GetFileNameWithoutExtension(targetPath),
                        DependencyNodeKind.Project,
                        Path: targetPath));
                }

                edges.Add(new DependencyEdge(
                    projectKey,
                    targetKey,
                    DependencyEdgeKind.ProjectReference,
                    Condition: projectReference.Condition,
                    Source: descriptor.Path));
            }

            foreach (var targetFramework in descriptor.TargetFrameworks)
            {
                if (string.IsNullOrWhiteSpace(targetFramework))
                    continue;

                var frameworkKey = FrameworkKey(targetFramework);
                AddNode(nodes, new DependencyNode(
                    frameworkKey,
                    targetFramework,
                    DependencyNodeKind.Framework));
                edges.Add(new DependencyEdge(
                    projectKey,
                    frameworkKey,
                    DependencyEdgeKind.FrameworkReference,
                    TargetFramework: targetFramework,
                    Source: descriptor.Path));
            }

            foreach (var packageReference in packageReferences)
            {
                var packageKey = assets?.ResolvePackageKey(packageReference.Id)
                    ?? PackageKey(packageReference.Id, packageReference.Version ?? "unresolved");
                var packageVersion = ExtractPackageVersion(packageKey)
                    ?? packageReference.Version
                    ?? "unresolved";
                var packageSource = assets?.GetPackageSource(packageReference.Id)
                    ?? FindPackageSource(packageReference.Id, packageVersion, packageSources)?.Name;

                AddNode(nodes, new DependencyNode(
                    packageKey,
                    packageReference.Id,
                    DependencyNodeKind.Package,
                    packageVersion,
                    Source: packageSource));

                edges.Add(new DependencyEdge(
                    projectKey,
                    packageKey,
                    DependencyEdgeKind.DirectPackageReference,
                    Condition: packageReference.Condition,
                    Source: packageReference.SourcePath));

                if (assets is not null && !assets.ContainsPackage(packageReference.Id))
                {
                    issues.Add(new DependencyIssue(
                        "warning",
                        $"Package '{packageReference.Id}' is declared by the project but is not present in restored assets.",
                        descriptor.Path));
                }
            }

            if (assets is not null)
            {
                foreach (var assetPackage in assets.Packages.Values)
                {
                    AddNode(nodes, new DependencyNode(
                        assetPackage.Key,
                        assetPackage.Id,
                        DependencyNodeKind.Package,
                        assetPackage.Version,
                        Source: assetPackage.Source));
                }

                foreach (var dependency in assets.Dependencies)
                {
                    edges.Add(new DependencyEdge(
                        dependency.From,
                        dependency.To,
                        DependencyEdgeKind.TransitivePackageReference,
                        dependency.TargetFramework,
                        Source: descriptor.Path));
                }
            }
            else
            {
                AddLocalFeedDependencies(
                    packageReferences,
                    packageSources,
                    nodes,
                    edges,
                    issues);
            }
        }

        AddPackageIssues(nodes, edges, issues);

        var solutions = solutionPaths
            .Select(path => new DependencySolution(
                Path.GetFileName(path),
                path,
                solutionMembership
                    .Where(entry => entry.Value.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                    .Select(entry => entry.Key)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                solutionConfigurations.GetValueOrDefault(path) ?? []))
            .ToArray();

        if (projectPaths.Length == 0)
        {
            issues.Add(new DependencyIssue(
                "warning",
                "No project files were found beneath the selected scope.",
                rootPath));
        }

        return new DependencyReport(
            rootPath,
            solutions,
            projects.OrderBy(project => project.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            nodes.Values.OrderBy(node => node.Kind).ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            edges.ToArray(),
            packageSources,
            issues);
    }

    private static ProjectDescriptor ReadProjectDescriptor(string path, string rootPath)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var propertyGroups = document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PropertyGroup", StringComparison.OrdinalIgnoreCase));
        var assemblyName = propertyGroups
            .Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "AssemblyName", StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim();
        var targetFrameworks = propertyGroups
            .Elements()
            .Where(element => string.Equals(element.Name.LocalName, "TargetFramework", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(element.Name.LocalName, "TargetFrameworks", StringComparison.OrdinalIgnoreCase))
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var configurations = propertyGroups
            .Elements()
            .Where(element => string.Equals(element.Name.LocalName, "Configurations", StringComparison.OrdinalIgnoreCase))
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(["Debug", "Release"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(configuration => configuration, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var packageReferences = ReadPackageReferences(path, document);
        var projectReferences = document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
            .Select(element => new ProjectReferenceSpec(
                element.Attribute("Include")?.Value.Trim() ?? string.Empty,
                element.Attribute("Condition")?.Value.Trim()))
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Path))
            .ToArray();

        return new ProjectDescriptor(
            Path.GetFullPath(path),
            string.IsNullOrWhiteSpace(assemblyName)
                ? Path.GetFileNameWithoutExtension(path)
                : assemblyName,
            targetFrameworks,
            configurations,
            packageReferences,
            projectReferences);
    }

    private static IReadOnlyList<PackageReferenceSpec> CollectPackageReferences(
        ProjectDescriptor descriptor,
        IReadOnlyDictionary<string, string> centralVersions)
    {
        var paths = new List<string> { descriptor.Path };
        var projectDirectory = Path.GetDirectoryName(descriptor.Path) ?? DirectoryManager.GetWorkingDirectory();

        foreach (var directory in EnumerateAncestorDirectories(projectDirectory, DirectoryManager.GetWorkingDirectory()))
        {
            paths.Add(Path.Combine(directory, "Directory.Build.props"));
            paths.Add(Path.Combine(directory, "Directory.Build.targets"));
            paths.Add(Path.Combine(directory, "Directory.Packages.props"));
        }

        return paths
            .Where(File.Exists)
            .SelectMany(path =>
            {
                try
                {
                    return ReadPackageReferences(path, XDocument.Load(path, LoadOptions.PreserveWhitespace));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
                {
                    return [];
                }
            })
            .Select(reference => reference with
            {
                Version = string.IsNullOrWhiteSpace(reference.Version)
                    ? centralVersions.GetValueOrDefault(reference.Id)
                    : reference.Version
            })
            .GroupBy(reference => $"{reference.Id}|{reference.Version}|{reference.Condition}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<PackageReferenceSpec> ReadPackageReferences(string sourcePath, XDocument document)
    {
        return document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase))
            .Select(element =>
            {
                var id = element.Attribute("Include")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(id))
                    id = element.Attribute("Update")?.Value.Trim();

                var version = element.Attribute("Version")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(version))
                {
                    version = element.Elements()
                        .FirstOrDefault(child => string.Equals(child.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(child.Name.LocalName, "VersionOverride", StringComparison.OrdinalIgnoreCase))
                        ?.Value
                        .Trim();
                }

                return new PackageReferenceSpec(
                    id ?? string.Empty,
                    version,
                    element.Attribute("Condition")?.Value.Trim(),
                    sourcePath);
            })
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Id))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ReadCentralPackageVersions(string projectPath, string rootPath)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? rootPath;

        foreach (var directory in EnumerateAncestorDirectories(projectDirectory, rootPath))
        {
            var path = Path.Combine(directory, "Directory.Packages.props");
            if (!File.Exists(path))
                continue;

            try
            {
                var document = XDocument.Load(path);
                foreach (var element in document.Descendants()
                             .Where(element => string.Equals(element.Name.LocalName, "PackageVersion", StringComparison.OrdinalIgnoreCase)))
                {
                    var id = element.Attribute("Include")?.Value.Trim() ?? element.Attribute("Update")?.Value.Trim();
                    var version = element.Attribute("Version")?.Value.Trim() ?? element.Value.Trim();

                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version))
                        versions[id] = version;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
            }
        }

        return versions;
    }

    private static AssetGraph? TryReadAssets(
        string path,
        IReadOnlyList<DependencyPackageSource> packageSources,
        ICollection<DependencyIssue> issues)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var graph = new AssetGraph();

            if (document.RootElement.TryGetProperty("targets", out var targets))
            {
                foreach (var target in targets.EnumerateObject())
                {
                    foreach (var library in target.Value.EnumerateObject())
                    {
                        var (id, version) = SplitPackageKey(library.Name);
                        if (string.IsNullOrWhiteSpace(id) ||
                            !library.Value.TryGetProperty("type", out var type) ||
                            !string.Equals(type.GetString(), "package", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var key = PackageKey(id, version);
                        var source = FindPackageSource(id, version, packageSources)?.Name;
                        graph.Packages[key] = new AssetPackage(key, id, version, source);

                        if (!library.Value.TryGetProperty("dependencies", out var dependencies))
                            continue;

                        foreach (var dependency in dependencies.EnumerateObject())
                        {
                            var dependencyVersion = dependency.Value.GetString() ?? "unresolved";
                            var dependencyKey = graph.ResolvePackageKey(dependency.Name, dependencyVersion)
                                ?? PackageKey(dependency.Name, dependencyVersion);
                            graph.Packages.TryAdd(
                                dependencyKey,
                                new AssetPackage(
                                    dependencyKey,
                                    dependency.Name,
                                    ExtractPackageVersion(dependencyKey) ?? dependencyVersion,
                                    FindPackageSource(dependency.Name, dependencyVersion, packageSources)?.Name));
                            graph.Dependencies.Add(new AssetDependency(
                                key,
                                dependencyKey,
                                target.Name));
                        }
                    }
                }
            }

            return graph;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            issues.Add(new DependencyIssue(
                "warning",
                $"The restored assets file could not be read: {exception.Message}",
                path));
            return null;
        }
    }

    private static void AddLocalFeedDependencies(
        IReadOnlyList<PackageReferenceSpec> packageReferences,
        IReadOnlyList<DependencyPackageSource> sources,
        IDictionary<string, DependencyNode> nodes,
        ISet<DependencyEdge> edges,
        ICollection<DependencyIssue> issues)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<(string Id, string Version, string ParentKey)>();

        foreach (var reference in packageReferences)
        {
            if (!string.IsNullOrWhiteSpace(reference.Version))
                pending.Enqueue((reference.Id, reference.Version, PackageKey(reference.Id, reference.Version)));
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var currentKey = PackageKey(current.Id, current.Version);
            if (!visited.Add(currentKey))
                continue;

            var package = FindPackageArchive(current.Id, current.Version, sources);
            if (package is null)
                continue;

            foreach (var dependency in ReadNuspecDependencies(package))
            {
                var dependencyVersion = dependency.Version ?? "unresolved";
                var dependencyKey = PackageKey(dependency.Id, dependencyVersion);
                AddNode(nodes, new DependencyNode(
                    dependencyKey,
                    dependency.Id,
                    DependencyNodeKind.Package,
                    dependencyVersion,
                    Source: FindPackageSource(dependency.Id, dependencyVersion, sources)?.Name));
                edges.Add(new DependencyEdge(
                    currentKey,
                    dependencyKey,
                    DependencyEdgeKind.TransitivePackageReference,
                    Source: package));
                pending.Enqueue((dependency.Id, dependencyVersion, currentKey));
            }
        }
    }

    private static IEnumerable<(string Id, string? Version)> ReadNuspecDependencies(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var nuspec = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        if (nuspec is null)
            yield break;

        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        foreach (var dependency in document.Descendants()
                     .Where(element => string.Equals(element.Name.LocalName, "dependency", StringComparison.OrdinalIgnoreCase)))
        {
            var id = dependency.Attribute("id")?.Value.Trim();
            if (!string.IsNullOrWhiteSpace(id))
                yield return (id, dependency.Attribute("version")?.Value.Trim());
        }
    }

    private static string? FindPackageArchive(
        string id,
        string version,
        IReadOnlyList<DependencyPackageSource> sources)
    {
        foreach (var source in sources.Where(source => source.IsLocalFolder && source.IsEnabled))
        {
            if (string.IsNullOrWhiteSpace(source.ResolvedPath) || !Directory.Exists(source.ResolvedPath))
                continue;

            var candidates = new[]
            {
                Path.Combine(source.ResolvedPath, id, version, $"{id}.{version}.nupkg"),
                Path.Combine(source.ResolvedPath, id.ToLowerInvariant(), version, $"{id.ToLowerInvariant()}.{version}.nupkg"),
                Path.Combine(source.ResolvedPath, $"{id}.{version}.nupkg"),
                Path.Combine(source.ResolvedPath, $"{id.ToLowerInvariant()}.{version}.nupkg")
            };

            var candidate = candidates.FirstOrDefault(File.Exists);
            if (candidate is not null)
                return candidate;

            try
            {
                candidate = Directory.EnumerateFiles(
                        source.ResolvedPath,
                        $"{id}.{version}.nupkg",
                        SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (candidate is not null)
                    return candidate;
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    private static DependencyPackageSource? FindPackageSource(
        string id,
        string version,
        IReadOnlyList<DependencyPackageSource> sources)
    {
        foreach (var source in sources.Where(source => source.IsEnabled && source.IsLocalFolder))
        {
            if (FindPackageArchive(id, version, [source]) is not null)
                return source;
        }

        return sources.FirstOrDefault(source => source.IsEnabled);
    }

    private static IReadOnlyList<DependencyPackageSource> ReadPackageSources(
        IReadOnlyList<string> configFiles)
    {
        var sources = new List<DependencyPackageSource>();
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in configFiles)
        {
            try
            {
                var document = XDocument.Load(path);
                foreach (var element in document.Descendants()
                             .Where(element => string.Equals(element.Name.LocalName, "disabledPackageSources", StringComparison.OrdinalIgnoreCase))
                             .Elements())
                {
                    if (string.Equals(element.Attribute("value")?.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                        disabled.Add(element.Attribute("key")?.Value.Trim() ?? string.Empty);
                }

                foreach (var element in document.Descendants()
                             .Where(element => string.Equals(element.Name.LocalName, "packageSources", StringComparison.OrdinalIgnoreCase))
                             .Elements()
                             .Where(element => string.Equals(element.Name.LocalName, "add", StringComparison.OrdinalIgnoreCase)))
                {
                    var name = element.Attribute("key")?.Value.Trim() ?? "Unnamed source";
                    var value = element.Attribute("value")?.Value.Trim() ?? string.Empty;
                    var resolvedPath = ResolvePackageSourcePath(value, path);
                    sources.Add(new DependencyPackageSource(
                        name,
                        value,
                        resolvedPath,
                        path,
                        resolvedPath is not null && Directory.Exists(resolvedPath),
                        !disabled.Contains(name)));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                sources.Add(new DependencyPackageSource(
                    "Unreadable configuration",
                    exception.Message,
                    null,
                    path,
                    false,
                    false));
            }
        }

        return sources
            .GroupBy(source => $"{source.Name}|{source.Value}|{source.ConfigurationPath}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> DiscoverNuGetConfigFiles(string rootPath)
    {
        var paths = new List<string>();
        var directory = new DirectoryInfo(rootPath);

        while (directory is not null)
        {
            foreach (var file in directory.EnumerateFiles("*.config", SearchOption.TopDirectoryOnly)
                         .Where(file => string.Equals(file.Name, "NuGet.Config", StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(file.FullName);
            }

            directory = directory.Parent;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userConfig = Path.Combine(appData, "NuGet", "NuGet.Config");
        if (File.Exists(userConfig))
            paths.Add(userConfig);

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ResolvePackageSourcePath(string value, string configPath)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
            return Path.GetFullPath(uri.LocalPath);

        if (Path.IsPathRooted(value))
            return Path.GetFullPath(value);

        var directory = Path.GetDirectoryName(configPath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var candidate = Path.GetFullPath(value, directory);
        return Directory.Exists(candidate) ? candidate : null;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadSolutionMembership(
        IReadOnlyList<string> solutionPaths,
        string rootPath)
    {
        var membership = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var solutionPath in solutionPaths)
        {
            try
            {
                var extension = Path.GetExtension(solutionPath);
                var projectPaths = extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                    ? ReadSlnxProjects(solutionPath)
                    : ReadSlnProjects(solutionPath);

                foreach (var projectPath in projectPaths)
                {
                    var resolved = Path.GetFullPath(projectPath, Path.GetDirectoryName(solutionPath) ?? rootPath);
                    if (!IsWithinRoot(resolved, rootPath))
                        continue;

                    if (!membership.TryGetValue(resolved, out var names))
                    {
                        names = [];
                        membership[resolved] = names;
                    }

                    names.Add(Path.GetFileName(solutionPath));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
            }
        }

        return membership.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ReadSlnProjects(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var firstQuote = line.IndexOf('"');
            var secondQuote = firstQuote < 0 ? -1 : line.IndexOf('"', firstQuote + 1);
            if (firstQuote < 0 || secondQuote <= firstQuote)
                continue;

            var value = line[(firstQuote + 1)..secondQuote];
            if (ProjectExtensions.Contains(Path.GetExtension(value)))
                yield return value;
        }
    }

    private static IEnumerable<string> ReadSlnxProjects(string path)
    {
        var document = XDocument.Load(path);
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (ProjectExtensions.Contains(Path.GetExtension(attribute.Value)))
                yield return attribute.Value;
        }
    }

    private static IReadOnlyList<string> ReadSolutionConfigurations(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var document = XDocument.Load(path);
                return document.Descendants()
                    .Attributes()
                    .Where(attribute => string.Equals(attribute.Name.LocalName, "Configuration", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(attribute.Name.LocalName, "Configurations", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(attribute => attribute.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Select(configuration => configuration.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(configuration => configuration, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            var configurations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inConfigurationSection = false;

            foreach (var line in File.ReadLines(path))
            {
                if (line.Contains("SolutionConfigurationPlatforms", StringComparison.OrdinalIgnoreCase))
                {
                    inConfigurationSection = true;
                    continue;
                }

                if (inConfigurationSection && line.Contains("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
                {
                    inConfigurationSection = false;
                    continue;
                }

                if (!inConfigurationSection || !line.Contains('=', StringComparison.Ordinal))
                    continue;

                var value = line.Split('=', 2)[0].Trim();
                var configuration = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
                if (!string.IsNullOrWhiteSpace(configuration))
                    configurations.Add(configuration);
            }

            return configurations
                .OrderBy(configuration => configuration, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateFiles(string rootPath, IEnumerable<string> extensions)
    {
        var extensionSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        return EnumerateFilesRecursive(rootPath, extensionSet);
    }

    private static IEnumerable<string> EnumerateFilesRecursive(string path, IReadOnlySet<string> extensions)
    {
        var directory = new DirectoryInfo(path);
        if (IsReparsePoint(directory))
            yield break;

        IEnumerable<FileInfo> files;
        IEnumerable<DirectoryInfo> directories;

        try
        {
            files = directory.EnumerateFiles();
            directories = directory.EnumerateDirectories();
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            if (extensions.Contains(file.Extension))
                yield return file.FullName;
        }

        foreach (var child in directories)
        {
            if (ExcludedDirectories.Contains(child.Name) || IsReparsePoint(child))
                continue;

            foreach (var file in EnumerateFilesRecursive(child.FullName, extensions))
                yield return file;
        }
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string start, string stopAt)
    {
        var stop = Path.GetFullPath(stopAt).TrimEnd(Path.DirectorySeparatorChar);
        var directory = new DirectoryInfo(start);

        while (directory is not null)
        {
            yield return directory.FullName;
            if (string.Equals(
                    directory.FullName.TrimEnd(Path.DirectorySeparatorChar),
                    stop,
                    StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            directory = directory.Parent;
        }
    }

    private static void AddPackageIssues(
        IReadOnlyDictionary<string, DependencyNode> nodes,
        IEnumerable<DependencyEdge> edges,
        ICollection<DependencyIssue> issues)
    {
        foreach (var group in nodes.Values
                     .Where(node => node.Kind == DependencyNodeKind.Package)
                     .GroupBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
        {
            var versions = group
                .Select(node => node.Version)
                .Where(version => !string.IsNullOrWhiteSpace(version) && !string.Equals(version, "unresolved", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (versions.Length > 1)
            {
                issues.Add(new DependencyIssue(
                    "warning",
                    $"Package '{group.Key}' appears with multiple versions: {string.Join(", ", versions)}."));
            }
        }

        var adjacency = edges
            .Where(edge => edge.Kind is DependencyEdgeKind.TransitivePackageReference or DependencyEdgeKind.ProjectReference)
            .GroupBy(edge => edge.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.To).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes.Keys)
            DetectCycle(node, adjacency, visiting, visited, issues);
    }

    private static void DetectCycle(
        string node,
        IReadOnlyDictionary<string, string[]> adjacency,
        ISet<string> visiting,
        ISet<string> visited,
        ICollection<DependencyIssue> issues)
    {
        if (visited.Contains(node))
            return;

        if (!visiting.Add(node))
        {
            issues.Add(new DependencyIssue("warning", $"A dependency cycle includes '{node}'."));
            return;
        }

        if (adjacency.TryGetValue(node, out var children))
        {
            foreach (var child in children)
                DetectCycle(child, adjacency, visiting, visited, issues);
        }

        visiting.Remove(node);
        visited.Add(node);
    }

    private static void AddNode(IDictionary<string, DependencyNode> nodes, DependencyNode node)
    {
        if (!nodes.ContainsKey(node.Key))
            nodes[node.Key] = node;
    }

    private static string ProjectKey(string path) =>
        $"project:{Path.GetFullPath(path)}";

    private static string FrameworkKey(string framework) =>
        $"framework:{framework}";

    private static string PackageKey(string id, string version) =>
        $"package:{id.ToLowerInvariant()}@{version}";

    private static (string Id, string Version) SplitPackageKey(string key)
    {
        var separator = key.LastIndexOf('/');
        return separator <= 0 || separator == key.Length - 1
            ? (key, "unresolved")
            : (key[..separator], key[(separator + 1)..]);
    }

    private static string? ExtractPackageVersion(string key)
    {
        var separator = key.LastIndexOf('@');
        return separator < 0 || separator == key.Length - 1
            ? null
            : key[(separator + 1)..];
    }

    private static bool IsWithinRoot(string path, string rootPath)
    {
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidate, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(FileSystemInfo entry) =>
        entry.Attributes.HasFlag(FileAttributes.ReparsePoint);

    private sealed record ProjectDescriptor(
        string Path,
        string Name,
        IReadOnlyList<string> TargetFrameworks,
        IReadOnlyList<string> Configurations,
        IReadOnlyList<PackageReferenceSpec> PackageReferences,
        IReadOnlyList<ProjectReferenceSpec> ProjectReferences);

    private sealed record PackageReferenceSpec(
        string Id,
        string? Version,
        string? Condition,
        string SourcePath);

    private sealed record ProjectReferenceSpec(
        string Path,
        string? Condition);

    private sealed record AssetPackage(
        string Key,
        string Id,
        string Version,
        string? Source);

    private sealed record AssetDependency(
        string From,
        string To,
        string TargetFramework);

    private sealed class AssetGraph
    {
        internal Dictionary<string, AssetPackage> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<AssetDependency> Dependencies { get; } = [];

        internal bool ContainsPackage(string id) =>
            Packages.Values.Any(package => string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase));

        internal string? ResolvePackageKey(string id, string? requestedVersion = null)
        {
            var matches = Packages.Values
                .Where(package => string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(requestedVersion))
            {
                var exact = matches.FirstOrDefault(package =>
                    string.Equals(package.Version, requestedVersion, StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                    return exact.Key;
            }

            return matches[0].Key;
        }

        internal string? GetPackageSource(string id) =>
            Packages.Values
                .Where(package => string.Equals(package.Id, id, StringComparison.OrdinalIgnoreCase))
                .Select(package => package.Source)
                .FirstOrDefault(source => !string.IsNullOrWhiteSpace(source));
    }
}
