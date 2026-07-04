using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Konsole.Menues.Simulation;
using Fussballturnierverwaltung.Konsole.Menues.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
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
    AuslosungService auslosungService,
    SpielplanGeneratorFactory generatorFactory)
{
    private enum Option
    {
        SpielplanGenerieren,
        SpielplanAnzeigen,
        SpielplanLoeschen,
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
                        Option.SpielplanLoeschen   => "Spielplan einer Runde löschen",
                        Option.ErgebnisErfassen    => "Spielergebnis erfassen",
                        Option.ErgebnisLoeschen    => "Spielergebnis löschen",
                        Option.TabelleAnzeigen     => "Tabelle / Standings anzeigen",
                        Option.TurnierBaum         => "Turnierbaum / Bracket",
                        Option.Simulation          => "Simulation",
                        Option.Zurueck             => "← Zurück",
                        _                          => o.ToString()
                    })
                    .AddChoices(Option.SpielplanGenerieren, Option.SpielplanAnzeigen,
                                Option.SpielplanLoeschen, Option.ErgebnisErfassen,
                                Option.ErgebnisLoeschen, Option.TabelleAnzeigen,
                                Option.TurnierBaum, Option.Simulation, Option.Zurueck));

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
            case Option.SpielplanLoeschen:   await SpielplanLoeschenAsync(ct);   break;
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

        // TopfLiga verwendet Auslosungspaarungen statt manueller Team-Auswahl
        if (runde.Rundenart == Rundenart.TopfLiga)
        {
            await TopfLigaSpielplanGenerierenAsync(runde, instanz, ct);
            return;
        }

        // Schweizer System: pro Spieltag eine neue Runde basierend auf Tabellenstand
        if (runde.Rundenart == Rundenart.SchweizerSystem)
        {
            await SchweizerRundePlanenAsync(runde, ct);
            return;
        }

        AnsiConsole.MarkupLine($"\n[bold]Runde:[/] {Markup.Escape(runde.Name)} | " +
                               $"Rundenart: {runde.Rundenart} | " +
                               $"Benötigt: [yellow]{runde.EingangsMannschaftsanzahl}[/] Mannschaften\n");

        // Vorhandenen Spielplan prüfen
        var vorhandene = await spielplanService.SpielplanAbrufenAsync(runde.Id, ct);
        if (vorhandene.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/]  Für diese Runde existieren bereits [bold]{vorhandene.Count}[/] Spiele.");
            if (!KonsolenHilfsmittel.Bestaetigen("Vorhandenen Spielplan löschen und neu generieren?"))
            {
                KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
                KonsolenHilfsmittel.Pause();
                return;
            }
            await spielplanService.LoeschenAsync(runde.Id, ct);
        }

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
        SpielplanAnzeigenMitFilter(ergebnis.Spiele, runde.MitRueckspiel);
        KonsolenHilfsmittel.Pause();
    }

    private async Task TopfLigaSpielplanGenerierenAsync(TurnierRunde runde, TurnierInstanz instanz, CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf($"TopfLiga-Spielplan: {Markup.Escape(runde.Name)}");
        AnsiConsole.MarkupLine("[grey]Der Spielplan wird aus den Paarungen einer TopfZuordnung-Auslosung generiert.[/]\n");

        // Abgeschlossene TopfZuordnung-Auslosungen für diese Runde suchen
        var alleAuslosungen = await auslosungService.AlleNachInstanzAbrufenAsync(instanz.Id, ct);
        var topfAuslosungen = alleAuslosungen
            .Where(a => a.TurnierRundeId == runde.Id &&
                        a.Status         == AuslosungsStatus.Abgeschlossen &&
                        a.Auslosungsart  == Auslosungsart.TopfZuordnung)
            .ToList();

        if (!topfAuslosungen.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen(
                "Keine abgeschlossene TopfZuordnung-Auslosung für diese Runde vorhanden.\n" +
                "Bitte zuerst unter 'Auslosungen' eine Auslosung vom Typ 'Topf-Zuordnung' anlegen und durchführen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        Auslosung ausgewaehlt;
        if (topfAuslosungen.Count == 1)
        {
            ausgewaehlt = await auslosungService.FindenNachIdAsync(topfAuslosungen[0].Id, ct)
                          ?? topfAuslosungen[0];
        }
        else
        {
            var basis = AnsiConsole.Prompt(
                new SelectionPrompt<Auslosung?>()
                    .Title("Welche Auslosung verwenden?")
                    .UseConverter(a => a is null ? "← Zurück"
                        : $"{Markup.Escape(a.Name)} [{a.Ergebnisse.Count} Paarungen]")
                    .AddChoices(topfAuslosungen.Cast<Auslosung?>().Append(null)));
            if (basis is null) return;
            ausgewaehlt = await auslosungService.FindenNachIdAsync(basis.Id, ct) ?? basis;
        }

        var paarungen = ausgewaehlt.Ergebnisse
            .Where(e => !e.IstFreilos && e.HeimMannschaftId.HasValue && e.AuswaertsMannschaftId.HasValue)
            .Select(e => (HeimId: e.HeimMannschaftId!.Value, AuswaertsId: e.AuswaertsMannschaftId!.Value))
            .ToList<(int HeimId, int AuswaertsId)>();

        AnsiConsole.MarkupLine($"Auslosung: [bold]{Markup.Escape(ausgewaehlt.Name)}[/] | [bold]{paarungen.Count}[/] Paarungen");

        // Vorhandenen Spielplan prüfen
        var vorhandene = await spielplanService.SpielplanAbrufenAsync(runde.Id, ct);
        if (vorhandene.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/]  Für diese Runde existieren bereits [bold]{vorhandene.Count}[/] Spiele.");
            if (!KonsolenHilfsmittel.Bestaetigen("Vorhandenen Spielplan löschen und neu generieren?"))
            {
                KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
                KonsolenHilfsmittel.Pause();
                return;
            }
            await spielplanService.LoeschenAsync(runde.Id, ct);
        }

        AnsiConsole.WriteLine();
        var ergebnis = await AnsiConsole.Status()
            .StartAsync("Spielplan wird generiert...", async _ =>
                await spielplanService.GenerierenAusPaarungenAsync(runde.Id, paarungen, ct));

        int anzahlSpieltage = ergebnis.Spiele
            .Where(s => s.Spieltag.HasValue)
            .Select(s => s.Spieltag!.Value)
            .Distinct().Count();

        KonsolenHilfsmittel.ErfolgAnzeigen(
            $"{ergebnis.Spiele.Count} Spiele generiert, verteilt auf {anzahlSpieltage} Spieltage.");
        AnsiConsole.WriteLine();
        SpielplanAnzeigenMitFilter(ergebnis.Spiele, runde.MitRueckspiel);
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
        SpielplanAnzeigenMitFilter(spiele, runde.MitRueckspiel);
        KonsolenHilfsmittel.Pause();
    }

    private async Task SpielplanLoeschenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spielplan löschen");

        var instanz = await TurnierInstanzAuswaehlenAsync(ct);
        if (instanz is null) return;

        var runde = RundeAuswaehlen(instanz);
        if (runde is null) return;

        var spiele = await spielplanService.SpielplanAbrufenAsync(runde.Id, ct);
        if (!spiele.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Für diese Runde ist kein Spielplan vorhanden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.MarkupLine($"Runde [bold]{Markup.Escape(runde.Name)}[/]: [bold]{spiele.Count}[/] Spiele.");
        if (!KonsolenHilfsmittel.Bestaetigen($"[red]Alle {spiele.Count} Spiele dieser Runde wirklich löschen?[/]"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        await spielplanService.LoeschenAsync(runde.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"{spiele.Count} Spiele gelöscht.");
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
        var auswahl = AnsiConsole.Prompt(
            new SelectionPrompt<TurnierInstanz?>()
                .Title("Turnier:")
                .UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.SaisonOderJahr})")
                .EnableSearch().AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
        if (auswahl is null) return null;
        return await instanzService.FindenNachIdMitRundenAsync(auswahl.Id, ct);
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
        if (runde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
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
        KonsolenHilfsmittel.Kopf("Spielergebnis(se) löschen");

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

        var bereich = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Welche Ergebnisse löschen?")
                .AddChoices("Einzelnes Spiel", "Ganzen Spieltag", "Gesamte Runde", "← Zurück"));

        if (bereich == "← Zurück") return;

        if (bereich == "Einzelnes Spiel")
        {
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
            { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

            await ergebnisService.ErgebnisLoeschenAsync(spiel.Id, ct);
            KonsolenHilfsmittel.ErfolgAnzeigen("Ergebnis gelöscht.");
        }
        else if (bereich == "Ganzen Spieltag")
        {
            var spieltage = mitErgebnis
                .Where(s => s.Spieltag.HasValue)
                .GroupBy(s => s.Spieltag!.Value)
                .Select(g => (Spieltag: g.Key, Anzahl: g.Count()))
                .OrderBy(x => x.Spieltag)
                .ToList();

            if (!spieltage.Any())
            {
                KonsolenHilfsmittel.WarnungAnzeigen("Diese Runde hat keine Spieltage (kein Liga-Format).");
                KonsolenHilfsmittel.Pause();
                return;
            }

            var tag = AnsiConsole.Prompt(
                new SelectionPrompt<(int Spieltag, int Anzahl)?>()
                    .Title("Spieltag:")
                    .UseConverter(t => t is null ? "← Zurück"
                        : $"Spieltag {t.Value.Spieltag}  [grey]({t.Value.Anzahl} Ergebnis(se))[/]")
                    .AddChoices(spieltage.Select(t => ((int Spieltag, int Anzahl)?)t).Append(null)));
            if (tag is null) return;

            var zuLoeschen = mitErgebnis
                .Where(s => s.Spieltag == tag.Value.Spieltag)
                .Select(s => s.Id).ToList();

            if (!KonsolenHilfsmittel.Bestaetigen(
                    $"[red]{zuLoeschen.Count} Ergebnis(se)[/] von Spieltag {tag.Value.Spieltag} wirklich löschen?"))
            { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

            int n = await ergebnisService.MehrereErgebnisseLoeschenAsync(zuLoeschen, ct);
            KonsolenHilfsmittel.ErfolgAnzeigen($"{n} Ergebnis(se) gelöscht.");
        }
        else // Gesamte Runde
        {
            if (!KonsolenHilfsmittel.Bestaetigen(
                    $"[red]Alle {mitErgebnis.Count} Ergebnisse[/] der Runde [bold]{Markup.Escape(runde.Name)}[/] wirklich löschen?"))
            { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

            int n = await ergebnisService.MehrereErgebnisseLoeschenAsync(
                mitErgebnis.Select(s => s.Id).ToList(), ct);
            KonsolenHilfsmittel.ErfolgAnzeigen($"{n} Ergebnis(se) gelöscht.");
        }

        KonsolenHilfsmittel.Pause();
    }

    // ── Schweizer System ─────────────────────────────────────────────────

    private async Task SchweizerRundePlanenAsync(TurnierRunde runde, CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf($"Schweizer System: {Markup.Escape(runde.Name)}");

        var vorhandene = await spielplanService.SpielplanAbrufenAsync(runde.Id, ct);

        if (!vorhandene.Any())
        {
            await SchweizerErsteRundeAsync(runde, ct);
            return;
        }

        var offene = vorhandene.Where(s => s.Status != SpielStatus.Abgeschlossen).ToList();
        if (offene.Any())
        {
            int aktSpieltag = vorhandene.Max(s => s.Spieltag ?? 0);
            KonsolenHilfsmittel.WarnungAnzeigen(
                $"Spieltag {aktSpieltag} hat noch {offene.Count} offene Spiel(e). " +
                "Bitte alle Ergebnisse erfassen, bevor die nächste Runde generiert wird.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        await SchweizerNaechsteRundeAsync(runde, ct);
    }

    private async Task SchweizerErsteRundeAsync(TurnierRunde runde, CancellationToken ct)
    {
        int benoetigte = runde.EingangsMannschaftsanzahl >= 2
            ? runde.EingangsMannschaftsanzahl
            : 0;

        AnsiConsole.MarkupLine(
            "[grey]Runde 1 — Setzliste festlegen. " +
            "Setzung 1 spielt in Runde 1 gegen Setzung 2, Setzung 3 gegen 4 usw.[/]\n");

        var alleMannschaften = await mannschaftService.AlleAbrufenMitLandAsync(ct);
        var ausgewaehlt = new List<Mannschaft>();
        var verbleibend = alleMannschaften.ToList();

        while (verbleibend.Any())
        {
            int nr = ausgewaehlt.Count + 1;
            if (benoetigte > 0 && ausgewaehlt.Count >= benoetigte) break;

            bool kannAbbrechen = ausgewaehlt.Count >= 2 && benoetigte == 0;
            var choices = verbleibend.Cast<Mannschaft?>().ToList();
            if (kannAbbrechen) choices.Add(null);

            string titel = benoetigte > 0
                ? $"Setzung {nr} von {benoetigte}:"
                : $"Setzung {nr}:";

            var mann = AnsiConsole.Prompt(
                new SelectionPrompt<Mannschaft?>()
                    .Title(titel)
                    .UseConverter(m => m is null
                        ? "✓ Fertig — Spielplan generieren"
                        : $"{m.Name} ({m.Land?.FifaCode ?? "?"})")
                    .EnableSearch().PageSize(15)
                    .AddChoices(choices));

            if (mann is null) break;

            ausgewaehlt.Add(mann);
            verbleibend.Remove(mann);
            AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(mann.Name)} (Setzung {nr})");
        }

        if (ausgewaehlt.Count < 2)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Mindestens 2 Mannschaften benötigt. Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.WriteLine();
        var ergebnis = await AnsiConsole.Status()
            .StartAsync("Spieltag 1 wird generiert...", async _ =>
                await spielplanService.SchweizerRundeGenerierenAsync(
                    runde.Id, ausgewaehlt.Select(m => m.Id).ToList(), ct));

        KonsolenHilfsmittel.ErfolgAnzeigen($"Spieltag 1: {ergebnis.Spiele.Count} Spiel(e) generiert.");
        AnsiConsole.WriteLine();
        SpielplanAnzeigenMitFilter(ergebnis.Spiele, runde.MitRueckspiel);
        KonsolenHilfsmittel.Pause();
    }

    private async Task SchweizerNaechsteRundeAsync(TurnierRunde runde, CancellationToken ct)
    {
        var vorhandene = await spielplanService.SpielplanAbrufenAsync(runde.Id, ct);
        int naechsterSpieltag = vorhandene.Select(s => s.Spieltag ?? 0).DefaultIfEmpty(0).Max() + 1;

        var tabelle = await tabellenService.TabelleBerechnenAsync(runde.Id, ct: ct);

        if (!tabelle.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Tabelle berechenbar — Ergebnisse noch nicht vorhanden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.MarkupLine($"[bold]Tabelle nach Spieltag {naechsterSpieltag - 1}:[/]\n");
        TabelleTabelle(tabelle);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Spieltag {naechsterSpieltag} wird nach dieser Tabellenreihenfolge gepaartet.[/]");

        var bereitsGespielt = vorhandene
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .Select(s => (Math.Min(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value),
                          Math.Max(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value)))
            .Distinct().ToList();

        int teamAnzahl = tabelle.Count;
        bool hatFreilos = teamAnzahl % 2 == 1;
        if (hatFreilos)
            AnsiConsole.MarkupLine(
                $"[yellow]⚠[/]  Ungerade Teamanzahl ({teamAnzahl}) — " +
                $"[bold]{Markup.Escape(tabelle[^1].Mannschaft.Name)}[/] (letzter Platz) erhält ein Freilos.\n");

        if (!KonsolenHilfsmittel.Bestaetigen($"Spieltag {naechsterSpieltag} generieren?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var sortierteMannschaftIds = tabelle.Select(e => e.Mannschaft.Id).ToList();

        var ergebnis = await AnsiConsole.Status()
            .StartAsync($"Spieltag {naechsterSpieltag} wird generiert...", async _ =>
                await spielplanService.SchweizerRundeGenerierenAsync(runde.Id, sortierteMannschaftIds, ct));

        KonsolenHilfsmittel.ErfolgAnzeigen(
            $"Spieltag {naechsterSpieltag}: {ergebnis.Spiele.Count} Spiel(e) generiert.");
        AnsiConsole.WriteLine();
        SpielplanAnzeigenMitFilter(
            ergebnis.Spiele.Where(s => s.Spieltag == naechsterSpieltag).ToList(),
            runde.MitRueckspiel);
        KonsolenHilfsmittel.Pause();
    }

    // ── Tabelle ───────────────────────────────────────────────────────────

    private async Task TabelleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Tabelle / Standings");

        var instanz = await TurnierInstanzAuswaehlenAsync(ct);
        if (instanz is null) return;

        // Nur rundenmit Tabellenwertung anzeigen
        var rundenFuerTabelle = instanz.Runden
            .Where(r => r.Rundenart is Rundenart.Liga or Rundenart.Gruppenphase
                                    or Rundenart.TopfLiga or Rundenart.SchweizerSystem)
            .OrderBy(r => r.Reihenfolge)
            .ToList();

        if (!rundenFuerTabelle.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Dieses Turnier hat keine Runden mit Tabellenformat (Liga, Gruppenphase, TopfLiga, Schweizer System).");
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
                await tabellenService.TabelleBerechnenAsync(runde.Id, ct: ct));

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

    private const int SpieltageFilterAb = 4;

    private static void SpielplanAnzeigenMitFilter(IReadOnlyList<Spiel> spiele, bool mitRueckspiel)
    {
        var spieltage = spiele
            .Where(s => s.Spieltag.HasValue)
            .Select(s => s.Spieltag!.Value)
            .Distinct().OrderBy(t => t).ToList();

        if (spieltage.Count >= SpieltageFilterAb)
        {
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<int?>()
                    .Title("Spieltag auswählen:")
                    .UseConverter(t => t is null
                        ? $"Alle Spieltage  [grey]({spiele.Count} Spiele)[/]"
                        : $"Spieltag {t}  [grey]({spiele.Count(s => s.Spieltag == t)} Spiele)[/]")
                    .AddChoices(spieltage.Cast<int?>().Append(null)));

            var gefiltert = auswahl.HasValue
                ? spiele.Where(s => s.Spieltag == auswahl.Value).ToList()
                : spiele;

            if (auswahl.HasValue)
                AnsiConsole.MarkupLine($"[bold]Spieltag {auswahl.Value}[/]");

            SpielplanTabelle(gefiltert, mitRueckspiel);
        }
        else
        {
            SpielplanTabelle(spiele, mitRueckspiel);
        }
    }

    private static void SpielplanTabelle(IReadOnlyList<Spiel> spiele, bool mitRueckspiel)
    {
        var hatSpieltage = spiele.Any(s => s.Spieltag.HasValue && s.Spieltag > 0);
        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]Nr.[/]").RightAligned())
            .AddColumn("Heim")
            .AddColumn(new TableColumn("Ergebnis").Centered())
            .AddColumn("Auswärts");

        if (hatSpieltage) tabelle.AddColumn(new TableColumn("[grey]Spieltag[/]").RightAligned());
        if (mitRueckspiel)
            tabelle.AddColumn(new TableColumn("[grey]H/R[/]").Centered());

        foreach (var s in spiele)
        {
            var row = new List<string>
            {
                $"[grey]{s.Spielnummer}[/]",
                Markup.Escape(s.HeimMannschaft?.Name ?? $"ID {s.HeimMannschaftId}"),
                ErgebnisMarkup(s),
                Markup.Escape(s.AuswaertsMannschaft?.Name ?? $"ID {s.AuswaertsMannschaftId}")
            };
            if (hatSpieltage) row.Add(s.Spieltag?.ToString() ?? "[grey]–[/]");
            if (mitRueckspiel)
                row.Add(s.IstRueckspiel ? "[blue]R[/]" : "[green]H[/]");

            tabelle.AddRow([.. row]);
        }

        AnsiConsole.MarkupLine($"[grey]{spiele.Count} Spiel(e)[/]");
        AnsiConsole.Write(tabelle);
    }

    private static string ErgebnisMarkup(Spiel s)
    {
        if (s.Ergebnis is null || s.Status != SpielStatus.Abgeschlossen)
            return "[grey]–[/]";

        var e = s.Ergebnis;
        var score = $"[bold]{e.HeimToreGesamt} : {e.AuswaertsToreGesamt}[/]";
        var suffix = e.EntscheidungArt switch
        {
            EntscheidungArt.Verlaengerung      => " [grey]n.V.[/]",
            EntscheidungArt.GoldenGoal         => " [grey]GG[/]",
            EntscheidungArt.SilverGoal         => " [grey]SG[/]",
            EntscheidungArt.Elfmeterschiessen  => $" [grey]i.E. ({e.HeimElfmetertore}:{e.AuswaertsElfmetertore})[/]",
            EntscheidungArt.Losentscheid       => " [grey]Los[/]",
            EntscheidungArt.Muenzwurf          => " [grey]Münzwurf[/]",
            _                                  => ""
        };
        return score + suffix;
    }
}
