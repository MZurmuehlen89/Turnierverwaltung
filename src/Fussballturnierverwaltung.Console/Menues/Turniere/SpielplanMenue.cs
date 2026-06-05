using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Konsole.Menues.Simulation;
using Fussballturnierverwaltung.Konsole.Menues.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turniere;

public class SpielplanMenue(
    SpielplanService spielplanService,
    SpielErgebnisService ergebnisService,
    TabellenService tabellenService,
    TurnierBaumMenue turnierBaumMenue,
    SimulationsMenue simulationsMenue,
    TurnierInstanzService instanzService,
    MannschaftService mannschaftService,
    SpielplanGeneratorFactory generatorFactory)
{
    private enum Option
    {
        SpielplanGenerieren,
        SpielplanAnzeigen,
        ErgebnisErfassen,
        ErgebnisLoeschen,
        TabelleAnzeigen,
        TurnierBaum,
        Simulation,
        Zurueck
    }

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
                        Option.ErgebnisErfassen    => "Spielergebnis erfassen",
                        Option.ErgebnisLoeschen    => "Spielergebnis löschen",
                        Option.TabelleAnzeigen     => "Tabelle / Standings anzeigen",
                        Option.TurnierBaum         => "Turnierbaum / Bracket",
                        Option.Simulation          => "Simulation",
                        Option.Zurueck             => "← Zurück",
                        _                          => o.ToString()
                    })
                    .AddChoices(Option.SpielplanGenerieren, Option.SpielplanAnzeigen,
                                Option.ErgebnisErfassen, Option.ErgebnisLoeschen,
                                Option.TabelleAnzeigen, Option.TurnierBaum,
                                Option.Simulation, Option.Zurueck));

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
            case Option.ErgebnisErfassen:    await ErgebnisErfassenAsync(ct);    break;
            case Option.ErgebnisLoeschen:    await ErgebnisLoeschenAsync(ct);    break;
            case Option.TabelleAnzeigen:     await TabelleAnzeigenAsync(ct);     break;
            case Option.TurnierBaum:         await turnierBaumMenue.AnzeigenAsync(ct);  break;
            case Option.Simulation:          await simulationsMenue.AnzeigenAsync(ct); break;
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
            new SelectionPrompt<TurnierInstanz?>()
                .Title("Turnier:")
                .UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.SaisonOderJahr})")
                .EnableSearch().AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
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
            new SelectionPrompt<TurnierRunde?>()
                .Title("Runde:")
                .UseConverter(r => r is null ? "← Zurück" : $"{r.Reihenfolge}. {Markup.Escape(r.Name)} [[{r.Rundenart}]] ({r.EingangsMannschaftsanzahl} Teams)")
                .AddChoices(instanz.Runden.OrderBy(r => r.Reihenfolge).Cast<TurnierRunde?>().Append(null)));
    }

    // ── Ergebnis erfassen ─────────────────────────────────────────────────

    private async Task ErgebnisErfassenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spielergebnis erfassen");

        var instanz = await TurnierInstanzAuswaehlenAsync(ct);
        if (instanz is null) return;

        var runde = RundeAuswaehlen(instanz);
        if (runde is null) return;

        var spiele = await ergebnisService.AlleNachRundeAsync(runde.Id, ct);
        var offene  = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen).ToList();

        if (!offene.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Alle Spiele dieser Runde haben bereits ein Ergebnis.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var spiel = AnsiConsole.Prompt(
            new SelectionPrompt<Spiel?>()
                .Title("Spiel auswählen:")
                .UseConverter(s => s is null ? "← Zurück" : $"#{s.Spielnummer}  {s.HeimMannschaft?.Name ?? "?"} – {s.AuswaertsMannschaft?.Name ?? "?"}")
                .AddChoices(offene.Cast<Spiel?>().Append(null)));
        if (spiel is null) return;

        AnsiConsole.MarkupLine($"\n[bold]{Markup.Escape(spiel.HeimMannschaft?.Name ?? "Heim")}[/] " +
                               $"vs [bold]{Markup.Escape(spiel.AuswaertsMannschaft?.Name ?? "Auswärts")}[/]\n");

        int heimTore     = AnsiConsole.Prompt(new TextPrompt<int>($"Tore {Markup.Escape(spiel.HeimMannschaft?.Name ?? "Heim")}:").Validate(n => n >= 0));
        int auswaertsTore = AnsiConsole.Prompt(new TextPrompt<int>($"Tore {Markup.Escape(spiel.AuswaertsMannschaft?.Name ?? "Auswärts")}:").Validate(n => n >= 0));

        var entscheidungArt = EntscheidungArt.RegulaereSpielzeit;
        int? heimVerl = null, auswaertsVerl = null, heimElf = null, auswaertsElf = null;

        // Für K.-o.-Runden: Verlängerung / Elfmeter möglich
        if (runde.Rundenart is Rundenart.KoRunde or Rundenart.KoRundeMitHinUndRueckspiel or
            Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            if (heimTore == auswaertsTore)
            {
                entscheidungArt = AnsiConsole.Prompt(
                    new SelectionPrompt<EntscheidungArt>()
                        .Title("Wie wurde entschieden?")
                        .AddChoices(EntscheidungArt.Verlaengerung, EntscheidungArt.Elfmeterschiessen,
                                    EntscheidungArt.Losentscheid));

                if (entscheidungArt is EntscheidungArt.Verlaengerung or EntscheidungArt.Elfmeterschiessen)
                {
                    heimVerl     = AnsiConsole.Prompt(new TextPrompt<int>("Tore Verlängerung Heim:").Validate(n => n >= 0));
                    auswaertsVerl = AnsiConsole.Prompt(new TextPrompt<int>("Tore Verlängerung Auswärts:").Validate(n => n >= 0));
                }

                if (entscheidungArt == EntscheidungArt.Elfmeterschiessen)
                {
                    heimElf     = AnsiConsole.Prompt(new TextPrompt<int>("Elfmetertore Heim:").Validate(n => n >= 0));
                    auswaertsElf = AnsiConsole.Prompt(new TextPrompt<int>("Elfmetertore Auswärts:").Validate(n => n >= 0));
                }
            }
        }

        var eingabe = new ErgebnisErfassenEingabe(
            heimTore, auswaertsTore, entscheidungArt,
            heimVerl, auswaertsVerl, heimElf, auswaertsElf);

        await ergebnisService.ErgebnisErfassenAsync(spiel.Id, eingabe, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Ergebnis gespeichert: {heimTore}:{auswaertsTore}");
        KonsolenHilfsmittel.Pause();
    }

    private async Task ErgebnisLoeschenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spielergebnis löschen");

        var instanz = await TurnierInstanzAuswaehlenAsync(ct);
        if (instanz is null) return;

        var runde = RundeAuswaehlen(instanz);
        if (runde is null) return;

        var spiele = await ergebnisService.AlleNachRundeAsync(runde.Id, ct);
        var mitErgebnis = spiele.Where(s => s.Status == SpielStatus.Abgeschlossen).ToList();

        if (!mitErgebnis.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Spiele mit Ergebnis in dieser Runde.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var spiel = AnsiConsole.Prompt(
            new SelectionPrompt<Spiel?>()
                .Title("Ergebnis welches Spiels löschen?")
                .UseConverter(s =>
                {
                    if (s is null) return "← Zurück";
                    var score = s.Ergebnis is not null
                        ? $"{s.Ergebnis.HeimToreRegulaer}:{s.Ergebnis.AuswaertsToreRegulaer}"
                        : "?:?";
                    return $"#{s.Spielnummer}  {s.HeimMannschaft?.Name ?? "?"} {score} {s.AuswaertsMannschaft?.Name ?? "?"}";
                })
                .AddChoices(mitErgebnis.Cast<Spiel?>().Append(null)));
        if (spiel is null) return;

        if (!KonsolenHilfsmittel.Bestaetigen($"Ergebnis von Spiel #{spiel.Spielnummer} wirklich löschen?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        await ergebnisService.ErgebnisLoeschenAsync(spiel.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen("Ergebnis gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    // ── Tabelle ───────────────────────────────────────────────────────────

    private async Task TabelleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Tabelle / Standings");

        var instanz = await TurnierInstanzAuswaehlenAsync(ct);
        if (instanz is null) return;

        // Nur Liga/Gruppenphase-Runden für die Tabelle sinnvoll
        var rundenFuerTabelle = instanz.Runden
            .Where(r => r.Rundenart is Rundenart.Liga or Rundenart.Gruppenphase)
            .OrderBy(r => r.Reihenfolge)
            .ToList();

        if (!rundenFuerTabelle.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Dieses Turnier hat keine Liga- oder Gruppenphase-Runden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        TurnierRunde? runde;
        if (rundenFuerTabelle.Count == 1)
        {
            runde = rundenFuerTabelle[0];
        }
        else
        {
            runde = AnsiConsole.Prompt(
                new SelectionPrompt<TurnierRunde?>()
                    .Title("Runde:")
                    .UseConverter(r => r is null ? "← Zurück" : $"{r.Reihenfolge}. {r.Name}")
                    .AddChoices(rundenFuerTabelle.Cast<TurnierRunde?>().Append(null)));
            if (runde is null) return;
        }

        var tabelle = await AnsiConsole.Status()
            .StartAsync("Tabelle wird berechnet...", async _ =>
                await tabellenService.TabelleBerechnenAsync(runde.Id, ct));

        if (!tabelle.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Noch keine Spiele für diese Runde vorhanden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        KonsolenHilfsmittel.Kopf($"Tabelle: {Markup.Escape(runde.Name)}");
        TabelleTabelle(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private static void TabelleTabelle(IReadOnlyList<TabellenEintrag> tabelle)
    {
        var t = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]#[/]").RightAligned())
            .AddColumn("Mannschaft")
            .AddColumn(new TableColumn("[grey]Sp[/]").RightAligned())
            .AddColumn(new TableColumn("[green]S[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]U[/]").RightAligned())
            .AddColumn(new TableColumn("[red]N[/]").RightAligned())
            .AddColumn(new TableColumn("Tore").RightAligned())
            .AddColumn(new TableColumn("Diff").RightAligned())
            .AddColumn(new TableColumn("[bold]Pkt[/]").RightAligned());

        foreach (var e in tabelle)
        {
            var diffMarkup = e.Tordifferenz > 0
                ? $"[green]+{e.Tordifferenz}[/]"
                : e.Tordifferenz < 0
                    ? $"[red]{e.Tordifferenz}[/]"
                    : "[grey]±0[/]";

            t.AddRow(
                $"[grey]{e.Rang}[/]",
                Markup.Escape(e.Mannschaft.Name),
                e.Spiele.ToString(),
                $"[green]{e.Siege}[/]",
                $"[grey]{e.Unentschieden}[/]",
                $"[red]{e.Niederlagen}[/]",
                $"{e.Tore}:{e.Gegentore}",
                diffMarkup,
                $"[bold]{e.Punkte}[/]");
        }

        AnsiConsole.Write(t);
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
