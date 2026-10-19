using Lasagna.Models;

namespace Lasagna.Helpers;

internal static class DependencyReportCache
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, CacheEntry> Entries = new(StringComparer.OrdinalIgnoreCase);

    private const int MaximumEntries = 8;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    internal static DependencyReport BuildFresh(string scope)
    {
        var rootPath = DirectoryManager.GetDirectoryPath(scope);
        var report = DependencyAnalyzer.BuildReport(rootPath);

        lock (Gate)
        {
            Entries[rootPath] = new CacheEntry(report, DateTimeOffset.UtcNow);
            TrimExpiredAndExcessEntries();
        }

        return report;
    }

    internal static DependencyReport GetOrBuild(string scope)
    {
        var rootPath = DirectoryManager.GetDirectoryPath(scope);

        lock (Gate)
        {
            if (Entries.TryGetValue(rootPath, out var entry) &&
                DateTimeOffset.UtcNow - entry.CreatedAt <= Lifetime)
            {
                return entry.Report;
            }

            Entries.Remove(rootPath);
        }

        return BuildFresh(rootPath);
    }

    private static void TrimExpiredAndExcessEntries()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in Entries
                     .Where(entry => now - entry.Value.CreatedAt > Lifetime)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            Entries.Remove(entry);
        }

        if (Entries.Count <= MaximumEntries)
            return;

        foreach (var entry in Entries
                     .OrderBy(entry => entry.Value.CreatedAt)
                     .Take(Entries.Count - MaximumEntries)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            Entries.Remove(entry);
        }
    }

    private sealed record CacheEntry(DependencyReport Report, DateTimeOffset CreatedAt);
}
