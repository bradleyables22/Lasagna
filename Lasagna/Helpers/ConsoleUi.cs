using Spectre.Console;

namespace Lasagna.Helpers;

internal static class ConsoleUi
{
    public static bool SupportsInteractivePrompts =>
        AnsiConsole.Profile.Capabilities.Interactive &&
        AnsiConsole.Profile.Capabilities.Unicode;

    public static void WriteLogo()
    {
        AnsiConsole.MarkupLine("[yellow]LASAGNA[/] - [grey]file ingredients for .NET[/]");
        AnsiConsole.WriteLine();
    }

    public static void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]OK[/] {Markup.Escape(message)}");
    }

    public static void WriteInfo(string message)
    {
        AnsiConsole.MarkupLine($"[deepskyblue1]INFO[/] {Markup.Escape(message)}");
    }

    public static void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]WARN[/] {Markup.Escape(message)}");
    }

    public static void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]ERROR[/] {Markup.Escape(message)}");
    }

    public static void WriteSummary(string title, params (string Label, string Value)[] rows)
    {
        var table = new Table
        {
            Border = TableBorder.Rounded
        };
        table.BorderColor(Color.Grey);

        table.AddColumn("[yellow]Detail[/]");
        table.AddColumn("[white]Value[/]");

        foreach (var (label, value) in rows)
            table.AddRow(Markup.Escape(label), Markup.Escape(value));

        AnsiConsole.Write(new Panel(table)
            .Header($"[green]{Markup.Escape(title)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green));
    }

}
