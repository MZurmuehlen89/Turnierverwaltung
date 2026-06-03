using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Stammdaten;

public class MannschaftMenue(MannschaftService mannschaftService, LandService landService)
{
    private enum Option { AlleAnzeigen, Suchen, NeuAnlegen, Bearbeiten, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("⚽ Mannschaften");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen => "Alle anzeigen",
                        Option.Suchen       => "Suchen",
                        Option.NeuAnlegen   => "Neue Mannschaft anlegen",
                        Option.Bearbeiten   => "Mannschaft bearbeiten",
                        Option.Loeschen     => "Mannschaft löschen",
                        Option.Zurueck      => "← Zurück",
                        _                   => o.ToString()
                    })
                    .AddChoices(Option.AlleAnzeigen, Option.Suchen, Option.NeuAnlegen,
                                Option.Bearbeiten, Option.Loeschen, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try
            {
                await AktionAusfuehrenAsync(auswahl, ct);
            }
            catch (Exception ex)
            {
                KonsolenHilfsmittel.FehlerAnzeigen(ex.Message);
                KonsolenHilfsmittel.Pause();
            }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.AlleAnzeigen:
                await AlleAnzeigenAsync(ct);
                break;
            case Option.Suchen:
                await SuchenAsync(ct);
                break;
            case Option.NeuAnlegen:
                await NeuAnlegenAsync(ct);
                break;
            case Option.Bearbeiten:
                await BearbeitenAsync(ct);
                break;
            case Option.Loeschen:
                await LoeschenAsync(ct);
                break;
        }
    }

    // --- Alle anzeigen ---

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Mannschaften");
        var mannschaften = await mannschaftService.AlleAbrufenMitLandAsync(ct);
        MannschaftenTabelle(mannschaften);
        KonsolenHilfsmittel.Pause();
    }

    // --- Suchen ---

    private async Task SuchenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Mannschaft suchen");
        var suchbegriff = AnsiConsole.Ask<string>("Suchbegriff (Name, Kürzel oder Land):");

        var ergebnisse = await mannschaftService.SuchenAsync(suchbegriff, ct);

        AnsiConsole.WriteLine();
        if (ergebnisse.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Mannschaft gefunden.");
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey]{ergebnisse.Count} Treffer für '{Markup.Escape(suchbegriff)}'[/]");
            MannschaftenTabelle(ergebnisse);
        }
        KonsolenHilfsmittel.Pause();
    }

    // --- Neu anlegen ---

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neue Mannschaft anlegen");

        var laender = await landService.AlleAbrufenAsync(ct);
        if (laender.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Länder vorhanden. Bitte zuerst Länder anlegen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var name     = KonsolenHilfsmittel.TextEingeben("Name");
        var kurzname = KonsolenHilfsmittel.TextEingeben("Kürzel");

        var art = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaftsart>()
                .Title("Mannschaftsart:")
                .UseConverter(a => a == Mannschaftsart.Nationalmannschaft ? "Nationalmannschaft" : "Verein")
                .AddChoices(Mannschaftsart.Nationalmannschaft, Mannschaftsart.Verein));

        var land = LandAuswaehlen(laender);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Ratings (1–100)[/]");
        var off  = KonsolenHilfsmittel.RatingEingeben("Offensiv-Rating");
        var mid  = KonsolenHilfsmittel.RatingEingeben("Mittelfeld-Rating");
        var def  = KonsolenHilfsmittel.RatingEingeben("Defensiv-Rating");
        var heim = KonsolenHilfsmittel.RatingEingeben("Heimstärke");
        var ausw = KonsolenHilfsmittel.RatingEingeben("Auswärtsstärke");

        var eingabe = new MannschaftErstellenEingabe(name, kurzname, art, land.Id, off, mid, def, heim, ausw);
        var neu = await mannschaftService.ErstellenAsync(eingabe, ct);

        AnsiConsole.WriteLine();
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{neu.Name}' wurde angelegt (ID {neu.Id}, GesamtRating {neu.GesamtRating:F1}).");
        KonsolenHilfsmittel.Pause();
    }

    // --- Bearbeiten ---

    private async Task BearbeitenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Mannschaft bearbeiten");

        var mannschaften = await mannschaftService.AlleAbrufenMitLandAsync(ct);
        if (mannschaften.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Mannschaften vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaft>()
                .Title("Welche Mannschaft bearbeiten?")
                .UseConverter(m => $"{m.Name} ({m.Land?.FifaCode ?? "?"}) – {(m.Mannschaftsart == Mannschaftsart.Nationalmannschaft ? "NT" : "V")}")
                .EnableSearch()
                .AddChoices(mannschaften));

        var laender = await landService.AlleAbrufenAsync(ct);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Aktuelle Werte für [bold]{Markup.Escape(auswahl.Name)}[/]:");
        MannschaftDetailAnzeigen(auswahl);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Enter = Wert behalten[/]");
        AnsiConsole.WriteLine();

        var name     = KonsolenHilfsmittel.TextEingeben("Name", auswahl.Name);
        var kurzname = KonsolenHilfsmittel.TextEingeben("Kürzel", auswahl.Kurzname);

        var aktuellesLand = laender.FirstOrDefault(l => l.Id == auswahl.LandId) ?? laender[0];
        AnsiConsole.MarkupLine($"Aktuelles Land: [bold]{Markup.Escape(aktuellesLand.Name)}[/] – Ändern?");
        var landAendern = AnsiConsole.Confirm("Land ändern?", defaultValue: false);
        var land = landAendern ? LandAuswaehlen(laender) : aktuellesLand;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Ratings (1–100, Enter = behalten)[/]");
        var off  = KonsolenHilfsmittel.RatingEingeben("Offensiv-Rating",    auswahl.OffensivRating);
        var mid  = KonsolenHilfsmittel.RatingEingeben("Mittelfeld-Rating",  auswahl.MittelfeldRating);
        var def  = KonsolenHilfsmittel.RatingEingeben("Defensiv-Rating",    auswahl.DefensivRating);
        var heim = KonsolenHilfsmittel.RatingEingeben("Heimstärke",         auswahl.Heimstaerke);
        var ausw = KonsolenHilfsmittel.RatingEingeben("Auswärtsstärke",     auswahl.Auswaertsstaerke);

        var eingabe = new MannschaftAktualisierenEingabe(name, kurzname, land.Id, off, mid, def, heim, ausw);
        await mannschaftService.AktualisierenAsync(auswahl.Id, eingabe, ct);

        AnsiConsole.WriteLine();
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{name}' wurde aktualisiert.");
        KonsolenHilfsmittel.Pause();
    }

    // --- Löschen ---

    private async Task LoeschenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Mannschaft löschen");

        var mannschaften = await mannschaftService.AlleAbrufenMitLandAsync(ct);
        if (mannschaften.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Mannschaften vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaft>()
                .Title("Welche Mannschaft löschen?")
                .UseConverter(m => $"{m.Name} ({m.Land?.FifaCode ?? "?"})")
                .EnableSearch()
                .AddChoices(mannschaften));

        AnsiConsole.WriteLine();
        MannschaftDetailAnzeigen(auswahl);
        AnsiConsole.WriteLine();

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(auswahl.Name)}'[/] wirklich löschen?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        await mannschaftService.LoeschenAsync(auswahl.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{auswahl.Name}' wurde gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    // --- Hilfsmethoden ---

    private static Land LandAuswaehlen(IReadOnlyList<Land> laender)
        => AnsiConsole.Prompt(
            new SelectionPrompt<Land>()
                .Title("Land auswählen:")
                .UseConverter(l => $"{l.Name} ({l.FifaCode})")
                .EnableSearch()
                .PageSize(15)
                .AddChoices(laender));

    private static void MannschaftenTabelle(IReadOnlyList<Mannschaft> mannschaften)
    {
        var tabelle = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn(new TableColumn("Typ").Centered())
            .AddColumn("Land")
            .AddColumn(new TableColumn("[cyan]Off[/]").RightAligned())
            .AddColumn(new TableColumn("[cyan]Mid[/]").RightAligned())
            .AddColumn(new TableColumn("[cyan]Def[/]").RightAligned())
            .AddColumn(new TableColumn("[bold cyan]Ges[/]").RightAligned())
            .AddColumn(new TableColumn("[blue]Heim[/]").RightAligned())
            .AddColumn(new TableColumn("[blue]Ausw[/]").RightAligned());

        foreach (var m in mannschaften)
        {
            var typ = m.Mannschaftsart == Mannschaftsart.Nationalmannschaft
                ? "[blue]NT[/]" : "[green]V[/]";
            var land = m.Land != null ? $"{m.Land.FifaCode}" : "[grey]?[/]";
            var ges  = $"{m.GesamtRating:F1}";

            tabelle.AddRow(
                $"[grey]{m.Id}[/]",
                Markup.Escape(m.Name),
                typ,
                land,
                KonsolenHilfsmittel.RatingMarkup(m.OffensivRating),
                KonsolenHilfsmittel.RatingMarkup(m.MittelfeldRating),
                KonsolenHilfsmittel.RatingMarkup(m.DefensivRating),
                KonsolenHilfsmittel.RatingMarkup((int)Math.Round(m.GesamtRating)),
                KonsolenHilfsmittel.RatingMarkup(m.Heimstaerke),
                KonsolenHilfsmittel.RatingMarkup(m.Auswaertsstaerke));
        }

        AnsiConsole.MarkupLine($"[grey]{mannschaften.Count} Mannschaft(en)[/]");
        AnsiConsole.Write(tabelle);
    }

    private static void MannschaftDetailAnzeigen(Mannschaft m)
    {
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]ID:[/]",           $"{m.Id}");
        grid.AddRow("[grey]Name:[/]",         Markup.Escape(m.Name));
        grid.AddRow("[grey]Kürzel:[/]",       Markup.Escape(m.Kurzname));
        grid.AddRow("[grey]Art:[/]",          m.Mannschaftsart == Mannschaftsart.Nationalmannschaft ? "Nationalmannschaft" : "Verein");
        grid.AddRow("[grey]Land:[/]",         m.Land != null ? $"{Markup.Escape(m.Land.Name)} ({m.Land.FifaCode})" : "?");
        grid.AddRow("[grey]Off/Mid/Def:[/]",  $"{m.OffensivRating} / {m.MittelfeldRating} / {m.DefensivRating}");
        grid.AddRow("[grey]GesamtRating:[/]", $"{m.GesamtRating:F1}");
        grid.AddRow("[grey]Heim / Ausw:[/]",  $"{m.Heimstaerke} / {m.Auswaertsstaerke}");
        AnsiConsole.Write(new Panel(grid).Header(Markup.Escape(m.Name)));
    }
}
