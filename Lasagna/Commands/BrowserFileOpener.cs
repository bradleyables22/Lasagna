using System.Diagnostics;
using Lasagna.Helpers;

namespace Lasagna.Commands;

internal static class BrowserFileOpener
{
    internal static int Open(string name, IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            ConsoleUi.WriteInfo($"'{name}' contains no files to browse.");
            return 0;
        }

        var opened = 0;

        foreach (var file in files)
        {
            var url = new Uri(Path.GetFullPath(file)).AbsoluteUri;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            ConsoleUi.WriteProgress("Opening", ++opened, files.Count, file);
        }

        ConsoleUi.WriteSuccess(
            $"Opened {files.Count} file{(files.Count == 1 ? string.Empty : "s")} " +
            $"from '{name}'.");
        return 0;
    }
}
