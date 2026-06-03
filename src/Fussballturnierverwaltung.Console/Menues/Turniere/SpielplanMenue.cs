using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turniere;

public class SpielplanMenue(
    SpielplanService spielplanService,
    TurnierInstanzService instanzService,
    MannschaftService mannschaftService,
    SpielplanGeneratorFactory generatorFactory)
{
    private enum Option { SpielplanGenerieren, SpielplanAnzeigen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("📅 Spielplan-Generator");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.SpielplanGenerieren => "Spielplan für eine Runde generieren",
                        Option.SpielplanAnzeigen   => "Spielplan einer Runde anzeigen",
                        Option.Zurueck             => "← Zurück",
                        _                          => o.ToString()
                    })
                    .AddChoices(Option.SpielplanGenerieren, Option.SpielplanAnzeigen, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.SpielplanGenerieren: await SpielplanGenerierenAsync(ct); break;
            case Option.SpielplanAnzeigen:   await SpielplanAnzeigenAsync(ct);   break;
        }
    }

    private async Task SpielplanGenerierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spielplan generieren");

        var instanz = await TurnierInstanzAuswaehlenAsync(ct);
        if (instanz is null) return;

        var runde = RundeAuswaehlen(instanz);
        if (runde is null) return;

        if (!generatorFactory.IstUnterstuetzt(runde.Rundenart))
        {
            KonsolenHilfsmittel.WarnungAnzeigen($"Rundenart '{runde.Rundenart}' wird noch nicht unterstützt.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.MarkupLine($"\n[bold]Runde:[/] {Markup.Escape(runde.Name)} | " +
                               $"Rundenart: {runde.Rundenart} | " +
                               $"Benötigt: [yellow]{runde.EingangsMannschaftsanzahl}[/] Mannschaften\n");

        // Mannschaften für diese Runde auswählen
        var alleMannschaften = await mannschaftService.AlleAbrufenMitLandAsync(ct);
        AnsiConsole.MarkupLine($"[grey]Bitte genau {runde.EingangsMannschaftsanzahl} Mannschaften auswählen.[/]");
        AnsiConsole.MarkupLine("[grey]Die Reihenfolge bestimmt die Paarungen (Auslosungsreihenfolge).[/]\n");

        var ausgewaehlt = new List<Mannschaft>();
        var verbleibend = alleMannschaften.ToList();

        while (ausgewaehlt.Count < runde.EingangsMannschaftsanzahl)
        {
            int noch = runde.EingangsMannschaftsanzahl - ausgewaehlt.Count;
            if (!verbleibend.Any())
            {
                KonsolenHilfsmittel.WarnungAnzeigen("Keine weiteren Mannschaften verfügbar.");
                break;
            }

            var mann = AnsiConsole.Prompt(
                new SelectionPrompt<Mannschaft>()
                    .Title($"Mannschaft {ausgewaehlt.Count + 1} von {runde.EingangsMannschaftsanzahl} (noch {noch} ausstehend):")
                    .UseConverter(m => $"{m.Name} ({m.Land?.FifaCode ?? "?"})")
                    .EnableSearch().PageSize(15)
                    .AddChoices(verbleibend));

            ausgewaehlt.Add(mann);
            verbleibend.Remove(mann);
            AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(mann.Name)}");
        }

        if (ausgewaehlt.Count != runde.EingangsMannschaftsanzahl)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen – nicht genügend Mannschaften ausgewählt.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.WriteLine();
        var ergebnis = await AnsiConsole.Status()
            .StartAsync("Spielplan wird generiert...", async _ =>
                await spielplanService.GenerierenAsync(runde.Id, ausgewaehlt.Select(m => m.Id).ToList(), ct));

        KonsolenHilfsmittel.ErfolgAnzeigen($"{ergebnis.Spiele.Count} Spiele generiert.");
        AnsiConsole.WriteLine();
        SpielplanTabelle(ergebnis.Spiele, runde.Rundenart);
        KonsolenHilfsmittel.Pause();
    }

    private async Task SpielplanAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spielplan anzeigen");

        var instanz = await TurnierInstanzAuswaehlenAsync(ct);
        if (instanz is null) return;

        var runde = RundeAuswaehlen(instanz);
        if (runde is null) return;

        var spiele = await spielplanService.SpielplanAbrufenAsync(runde.Id, ct);
        if (!spiele.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen($"Für Runde '{runde.Name}' wurde noch kein Spielplan generiert.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        KonsolenHilfsmittel.Kopf($"Spielplan: {Markup.Escape(runde.Name)}");
        SpielplanTabelle(spiele, runde.Rundenart);
        KonsolenHilfsmittel.Pause();
    }

    private async Task<TurnierInstanz?> TurnierInstanzAuswaehlenAsync(CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (!instanzen.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden.");
            KonsolenHilfsmittel.Pause();
            return null;
        }
        return AnsiConsole.Prompt(
            new SelectionPrompt<TurnierInstanz>()
                .Title("Turnier:")
                .UseConverter(ti => $"{ti.Name} ({ti.SaisonOderJahr})")
                .EnableSearch().AddChoices(instanzen));
    }

    private static TurnierRunde? RundeAuswaehlen(TurnierInstanz instanz)
    {
        if (!instanz.Runden.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Dieses Turnier hat noch keine Runden.");
            KonsolenHilfsmittel.Pause();
            return null;
        }
        return AnsiConsole.Prompt(
            new SelectionPrompt<TurnierRunde>()
                .Title("Runde:")
                .UseConverter(r => $"{r.Reihenfolge}. {r.Name} [{r.Rundenart}] ({r.EingangsMannschaftsanzahl} Teams)")
                .AddChoices(instanz.Runden.OrderBy(r => r.Reihenfolge)));
    }

    private static void SpielplanTabelle(IReadOnlyList<Spiel> spiele, Rundenart rundenart)
    {
        var hatSpieltage = spiele.Any(s => s.Spieltag.HasValue && s.Spieltag > 0);
        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]Nr.[/]").RightAligned())
            .AddColumn("Heim")
            .AddColumn(new TableColumn("vs").Centered())
            .AddColumn("Auswärts")
            .AddColumn(new TableColumn("Status").Centered());

        if (hatSpieltage) tabelle.AddColumn(new TableColumn("[grey]Spieltag[/]").RightAligned());
        if (rundenart == Rundenart.KoRundeMitHinUndRueckspiel)
            tabelle.AddColumn(new TableColumn("[grey]H/R[/]").Centered());

        foreach (var s in spiele)
        {
            var row = new List<string>
            {
                $"[grey]{s.Spielnummer}[/]",
                Markup.Escape(s.HeimMannschaft?.Name ?? $"ID {s.HeimMannschaftId}"),
                "[grey]–[/]",
                Markup.Escape(s.AuswaertsMannschaft?.Name ?? $"ID {s.AuswaertsMannschaftId}"),
                s.Status == SpielStatus.Geplant ? "[grey]Geplant[/]" : s.Status.ToString()
            };
            if (hatSpieltage) row.Add(s.Spieltag?.ToString() ?? "[grey]–[/]");
            if (rundenart == Rundenart.KoRundeMitHinUndRueckspiel)
                row.Add(s.IstRueckspiel ? "[blue]R[/]" : "[green]H[/]");

            tabelle.AddRow([.. row]);
        }

        AnsiConsole.MarkupLine($"[grey]{spiele.Count} Spiel(e)[/]");
        AnsiConsole.Write(tabelle);
    }
}
