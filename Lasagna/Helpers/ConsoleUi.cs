namespace Lasagna.Helpers;

internal static class ConsoleUi
{
    public static bool SupportsInteractiveInput =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static void WriteLogo()
    {
        WriteBadge("LASAGNA", ConsoleColor.Black, ConsoleColor.Yellow);
        Console.WriteLine(" - file ingredients for .NET");
        Console.WriteLine();
    }

    public static void WriteSuccess(string message) =>
        WriteStatus(message, ConsoleColor.Green);

    public static void WriteInfo(string message) =>
        WriteStatus(message, ConsoleColor.Cyan);

    public static void WriteWarning(string message) =>
        WriteStatus(message, ConsoleColor.Yellow);

    public static void WriteError(string message) =>
        WriteStatus(message, ConsoleColor.Red);

    public static void WriteSummary(string title, params (string Label, string Value)[] rows)
    {
        Console.WriteLine(title);
        Console.WriteLine(new string('-', title.Length));

        foreach (var (label, value) in rows)
            Console.WriteLine($"{label}: {value}");

        Console.WriteLine();
    }

    public static void WriteTable(
        string title,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var materializedRows = rows
            .Select(row => row.ToArray())
            .ToArray();
        var widths = headers
            .Select((header, index) => Math.Max(
                header.Length,
                materializedRows
                    .Where(row => row.Length > index)
                    .Select(row => row[index].Length)
                    .DefaultIfEmpty(0)
                    .Max()))
            .ToArray();
        var separator = "+" + string.Join("+", widths.Select(width => new string('-', width + 2))) + "+";

        Console.WriteLine(title);
        Console.WriteLine(separator);
        WriteTableRow(headers, widths);
        Console.WriteLine(separator);

        foreach (var row in materializedRows)
            WriteTableRow(row, widths);

        Console.WriteLine(separator);
        Console.WriteLine();
    }

    public static IReadOnlyList<string> SelectFiles(
        string title,
        IReadOnlyList<string> files,
        string workingDirectory)
    {
        if (files.Count == 0)
            return [];

        if (!SupportsInteractiveInput)
        {
            WriteInfo($"Including {files.Count} discovered companion file{(files.Count == 1 ? "" : "s")}.");
            return files;
        }

        Console.WriteLine(title);

        for (var index = 0; index < files.Count; index++)
        {
            var relativePath = Path.GetRelativePath(workingDirectory, files[index]);
            Console.WriteLine($"  {index + 1}. [included] {relativePath}");
        }

        Console.Write("Enter numbers to exclude, separated by commas, or press Enter to include all: ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return files;

        var excluded = input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => int.TryParse(value, out _))
            .Select(int.Parse)
            .Where(index => index >= 1 && index <= files.Count)
            .ToHashSet();

        return files
            .Where((_, index) => !excluded.Contains(index + 1))
            .ToArray();
    }

    public static void WriteProgress(string action, int completed, int total, string path)
    {
        Console.WriteLine($"{action} {completed}/{total}: {Path.GetFileName(path)}");
    }

    private static void WriteStatus(
        string message,
        ConsoleColor color)
    {
        var previousForeground = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
        catch (IOException)
        {
            Console.WriteLine(message);
        }
        finally
        {
            try
            {
                Console.ForegroundColor = previousForeground;
            }
            catch (IOException)
            {
            }
        }
    }

    private static void WriteBadge(string text, ConsoleColor foreground, ConsoleColor background)
    {
        var previousForeground = Console.ForegroundColor;
        var previousBackground = Console.BackgroundColor;

        try
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
            Console.Write($" {text} ");
        }
        catch (IOException)
        {
            Console.Write(text);
        }
        finally
        {
            try
            {
                Console.ForegroundColor = previousForeground;
                Console.BackgroundColor = previousBackground;
            }
            catch (IOException)
            {
            }
        }
    }

    private static void WriteTableRow(IReadOnlyList<string> row, IReadOnlyList<int> widths)
    {
        var cells = widths
            .Select((width, index) => (index < row.Count ? row[index] : string.Empty).PadRight(width));
        Console.WriteLine("| " + string.Join(" | ", cells) + " |");
    }
}
