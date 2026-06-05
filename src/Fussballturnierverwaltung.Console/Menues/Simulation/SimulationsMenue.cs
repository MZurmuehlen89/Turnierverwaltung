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
        RundeSimulieren,
        TurnierSimulieren,
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
                        Option.Freundschaftsspiel => "⚽ Einzelspiel (Freundschaftsspiel)",
                        Option.SpielSimulieren    => "Ein Turnierspiel simulieren",
                        Option.RundeSimulieren    => "Ganze Runde simulieren",
                        Option.TurnierSimulieren  => "Ganzes Turnier simulieren",
                        Option.Protokoll          => "Simulationsprotokoll anzeigen",
                        Option.Zurueck            => "← Zurück",
                        _                         => o.ToString()
                    })
                    .AddChoices(Option.Freundschaftsspiel, Option.SpielSimulieren,
                                Option.RundeSimulieren, Option.TurnierSimulieren,
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
            case Option.Freundschaftsspiel: await FreundschaftsspielAsync(ct);   break;
            case Option.SpielSimulieren:    await SpielSimulierenAsync(ct);      break;
            case Option.RundeSimulieren:    await RundeSimulierenAsync(ct);      break;
            case Option.TurnierSimulieren:  await TurnierSimulierenAsync(ct);    break;
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
            new SelectionPrompt<Spiel>()
                .Title("Spiel:")
                .UseConverter(s => $"#{s.Spielnummer}  {s.HeimMannschaft?.Name ?? "?"} vs {s.AuswaertsMannschaft?.Name ?? "?"}")
                .AddChoices(offene));

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

    // ── Runde ─────────────────────────────────────────────────────────────

    private async Task RundeSimulierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Runde simulieren");

        var (instanz, runde) = await TurnierUndRundeAuswaehlenAsync(ct);
        if (instanz is null || runde is null) return;

        var modus = ModusAuswaehlen();

        if (!KonsolenHilfsmittel.Bestaetigen(
                $"Alle offenen Spiele der Runde [bold]{Markup.Escape(runde.Name)}[/] simulieren?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        int simuliert = await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Spiele werden simuliert...");
                int n = await simulationsService.RundeSimulierenAsync(runde.Id, modus, null, ct);
                task.Increment(100);
                return n;
            });

        KonsolenHilfsmittel.ErfolgAnzeigen($"{simuliert} Spiel(e) simuliert.");
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

    // ── Darstellung ───────────────────────────────────────────────────────

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
            $"[bold]{Markup.Escape(heim)}[/]   [bold yellow]{scoreHaupt}[bold]{Markup.Escape(scoreExt)}[/]   [bold]{Markup.Escape(auswaerts)}[/]")
            .Header("[blue]Simulations-Ergebnis[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);

        if (e.Ereignisse.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Ereignisse:[/]");

            foreach (var ev in e.Ereignisse.OrderBy(x => x.Minute))
            {
                string seite = ev.IstHeim ? $"[green]{Markup.Escape(heim)}[/]" : $"[red]{Markup.Escape(auswaerts)}[/]";
                string symbol = ev.Art switch
                {
                    EreignisArt.Tor        => "⚽",
                    EreignisArt.GoldenGoal => "⭐",
                    EreignisArt.GelbeKarte => "🟨",
                    EreignisArt.RoteKarte  => "🟥",
                    _                      => "•"
                };
                AnsiConsole.MarkupLine($"  [grey]{ev.Minute:D2}'[/] {symbol} {seite} – {Markup.Escape(ev.Beschreibung)}");
            }
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
                .UseConverter(r => $"{r.Reihenfolge}. {r.Name} [{r.Rundenart}]")
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
            new SelectionPrompt<TurnierInstanz>()
                .Title("Turnier:")
                .UseConverter(ti => $"{ti.Name} ({ti.SaisonOderJahr})")
                .EnableSearch()
                .AddChoices(instanzen));
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
