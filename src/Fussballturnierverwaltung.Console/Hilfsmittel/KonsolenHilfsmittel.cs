using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Hilfsmittel;

public static class KonsolenHilfsmittel
{
    public static void Kopf(string titel)
    {
        if (!Console.IsOutputRedirected)
            AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold blue]{titel}[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }

    public static void TrennlinieAnzeigen()
        => AnsiConsole.Write(new Rule());

    public static bool Bestaetigen(string frage)
        => AnsiConsole.Confirm(frage, defaultValue: false);

    public static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Weiter mit Enter...[/]");
        Console.ReadLine();
    }

    public static void ErfolgAnzeigen(string nachricht)
        => AnsiConsole.MarkupLine($"[green]✓[/] {nachricht}");

    public static void FehlerAnzeigen(string nachricht)
        => AnsiConsole.MarkupLine($"[red]✗ Fehler:[/] {Markup.Escape(nachricht)}");

    public static void WarnungAnzeigen(string nachricht)
        => AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(nachricht)}");

    public static void NochNichtImplementiert()
    {
        AnsiConsole.MarkupLine("[yellow]Diese Funktion ist in Version 1 noch nicht implementiert.[/]");
        Pause();
    }

    public static string RatingMarkup(int rating) => rating switch
    {
        >= 80 => $"[green]{rating}[/]",
        >= 65 => $"[yellow]{rating}[/]",
        _     => $"[red]{rating}[/]"
    };

    public static int RatingEingeben(string bezeichnung, int standard = 70)
        => AnsiConsole.Prompt(
            new TextPrompt<int>($"{bezeichnung} [grey](1–100)[/]:")
                .DefaultValue(standard)
                .ValidationErrorMessage("[red]Bitte eine ganze Zahl zwischen 1 und 100 eingeben.[/]")
                .Validate(r => r is >= 1 and <= 100));

    public static string TextEingeben(string bezeichnung, string standard = "")
    {
        var prompt = new TextPrompt<string>($"{bezeichnung}:")
            .ValidationErrorMessage("[red]Darf nicht leer sein.[/]")
            .Validate(s => !string.IsNullOrWhiteSpace(s));

        if (!string.IsNullOrWhiteSpace(standard))
            prompt.DefaultValue(standard);

        return AnsiConsole.Prompt(prompt);
    }
}
