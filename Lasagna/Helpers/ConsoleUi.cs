using Spectre.Console;

namespace Lasagna.Helpers;

internal static class ConsoleUi
{
    public static void WriteLogo()
    {
        var logo = new FigletText("LASAGNA")
            .Centered()
            .Color(Color.Red1);

        AnsiConsole.Write(logo);
        AnsiConsole.Write(new Rule("[yellow]file ingredients for .NET[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();
    }

    public static void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✔[/] {Markup.Escape(message)}");
    }

    public static void WriteInfo(string message)
    {
        AnsiConsole.MarkupLine($"[deepskyblue1]ℹ[/] {Markup.Escape(message)}");
    }

    public static void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    public static void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✖[/] {Markup.Escape(message)}");
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
