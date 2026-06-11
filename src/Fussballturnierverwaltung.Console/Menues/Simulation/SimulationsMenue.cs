using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Simulation;

public class SimulationsMenue(
    SimulationsService simulationsService,
    SpielErgebnisService spielErgebnisService,
    TurnierInstanzService instanzService,
    MannschaftService mannschaftService)
{
    private enum Option
    {
        Freundschaftsspiel,
        SpielSimulieren,
        SpieltageSimulieren,
        RundeSimulieren,
        TurnierSimulieren,
        Liveticker,
        Protokoll,
        Zurueck
    }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("⚡ Simulation");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.Freundschaftsspiel  => "⚽ Einzelspiel (Freundschaftsspiel)",
                        Option.SpielSimulieren     => "Ein Turnierspiel simulieren",
                        Option.SpieltageSimulieren => "Einen Spieltag simulieren",
                        Option.RundeSimulieren     => "Ganze Runde simulieren",
                        Option.TurnierSimulieren   => "Ganzes Turnier simulieren",
                        Option.Liveticker          => "📺 Liveticker (Spielverlauf anzeigen)",
                        Option.Protokoll           => "Simulationsprotokoll anzeigen",
                        Option.Zurueck             => "← Zurück",
                        _                          => o.ToString()
                    })
                    .AddChoices(Option.Freundschaftsspiel, Option.SpielSimulieren,
                                Option.SpieltageSimulieren, Option.RundeSimulieren,
                                Option.TurnierSimulieren, Option.Liveticker,
                                Option.Protokoll, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.Freundschaftsspiel:  await FreundschaftsspielAsync(ct);    break;
            case Option.SpielSimulieren:     await SpielSimulierenAsync(ct);      break;
            case Option.SpieltageSimulieren: await SpieltageSimulierenAsync(ct);  break;
            case Option.RundeSimulieren:     await RundeSimulierenAsync(ct);      break;
            case Option.TurnierSimulieren:  await TurnierSimulierenAsync(ct);    break;
            case Option.Liveticker:         await LivetickerAnzeigenAsync(ct);  break;
            case Option.Protokoll:          await ProtokollAnzeigenAsync(ct);    break;
        }
    }

    // ── Freundschaftsspiel ────────────────────────────────────────────────

    private async Task FreundschaftsspielAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("⚽ Einzelspiel / Freundschaftsspiel");

        var alleMannschaften = await mannschaftService.AlleAbrufenMitLandAsync(ct);
        if (alleMannschaften.Count < 2)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Mindestens 2 Mannschaften erforderlich.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var heim = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaft>()
                .Title("Heimmannschaft:")
                .UseConverter(m => $"{m.Name} ({m.Land?.FifaCode ?? "?"})")
                .EnableSearch().PageSize(15)
                .AddChoices(alleMannschaften));

        var rest = alleMannschaften.Where(m => m.Id != heim.Id).ToList();
        var auswaerts = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaft>()
                .Title("Auswärtsmannschaft:")
                .UseConverter(m => $"{m.Name} ({m.Land?.FifaCode ?? "?"})")
                .EnableSearch().PageSize(15)
                .AddChoices(rest));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(heim.Name)}[/]  [grey]Rating {heim.GesamtRating:F0}[/]  " +
                               $"vs  [bold]{Markup.Escape(auswaerts.Name)}[/]  [grey]Rating {auswaerts.GesamtRating:F0}[/]");
        AnsiConsole.WriteLine();

        var aktion = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Aktion:")
                .AddChoices("Simulieren", "Ergebnis manuell eingeben"));

        SimulationsErgebnis ergebnis;

        if (aktion == "Simulieren")
        {
            var modus = ModusAuswaehlen();
            ergebnis = SimulationsEngine.Simulieren(
                heim, auswaerts, [], modus == SimulationsModus.AusfuehrlicheSimulation, new Random());
        }
        else
        {
            int heimTore = AnsiConsole.Prompt(
                new TextPrompt<int>($"Tore [bold]{Markup.Escape(heim.Name)}[/]:").Validate(n => n >= 0));
            int auswaertsTore = AnsiConsole.Prompt(
                new TextPrompt<int>($"Tore [bold]{Markup.Escape(auswaerts.Name)}[/]:").Validate(n => n >= 0));
            ergebnis = new SimulationsErgebnis(
                heimTore, auswaertsTore, null, null, null, null,
                EntscheidungArt.RegulaereSpielzeit, []);
        }

        AnsiConsole.WriteLine();
        ErgebnisAnzeigen(heim.Name, auswaerts.Name, ergebnis);
        KonsolenHilfsmittel.Pause();
    }

    // ── Ein Spiel ─────────────────────────────────────────────────────────

    private async Task SpielSimulierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spiel simulieren");

        var (instanz, runde) = await TurnierUndRundeAuswaehlenAsync(ct);
        if (instanz is null || runde is null) return;

        var spiele = await spielErgebnisService.AlleNachRundeAsync(runde.Id, ct);
        var offene = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen).ToList();

        if (!offene.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Alle Spiele dieser Runde sind bereits abgeschlossen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var spiel = AnsiConsole.Prompt(
            new SelectionPrompt<Spiel?>()
                .Title("Spiel:")
                .UseConverter(s => s is null ? "← Zurück" : $"#{s.Spielnummer}  {s.HeimMannschaft?.Name ?? "?"} vs {s.AuswaertsMannschaft?.Name ?? "?"}")
                .AddChoices(offene.Cast<Spiel?>().Append(null)));
        if (spiel is null) return;

        var modus = ModusAuswaehlen();

        var ergebnis = await AnsiConsole.Status()
            .StartAsync("Simuliere...", async _ =>
                await simulationsService.SpielSimulierenAsync(spiel.Id, modus, null, ct));

        AnsiConsole.WriteLine();
        ErgebnisAnzeigen(spiel.HeimMannschaft?.Name ?? "Heim",
                         spiel.AuswaertsMannschaft?.Name ?? "Auswärts",
                         ergebnis);
        KonsolenHilfsmittel.Pause();
    }

    // ── Spieltag ──────────────────────────────────────────────────────────

    private async Task SpieltageSimulierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spieltag simulieren");

        var (instanz, runde) = await TurnierUndRundeAuswaehlenAsync(ct);
        if (instanz is null || runde is null) return;

        var spiele = await spielErgebnisService.AlleNachRundeAsync(runde.Id, ct);

        var infos = spiele
            .Where(s => s.Spieltag.HasValue)
            .GroupBy(s => s.Spieltag!.Value)
            .Select(g => (
                Spieltag: g.Key,
                Gesamt:   g.Count(),
                Offen:    g.Count(s => s.Status != SpielStatus.Abgeschlossen)))
            .OrderBy(i => i.Spieltag)
            .ToList();

        if (!infos.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen(
                "Diese Runde hat keine Spieltage. Spieltage werden nur bei Liga-/Gruppenrunden generiert.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var auswahl = AnsiConsole.Prompt(
            new SelectionPrompt<(int Spieltag, int Gesamt, int Offen)?>()
                .Title("Spieltag:")
                .UseConverter(i => i is null
                    ? "← Zurück"
                    : $"Spieltag {i.Value.Spieltag}  " +
                      $"[grey]({i.Value.Gesamt} Spiele, {i.Value.Offen} offen)[/]")
                .AddChoices(infos.Select(i => ((int Spieltag, int Gesamt, int Offen)?)i).Append(null)));
        if (auswahl is null) return;

        if (auswahl.Value.Offen == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen(
                $"Spieltag {auswahl.Value.Spieltag} ist bereits vollständig abgeschlossen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var modus = ModusAuswaehlen();

        // Offene Spiele dieses Spieltags mit Teamdaten laden
        var offene = spiele
            .Where(s => s.Spieltag == auswahl.Value.Spieltag && s.Status != SpielStatus.Abgeschlossen)
            .ToList();

        var ergebnisse = new List<(Spiel Spiel, SimulationsErgebnis Ergebnis)>();

        await AnsiConsole.Status().StartAsync("Spieltag wird simuliert...", async _ =>
        {
            foreach (var spiel in offene)
            {
                var e = await simulationsService.SpielSimulierenAsync(spiel.Id, modus, null, ct);
                ergebnisse.Add((spiel, e));
            }
        });

        AnsiConsole.WriteLine();
        SimulationsErgebnisseAnzeigen(ergebnisse, modus);
        KonsolenHilfsmittel.ErfolgAnzeigen($"{ergebnisse.Count} Spiel(e) simuliert.");
        KonsolenHilfsmittel.Pause();
    }

    // ── Runde ─────────────────────────────────────────────────────────────

    private async Task RundeSimulierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Runde simulieren");

        var (instanz, runde) = await TurnierUndRundeAuswaehlenAsync(ct);
        if (instanz is null || runde is null) return;

        var spiele = await spielErgebnisService.AlleNachRundeAsync(runde.Id, ct);
        var offene = spiele.Where(s => s.Status != SpielStatus.Abgeschlossen).ToList();

        if (!offene.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Alle Spiele dieser Runde sind bereits abgeschlossen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var modus = ModusAuswaehlen();

        if (!KonsolenHilfsmittel.Bestaetigen(
                $"Alle {offene.Count} offenen Spiele der Runde [bold]{Markup.Escape(runde.Name)}[/] simulieren?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var ergebnisse = new List<(Spiel Spiel, SimulationsErgebnis Ergebnis)>();
        await AnsiConsole.Status().StartAsync("Runde wird simuliert...", async _ =>
        {
            foreach (var spiel in offene)
            {
                var e = await simulationsService.SpielSimulierenAsync(spiel.Id, modus, null, ct);
                ergebnisse.Add((spiel, e));
            }
        });

        AnsiConsole.WriteLine();
        SimulationsErgebnisseAnzeigen(ergebnisse, modus);
        KonsolenHilfsmittel.ErfolgAnzeigen($"{ergebnisse.Count} Spiel(e) simuliert.");
        KonsolenHilfsmittel.Pause();
    }

    // ── Ganzes Turnier ────────────────────────────────────────────────────

    private async Task TurnierSimulierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Ganzes Turnier simulieren");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var modus = ModusAuswaehlen();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]⚠[/]  Alle noch nicht abgeschlossenen Spiele aller Runden werden simuliert.");
        AnsiConsole.MarkupLine("    K.-o.-Sieger werden automatisch fortgeschrieben (falls Turnierbaum vorhanden).");
        AnsiConsole.WriteLine();

        if (!KonsolenHilfsmittel.Bestaetigen(
                $"Turnier [bold]{Markup.Escape(instanz.Name)}[/] jetzt vollständig simulieren?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var lauf = await AnsiConsole.Status()
            .StartAsync("Turnier wird simuliert...", async _ =>
                await simulationsService.TurnierSimulierenAsync(instanz.Id, modus, null, ct));

        var dauer = lauf.EndZeitpunkt.HasValue
            ? (lauf.EndZeitpunkt.Value - lauf.StartZeitpunkt).TotalMilliseconds
            : 0;

        KonsolenHilfsmittel.ErfolgAnzeigen(
            $"Simulation abgeschlossen (Seed: {lauf.ZufallsSeed}, Dauer: {dauer:F0} ms).");
        KonsolenHilfsmittel.Pause();
    }

    // ── Protokoll ─────────────────────────────────────────────────────────

    private async Task ProtokollAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Simulationsprotokoll");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var laeufe = await simulationsService.LaeufeAbrufenAsync(instanz.Id, ct);

        if (!laeufe.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Simulationsläufe für dieses Turnier vorhanden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Zeitpunkt")
            .AddColumn(new TableColumn("Seed").RightAligned())
            .AddColumn("Beschreibung")
            .AddColumn(new TableColumn("Dauer").RightAligned());

        foreach (var l in laeufe)
        {
            var dauer = l.EndZeitpunkt.HasValue
                ? $"{(l.EndZeitpunkt.Value - l.StartZeitpunkt).TotalMilliseconds:F0} ms"
                : "[grey]läuft[/]";

            tabelle.AddRow(
                $"[grey]{l.Id}[/]",
                l.StartZeitpunkt.ToLocalTime().ToString("dd.MM. HH:mm:ss"),
                $"[blue]{l.ZufallsSeed}[/]",
                Markup.Escape(l.Beschreibung ?? "–"),
                dauer);
        }

        AnsiConsole.MarkupLine($"[grey]{laeufe.Count} Lauf/Läufe[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    // ── Liveticker ────────────────────────────────────────────────────────

    private async Task LivetickerAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("📺 Liveticker");

        var (instanz, runde) = await TurnierUndRundeAuswaehlenAsync(ct);
        if (instanz is null || runde is null) return;

        var spiele = await spielErgebnisService.AlleNachRundeAsync(runde.Id, ct);
        var abgeschlossene = spiele.Where(s => s.Status == SpielStatus.Abgeschlossen).ToList();

        if (!abgeschlossene.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine abgeschlossenen Spiele in dieser Runde.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var spiel = AnsiConsole.Prompt(
            new SelectionPrompt<Spiel?>()
                .Title("Spiel:")
                .UseConverter(s =>
                {
                    if (s is null) return "← Zurück";
                    var erg = s.Ergebnis is not null
                        ? $"  [{s.Ergebnis.HeimToreRegulaer}:{s.Ergebnis.AuswaertsToreRegulaer}]"
                        : "";
                    return $"#{s.Spielnummer}  {s.HeimMannschaft?.Name ?? "?"} vs {s.AuswaertsMannschaft?.Name ?? "?"}{erg}";
                })
                .AddChoices(abgeschlossene.Cast<Spiel?>().Append(null)));
        if (spiel is null) return;

        var mitEreignissen = await spielErgebnisService.FindenNachIdMitEreignissenAsync(spiel.Id, ct);
        if (mitEreignissen is null) return;

        var heimName     = mitEreignissen.HeimMannschaft?.Name     ?? "Heim";
        var auswaertsName = mitEreignissen.AuswaertsMannschaft?.Name ?? "Auswärts";
        var ergebnis     = mitEreignissen.Ergebnis;

        if (!mitEreignissen.Ereignisse.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen(
                "Für dieses Spiel sind keine Ereignisse gespeichert (Schnellsimulation verwendet).");
            if (ergebnis is not null)
            {
                AnsiConsole.WriteLine();
                var panel = new Panel(
                    $"[bold]{Markup.Escape(heimName)}[/]   " +
                    $"[bold yellow]{ergebnis.HeimToreRegulaer} : {ergebnis.AuswaertsToreRegulaer}[/]   " +
                    $"[bold]{Markup.Escape(auswaertsName)}[/]")
                    .Header("[blue]Endstand[/]").Border(BoxBorder.Rounded);
                AnsiConsole.Write(panel);
            }
            KonsolenHilfsmittel.Pause();
            return;
        }

        var modus = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Anzeigemodus:")
                .AddChoices("🎬 Live (mit Zeitverzögerung)", "📋 Protokoll (sofort)"));
        bool istLive = modus.StartsWith("🎬");

        await GespeicherteEreignisseZeigenAsync(mitEreignissen, istLive, ct);
        KonsolenHilfsmittel.Pause();
    }

    private static async Task GespeicherteEreignisseZeigenAsync(Spiel spiel, bool mitVerzoegerung, CancellationToken ct)
    {
        var heimName      = spiel.HeimMannschaft?.Name     ?? "Heim";
        var auswaertsName = spiel.AuswaertsMannschaft?.Name ?? "Auswärts";
        var ergebnis      = spiel.Ergebnis;

        // Phasengrenzen identisch zu ErgebnisAnzeigen
        static int Phase(int min) => min switch { <= 45 => 1, <= 90 => 2, <= 105 => 3, _ => 4 };
        static string PhaseLabel(int p) => p switch
        {
            1 => "1. Halbzeit",
            2 => "2. Halbzeit",
            3 => "1. Hälfte Verlängerung",
            _ => "2. Hälfte Verlängerung"
        };

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule(
            $"[bold]{Markup.Escape(heimName)}[/]  [grey]vs[/]  [bold]{Markup.Escape(auswaertsName)}[/]")
            .Centered());
        AnsiConsole.WriteLine();

        var ereignisTabelle = new Table()
            .Border(TableBorder.Minimal)
            .AddColumn(new TableColumn($"[bold]{Markup.Escape(heimName)}[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]'[/]").Centered())
            .AddColumn(new TableColumn($"[bold]{Markup.Escape(auswaertsName)}[/]").LeftAligned());

        int aktuellePhase = 0;
        int heimScore = 0, auswaertsScore = 0;

        foreach (var ev in spiel.Ereignisse.OrderBy(e => e.Minute))
        {
            if (ct.IsCancellationRequested) break;

            int phase = Phase(ev.Minute);
            if (phase != aktuellePhase)
            {
                ereignisTabelle.AddRow(
                    new Markup("[grey dim]────────────────[/]"),
                    new Markup($"[grey]{PhaseLabel(phase)}[/]"),
                    new Markup("[grey dim]────────────────[/]"));
                aktuellePhase = phase;
            }

            bool istHeim = ev.MannschaftId == spiel.HeimMannschaftId;

            string symbol = ev.EreignisArt switch
            {
                EreignisArt.Tor        => "⚽",
                EreignisArt.GoldenGoal => "⭐",
                EreignisArt.GelbeKarte => "🟨",
                EreignisArt.RoteKarte  => "🟥",
                _                      => "•"
            };

            if (ev.EreignisArt is EreignisArt.Tor or EreignisArt.GoldenGoal)
            {
                if (istHeim) heimScore++; else auswaertsScore++;
            }

            string scoreAktuell = $"[grey]({heimScore}:{auswaertsScore})[/]";
            string beschreibung = string.IsNullOrWhiteSpace(ev.Beschreibung)
                ? symbol
                : $"{symbol} {Markup.Escape(ev.Beschreibung)}";
            string minute = $"[grey]{ev.Minute:D2}'[/]";

            if (istHeim)
                ereignisTabelle.AddRow($"{beschreibung} {scoreAktuell}", minute, string.Empty);
            else
                ereignisTabelle.AddRow(string.Empty, minute, $"{beschreibung} {scoreAktuell}");

            if (mitVerzoegerung)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(ereignisTabelle);
                await Task.Delay(800, ct).ConfigureAwait(false);
            }
        }

        if (!mitVerzoegerung)
            AnsiConsole.Write(ereignisTabelle);

        // Endstand
        AnsiConsole.WriteLine();
        if (ergebnis is not null)
        {
            int heimGes  = ergebnis.HeimToreRegulaer + (ergebnis.HeimToreVerlaengerung ?? 0);
            int gastGes  = ergebnis.AuswaertsToreRegulaer + (ergebnis.AuswaertsToreVerlaengerung ?? 0);
            var suffix   = ergebnis.EntscheidungArt switch
            {
                EntscheidungArt.Verlaengerung     => " [grey]n.V.[/]",
                EntscheidungArt.GoldenGoal        => " [grey]GG[/]",
                EntscheidungArt.Elfmeterschiessen => $" [grey]i.E. ({ergebnis.HeimElfmetertore}:{ergebnis.AuswaertsElfmetertore})[/]",
                EntscheidungArt.Losentscheid      => " [grey]Los[/]",
                _                                 => ""
            };
            var panel = new Panel(
                $"[bold]{Markup.Escape(heimName)}[/]   [bold yellow]{heimGes} : {gastGes}[/]{suffix}   [bold]{Markup.Escape(auswaertsName)}[/]")
                .Header("[blue]Endstand[/]").Border(BoxBorder.Rounded);
            AnsiConsole.Write(panel);
        }
    }

    // ── Darstellung ───────────────────────────────────────────────────────

    private static void SimulationsErgebnisseAnzeigen(
        IReadOnlyList<(Spiel Spiel, SimulationsErgebnis Ergebnis)> ergebnisse,
        SimulationsModus modus)
    {
        if (modus == SimulationsModus.AusfuehrlicheSimulation)
        {
            foreach (var (spiel, ergebnis) in ergebnisse)
            {
                AnsiConsole.Write(new Rule(
                    $"[grey]#{spiel.Spielnummer}  " +
                    $"{Markup.Escape(spiel.HeimMannschaft?.Name ?? "?")} – " +
                    $"{Markup.Escape(spiel.AuswaertsMannschaft?.Name ?? "?")}[/]")
                    .LeftJustified());
                ErgebnisAnzeigen(
                    spiel.HeimMannschaft?.Name ?? "Heim",
                    spiel.AuswaertsMannschaft?.Name ?? "Auswärts",
                    ergebnis);
                AnsiConsole.WriteLine();
            }
        }
        else
        {
            var tabelle = new Table().Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[grey]#[/]").RightAligned())
                .AddColumn("Heim")
                .AddColumn(new TableColumn("Ergebnis").Centered())
                .AddColumn("Auswärts");

            foreach (var (spiel, e) in ergebnisse)
            {
                int heimGes = e.HeimToreRegulaer + (e.HeimToreVerlaengerung ?? 0);
                int gastGes = e.AuswaertsToreRegulaer + (e.AuswaertsToreVerlaengerung ?? 0);
                var suffix = e.EntscheidungArt switch
                {
                    EntscheidungArt.Verlaengerung     => " [grey]n.V.[/]",
                    EntscheidungArt.GoldenGoal        => " [grey]GG[/]",
                    EntscheidungArt.SilverGoal        => " [grey]SG[/]",
                    EntscheidungArt.Elfmeterschiessen => $" [grey]i.E. ({e.HeimElfmetertore}:{e.AuswaertsElfmetertore})[/]",
                    EntscheidungArt.Losentscheid      => " [grey]Los[/]",
                    _                                 => ""
                };
                tabelle.AddRow(
                    $"[grey]{spiel.Spielnummer}[/]",
                    Markup.Escape(spiel.HeimMannschaft?.Name ?? "?"),
                    $"[bold]{heimGes} : {gastGes}[/]{suffix}",
                    Markup.Escape(spiel.AuswaertsMannschaft?.Name ?? "?"));
            }

            AnsiConsole.Write(tabelle);
        }
    }

    private static void ErgebnisAnzeigen(string heim, string auswaerts, SimulationsErgebnis e)
    {
        string scoreHaupt = $"{e.HeimToreRegulaer} : {e.AuswaertsToreRegulaer}";
        string scoreExt   = "";

        if (e.EntscheidungArt != EntscheidungArt.RegulaereSpielzeit)
        {
            scoreExt = e.EntscheidungArt switch
            {
                EntscheidungArt.Verlaengerung when e.HeimToreVerlaengerung.HasValue
                    => $" n.V. ({e.HeimToreVerlaengerung}:{e.AuswaertsToreVerlaengerung})",
                EntscheidungArt.GoldenGoal
                    => " Golden Goal",
                EntscheidungArt.Elfmeterschiessen when e.HeimElfmetertore.HasValue
                    => $" i.E. ({e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
                EntscheidungArt.Losentscheid
                    => " Losentscheid",
                _ => $" ({e.EntscheidungArt})"
            };
        }

        var panel = new Panel(
            $"[bold]{Markup.Escape(heim)}[/]   [bold yellow]{scoreHaupt}[/][bold]{Markup.Escape(scoreExt)}[/]   [bold]{Markup.Escape(auswaerts)}[/]")
            .Header("[blue]Simulations-Ergebnis[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);

        if (e.Ereignisse.Any())
        {
            AnsiConsole.WriteLine();

            var ereignisTabelle = new Table()
                .Border(TableBorder.Minimal)
                .AddColumn(new TableColumn($"[bold]{Markup.Escape(heim)}[/]").RightAligned())
                .AddColumn(new TableColumn("[grey]'[/]").Centered())
                .AddColumn(new TableColumn($"[bold]{Markup.Escape(auswaerts)}[/]").LeftAligned());

            static int Phase(int min) => min switch { <= 45 => 1, <= 90 => 2, <= 105 => 3, _ => 4 };
            static string PhaseLabel(int p) => p switch
            {
                1 => "1. Halbzeit",
                2 => "2. Halbzeit",
                3 => "1. Hälfte Verlängerung",
                _ => "2. Hälfte Verlängerung"
            };

            int aktuellePhase = 0;

            foreach (var ev in e.Ereignisse.OrderBy(x => x.Minute))
            {
                int phase = Phase(ev.Minute);
                if (phase != aktuellePhase)
                {
                    ereignisTabelle.AddRow(
                        new Markup("[grey dim]────────────────[/]"),
                        new Markup($"[grey]{PhaseLabel(phase)}[/]"),
                        new Markup("[grey dim]────────────────[/]"));
                    aktuellePhase = phase;
                }

                string symbol = ev.Art switch
                {
                    EreignisArt.Tor        => "⚽",
                    EreignisArt.GoldenGoal => "⭐",
                    EreignisArt.GelbeKarte => "🟨",
                    EreignisArt.RoteKarte  => "🟥",
                    _                      => "•"
                };
                string eintrag = $"{symbol} {Markup.Escape(ev.Beschreibung)}";
                string minute  = $"[grey]{ev.Minute:D2}'[/]";

                if (ev.IstHeim)
                    ereignisTabelle.AddRow(eintrag, minute, string.Empty);
                else
                    ereignisTabelle.AddRow(string.Empty, minute, eintrag);
            }

            if (e.EntscheidungArt == EntscheidungArt.Elfmeterschiessen)
                ereignisTabelle.AddRow(
                    new Markup("[grey dim]────────────────[/]"),
                    new Markup("[grey]Elfmeterschießen[/]"),
                    new Markup("[grey dim]────────────────[/]"));

            AnsiConsole.Write(ereignisTabelle);
        }
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private async Task<(TurnierInstanz?, TurnierRunde?)> TurnierUndRundeAuswaehlenAsync(CancellationToken ct)
    {
        var instanz = await TurnierMitRundenAsync(ct);
        if (instanz is null) return (null, null);

        if (!instanz.Runden.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Dieses Turnier hat noch keine Runden.");
            KonsolenHilfsmittel.Pause();
            return (instanz, null);
        }

        var runde = AnsiConsole.Prompt(
            new SelectionPrompt<TurnierRunde>()
                .Title("Runde:")
                .UseConverter(r => $"{r.Reihenfolge}. {Markup.Escape(r.Name)} [[{r.Rundenart}]]")
                .AddChoices(instanz.Runden.OrderBy(r => r.Reihenfolge)));

        return (instanz, runde);
    }

    private async Task<TurnierInstanz?> TurnierAuswaehlenAsync(CancellationToken ct)
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
                .EnableSearch()
                .AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
    }

    private async Task<TurnierInstanz?> TurnierMitRundenAsync(CancellationToken ct)
    {
        var basis = await TurnierAuswaehlenAsync(ct);
        if (basis is null) return null;
        return await instanzService.FindenNachIdMitRundenAsync(basis.Id, ct);
    }

    private static SimulationsModus ModusAuswaehlen()
        => AnsiConsole.Prompt(
            new SelectionPrompt<SimulationsModus>()
                .Title("Simulationsmodus:")
                .UseConverter(m => m switch
                {
                    SimulationsModus.SchnellSimulation     => "Schnellsimulation (nur Endstand)",
                    SimulationsModus.AusfuehrlicheSimulation => "Ausführliche Simulation (mit Ereignissen)",
                    _ => m.ToString()
                })
                .AddChoices(SimulationsModus.SchnellSimulation, SimulationsModus.AusfuehrlicheSimulation));

}
