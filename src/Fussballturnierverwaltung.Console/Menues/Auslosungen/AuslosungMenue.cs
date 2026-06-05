using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Auslosungen;

public class AuslosungMenue(
    AuslosungService auslosungService,
    TurnierInstanzService instanzService,
    MannschaftService mannschaftService)
{
    private enum Hauptoption
    {
        AlleAnzeigen,
        NeuAnlegen,
        Verwalten,
        Zurueck
    }

    private enum Verwaltungsoption
    {
        LostopfHinzufuegen,
        MannschaftHinzufuegen,
        MannschaftEntfernen,
        RegelnKonfigurieren,
        Durchfuehren,
        ErgebnisAnzeigen,
        Zuruecksetzen,
        Loeschen,
        Zurueck
    }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🎯 Auslosungen");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Hauptoption>()
                    .UseConverter(o => o switch
                    {
                        Hauptoption.AlleAnzeigen => "Auslosungen anzeigen",
                        Hauptoption.NeuAnlegen   => "Neue Auslosung anlegen",
                        Hauptoption.Verwalten    => "Auslosung verwalten",
                        Hauptoption.Zurueck      => "← Zurück",
                        _                        => o.ToString()
                    })
                    .AddChoices(Hauptoption.AlleAnzeigen, Hauptoption.NeuAnlegen,
                                Hauptoption.Verwalten, Hauptoption.Zurueck));

            if (auswahl == Hauptoption.Zurueck) break;

            try { await HauptaktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task HauptaktionAusfuehrenAsync(Hauptoption auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Hauptoption.AlleAnzeigen: await AlleAnzeigenAsync(ct); break;
            case Hauptoption.NeuAnlegen:   await NeuAnlegenAsync(ct);   break;
            case Hauptoption.Verwalten:    await VerwaltenAsync(ct);     break;
        }
    }

    // ── Alle anzeigen ─────────────────────────────────────────────────────

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        KonsolenHilfsmittel.Kopf($"Auslosungen: {Markup.Escape(instanz.Name)}");
        var auslosungen = await auslosungService.AlleNachInstanzAbrufenAsync(instanz.Id, ct);

        if (!auslosungen.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Noch keine Auslosungen für dieses Turnier.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn("Runde")
            .AddColumn(new TableColumn("Art").Centered())
            .AddColumn(new TableColumn("Status").Centered())
            .AddColumn(new TableColumn("Paarungen").RightAligned());

        foreach (var a in auslosungen)
        {
            var statusMarkup = a.Status switch
            {
                AuslosungsStatus.Abgeschlossen => "[green]Abgeschlossen[/]",
                AuslosungsStatus.Laufend       => "[yellow]Laufend[/]",
                AuslosungsStatus.Abgebrochen   => "[red]Abgebrochen[/]",
                _                              => "[grey]Vorbereitung[/]"
            };
            tabelle.AddRow(
                $"[grey]{a.Id}[/]",
                Markup.Escape(a.Name),
                Markup.Escape(a.TurnierRunde?.Name ?? "?"),
                a.Auslosungsart.ToString(),
                statusMarkup,
                $"{a.Ergebnisse.Count}");
        }

        AnsiConsole.MarkupLine($"[grey]{auslosungen.Count} Auslosung(en)[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    // ── Neu anlegen ───────────────────────────────────────────────────────

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neue Auslosung anlegen");

        var instanz = await TurnierMitRundenAuswaehlenAsync(ct);
        if (instanz is null) return;

        if (!instanz.Runden.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Dieses Turnier hat noch keine Runden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var runde = AnsiConsole.Prompt(
            new SelectionPrompt<TurnierRunde>()
                .Title("Für welche Runde?")
                .UseConverter(r => $"{r.Reihenfolge}. {Markup.Escape(r.Name)} [[{r.Rundenart}]] ({r.EingangsMannschaftsanzahl} Teams)")
                .AddChoices(instanz.Runden.OrderBy(r => r.Reihenfolge)));

        var name = KonsolenHilfsmittel.TextEingeben("Name der Auslosung", $"Auslosung {runde.Name}");

        var art = AnsiConsole.Prompt(
            new SelectionPrompt<Auslosungsart>()
                .Title("Auslosungsart:")
                .UseConverter(a => a switch
                {
                    Auslosungsart.Zufall => "Zufällig (alle Teams in einem Topf)",
                    Auslosungsart.NachToepfen => "Nach Töpfen (Topf 1 vs Topf 2, ...)",
                    Auslosungsart.NachSetzliste => "Nach Setzliste (Gesetzte vs Ungesetzte)",
                    Auslosungsart.GruppensiegerGegenGruppenzweite => "Gruppensieger gegen Gruppenzweite",
                    _ => a.ToString()
                })
                .AddChoices(Auslosungsart.Zufall, Auslosungsart.NachToepfen,
                            Auslosungsart.NachSetzliste, Auslosungsart.GruppensiegerGegenGruppenzweite));

        var eingabe = new AuslosungErstellenEingabe(
            instanz.Id, runde.Id, name, art, IstAutomatisch: true);

        var auslosung = await auslosungService.ErstellenAsync(eingabe, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Auslosung '{auslosung.Name}' angelegt (ID {auslosung.Id}).");
        AnsiConsole.MarkupLine("[grey]Nächster Schritt: Lostöpfe und Mannschaften konfigurieren.[/]");
        KonsolenHilfsmittel.Pause();
    }

    // ── Verwalten ─────────────────────────────────────────────────────────

    private async Task VerwaltenAsync(CancellationToken ct)
    {
        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var auslosungen = await auslosungService.AlleNachInstanzAbrufenAsync(instanz.Id, ct);
        if (!auslosungen.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Auslosungen vorhanden. Bitte zuerst anlegen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var ausgewaehlt = AnsiConsole.Prompt(
            new SelectionPrompt<Auslosung?>()
                .Title("Auslosung auswählen:")
                .UseConverter(a => a is null ? "← Zurück" : $"{Markup.Escape(a.Name)} [[{a.Status}]] – {Markup.Escape(a.TurnierRunde?.Name ?? "?")}")
                .EnableSearch()
                .AddChoices(auslosungen.Cast<Auslosung?>().Append(null)));
        if (ausgewaehlt is null) return;

        await VerwaltungsmenueAnzeigenAsync(ausgewaehlt.Id, ct);
    }

    private async Task VerwaltungsmenueAnzeigenAsync(int auslosungId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var auslosung = await auslosungService.FindenNachIdAsync(auslosungId, ct);
            if (auslosung is null) break;

            KonsolenHilfsmittel.Kopf($"Auslosung: {Markup.Escape(auslosung.Name)}");
            StatusAnzeigen(auslosung);
            AnsiConsole.WriteLine();

            var optionen = new List<Verwaltungsoption>
            {
                Verwaltungsoption.LostopfHinzufuegen,
                Verwaltungsoption.MannschaftHinzufuegen,
                Verwaltungsoption.MannschaftEntfernen,
                Verwaltungsoption.RegelnKonfigurieren,
                Verwaltungsoption.Durchfuehren,
                Verwaltungsoption.ErgebnisAnzeigen,
                Verwaltungsoption.Zuruecksetzen,
                Verwaltungsoption.Loeschen,
                Verwaltungsoption.Zurueck
            };

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Verwaltungsoption>()
                    .UseConverter(o => o switch
                    {
                        Verwaltungsoption.LostopfHinzufuegen    => "Lostopf hinzufügen",
                        Verwaltungsoption.MannschaftHinzufuegen => "Mannschaft zu Lostopf hinzufügen",
                        Verwaltungsoption.MannschaftEntfernen   => "Mannschaft aus Lostopf entfernen",
                        Verwaltungsoption.RegelnKonfigurieren   => "Regeln konfigurieren",
                        Verwaltungsoption.Durchfuehren          => "Auslosung durchführen",
                        Verwaltungsoption.ErgebnisAnzeigen      => "Ergebnis anzeigen",
                        Verwaltungsoption.Zuruecksetzen         => "Auslosung zurücksetzen",
                        Verwaltungsoption.Loeschen              => "Auslosung löschen",
                        Verwaltungsoption.Zurueck               => "← Zurück",
                        _                                       => o.ToString()
                    })
                    .AddChoices(optionen));

            if (auswahl == Verwaltungsoption.Zurueck) break;

            try
            {
                bool sollBeenden = await VerwaltungsaktionAusfuehrenAsync(auswahl, auslosung, ct);
                if (sollBeenden) break;
            }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task<bool> VerwaltungsaktionAusfuehrenAsync(
        Verwaltungsoption auswahl, Auslosung auslosung, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Verwaltungsoption.LostopfHinzufuegen:    await LostopfHinzufuegenAsync(auslosung.Id, ct); break;
            case Verwaltungsoption.MannschaftHinzufuegen: await MannschaftHinzufuegenAsync(auslosung, ct);  break;
            case Verwaltungsoption.MannschaftEntfernen:   await MannschaftEntfernenAsync(auslosung, ct);    break;
            case Verwaltungsoption.RegelnKonfigurieren:   await RegelnKonfigurierenAsync(auslosung, ct);    break;
            case Verwaltungsoption.Durchfuehren:          await DurchfuehrenAsync(auslosung, ct);           break;
            case Verwaltungsoption.ErgebnisAnzeigen:      await ErgebnisAnzeigenAsync(auslosung, ct);       break;
            case Verwaltungsoption.Zuruecksetzen:         await ZuruecksetzenAsync(auslosung.Id, ct);       break;
            case Verwaltungsoption.Loeschen:
                bool geloescht = await LoeschenAsync(auslosung, ct);
                if (geloescht) return true;
                break;
        }
        return false;
    }

    // ── Teilaktionen ──────────────────────────────────────────────────────

    private async Task LostopfHinzufuegenAsync(int auslosungId, CancellationToken ct)
    {
        var name = KonsolenHilfsmittel.TextEingeben("Name des Lostopfs (z.B. 'Topf 1')");
        await auslosungService.LostopfHinzufuegenAsync(auslosungId, name, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Lostopf '{name}' hinzugefügt.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task MannschaftHinzufuegenAsync(Auslosung auslosung, CancellationToken ct)
    {
        if (!auslosung.Lostoepfe.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Bitte zuerst mindestens einen Lostopf anlegen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var lostopf = AuswaehlenLostopf(auslosung);
        if (lostopf is null) return;

        var bereitsVergeben = auslosung.Lostoepfe
            .SelectMany(l => l.Eintraege)
            .Select(e => e.MannschaftId)
            .ToHashSet();

        var alleMannschaften = await mannschaftService.AlleAbrufenMitLandAsync(ct);
        var verfuegbar = alleMannschaften.Where(m => !bereitsVergeben.Contains(m.Id)).ToList();

        if (!verfuegbar.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Alle Mannschaften sind bereits in Lostöpfen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var mann = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaft?>()
                .Title("Mannschaft:")
                .UseConverter(m => m is null ? "← Zurück" : $"{m.Name} ({m.Land?.FifaCode ?? "?"})")
                .EnableSearch().PageSize(15)
                .AddChoices(verfuegbar.Cast<Mannschaft?>().Append(null)));
        if (mann is null) return;

        int? setzPosition = null;
        if (auslosung.Auslosungsart == Auslosungsart.NachSetzliste &&
            AnsiConsole.Confirm("Ist diese Mannschaft gesetzt?", defaultValue: false))
        {
            setzPosition = AnsiConsole.Prompt(new TextPrompt<int>("Setzposition (1 = höchste):").Validate(n => n >= 1));
        }

        await auslosungService.MannschaftZuLostopfHinzufuegenAsync(
            auslosung.Id, lostopf.Id, mann.Id, setzPosition, ct);

        KonsolenHilfsmittel.ErfolgAnzeigen($"'{mann.Name}' zu '{lostopf.Name}' hinzugefügt.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task MannschaftEntfernenAsync(Auslosung auslosung, CancellationToken ct)
    {
        var alleEintraege = auslosung.Lostoepfe.SelectMany(l => l.Eintraege).ToList();
        if (!alleEintraege.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Mannschaften in den Lostöpfen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var eintrag = AnsiConsole.Prompt(
            new SelectionPrompt<LostopfEintrag?>()
                .Title("Mannschaft entfernen:")
                .UseConverter(e => e is null ? "← Zurück" : $"{e.Mannschaft?.Name ?? $"ID {e.MannschaftId}"} (aus: {e.Lostopf?.Name ?? "?"})")
                .AddChoices(alleEintraege.Cast<LostopfEintrag?>().Append(null)));
        if (eintrag is null) return;

        var lostopf = auslosung.Lostoepfe.First(l => l.Id == eintrag.LostopfId);
        await auslosungService.MannschaftAusLostopfEntfernenAsync(
            auslosung.Id, eintrag.LostopfId, eintrag.MannschaftId, ct);

        KonsolenHilfsmittel.ErfolgAnzeigen($"Mannschaft aus '{lostopf.Name}' entfernt.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task RegelnKonfigurierenAsync(Auslosung auslosung, CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Regeln konfigurieren");

        var alleRegelarten = new[]
        {
            AuslosungsRegelArt.KeinGleichesLand,
            AuslosungsRegelArt.HeimrechtNachLos,
            AuslosungsRegelArt.HeimrechtNachSetzliste,
            AuslosungsRegelArt.FreilosMoeglich
        };

        AnsiConsole.MarkupLine("[grey]Aktive Regeln:[/]");
        foreach (var r in auslosung.Regeln.Where(r => r.IstAktiv))
            AnsiConsole.MarkupLine($"  [green]✓[/] {r.Regelart}");
        if (!auslosung.Regeln.Any(r => r.IstAktiv))
            AnsiConsole.MarkupLine("  [grey](keine)[/]");
        AnsiConsole.WriteLine();

        var regelart = AnsiConsole.Prompt(
            new SelectionPrompt<AuslosungsRegelArt?>()
                .Title("Regel umschalten:")
                .UseConverter(a => a switch
                {
                    null                                      => "← Zurück",
                    AuslosungsRegelArt.KeinGleichesLand       => "Kein gleiches Land",
                    AuslosungsRegelArt.HeimrechtNachLos       => "Heimrecht nach Los",
                    AuslosungsRegelArt.HeimrechtNachSetzliste => "Heimrecht nach Setzliste (Topf 1 = Heim)",
                    AuslosungsRegelArt.FreilosMoeglich        => "Freilos möglich (bei ungerader Teamanzahl)",
                    _                                         => a.ToString()
                })
                .AddChoices(alleRegelarten.Select(r => (AuslosungsRegelArt?)r).Append(null)));
        if (regelart is null) return;

        var istAktiv = auslosung.Regeln.Any(r => r.Regelart == regelart.Value && r.IstAktiv);
        if (istAktiv)
        {
            await auslosungService.RegelDeaktivierenAsync(auslosung.Id, regelart.Value, ct);
            KonsolenHilfsmittel.ErfolgAnzeigen($"Regel '{regelart}' deaktiviert.");
        }
        else
        {
            await auslosungService.RegelHinzufuegenOderAktivierenAsync(auslosung.Id, regelart.Value, regelart.ToString()!, ct);
            KonsolenHilfsmittel.ErfolgAnzeigen($"Regel '{regelart}' aktiviert.");
        }
        KonsolenHilfsmittel.Pause();
    }

    private async Task DurchfuehrenAsync(Auslosung auslosung, CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf($"Auslosung durchführen: {Markup.Escape(auslosung.Name)}");

        var teamsInToepfen = auslosung.Lostoepfe.SelectMany(l => l.Eintraege).Count();
        AnsiConsole.MarkupLine($"Lostöpfe: [bold]{auslosung.Lostoepfe.Count}[/] | Teams: [bold]{teamsInToepfen}[/]");

        if (!KonsolenHilfsmittel.Bestaetigen("Auslosung jetzt durchführen?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var ergebnisse = await AnsiConsole.Status()
            .StartAsync("Auslosung wird durchgeführt...", async _ =>
                await auslosungService.AusfuehrenAsync(auslosung.Id, ct));

        KonsolenHilfsmittel.ErfolgAnzeigen($"Auslosung abgeschlossen: {ergebnisse.Count} Paarung(en).");
        AnsiConsole.WriteLine();
        ErgebnisTabelle(ergebnisse);
        KonsolenHilfsmittel.Pause();
    }

    private async Task ErgebnisAnzeigenAsync(Auslosung auslosung, CancellationToken ct)
    {
        var details = await auslosungService.FindenNachIdAsync(auslosung.Id, ct);
        if (details is null) return;

        KonsolenHilfsmittel.Kopf($"Ergebnis: {Markup.Escape(details.Name)}");

        if (!details.Ergebnisse.Any())
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Noch keine Ergebnisse. Bitte Auslosung erst durchführen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        ErgebnisTabelle(details.Ergebnisse.ToList());
        KonsolenHilfsmittel.Pause();
    }

    private async Task ZuruecksetzenAsync(int auslosungId, CancellationToken ct)
    {
        if (!KonsolenHilfsmittel.Bestaetigen("Auslosung wirklich zurücksetzen? Alle Ergebnisse werden gelöscht."))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        await auslosungService.ZuruecksetzenAsync(auslosungId, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen("Auslosung zurückgesetzt.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task<bool> LoeschenAsync(Auslosung auslosung, CancellationToken ct)
    {
        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(auslosung.Name)}'[/] wirklich löschen?"))
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen.");
            KonsolenHilfsmittel.Pause();
            return false;
        }

        await auslosungService.LoeschenAsync(auslosung.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{auslosung.Name}' gelöscht.");
        KonsolenHilfsmittel.Pause();
        return true;
    }

    // ── Darstellung ───────────────────────────────────────────────────────

    private static void StatusAnzeigen(Auslosung auslosung)
    {
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Status:[/]",       auslosung.Status.ToString());
        grid.AddRow("[grey]Art:[/]",          auslosung.Auslosungsart.ToString());
        grid.AddRow("[grey]Runde:[/]",        Markup.Escape(auslosung.TurnierRunde?.Name ?? "?"));
        grid.AddRow("[grey]Lostöpfe:[/]",     auslosung.Lostoepfe.Count.ToString());
        grid.AddRow("[grey]Teams gesamt:[/]", auslosung.Lostoepfe.SelectMany(l => l.Eintraege).Count().ToString());

        var aktiveRegeln = auslosung.Regeln.Where(r => r.IstAktiv).Select(r => r.Regelart.ToString()).ToList();
        grid.AddRow("[grey]Aktive Regeln:[/]", aktiveRegeln.Any() ? string.Join(", ", aktiveRegeln) : "–");

        AnsiConsole.Write(new Panel(grid));

        if (auslosung.Lostoepfe.Any())
        {
            AnsiConsole.MarkupLine("[bold]Lostöpfe:[/]");
            foreach (var topf in auslosung.Lostoepfe)
            {
                var teams = topf.Eintraege.Select(e => e.Mannschaft?.Name ?? $"ID {e.MannschaftId}");
                AnsiConsole.MarkupLine($"  [blue]{Markup.Escape(topf.Name)}[/] ({topf.Eintraege.Count} Teams): " +
                                       Markup.Escape(string.Join(", ", teams)));
            }
        }
    }

    private static void ErgebnisTabelle(IReadOnlyList<AuslosungsErgebnis> ergebnisse)
    {
        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]Paarung[/]").RightAligned())
            .AddColumn("Heim")
            .AddColumn(new TableColumn("vs").Centered())
            .AddColumn("Auswärts")
            .AddColumn(new TableColumn("Freilos").Centered());

        foreach (var e in ergebnisse)
        {
            if (e.IstFreilos)
            {
                tabelle.AddRow(
                    $"[grey]{e.PaarungsNummer}[/]",
                    Markup.Escape(e.HeimMannschaft?.Name ?? $"ID {e.HeimMannschaftId}"),
                    "[grey]–[/]", "[grey]–[/]",
                    "[yellow]Freilos[/]");
            }
            else
            {
                tabelle.AddRow(
                    $"[grey]{e.PaarungsNummer}[/]",
                    Markup.Escape(e.HeimMannschaft?.Name ?? $"ID {e.HeimMannschaftId}"),
                    "[grey]vs[/]",
                    Markup.Escape(e.AuswaertsMannschaft?.Name ?? $"ID {e.AuswaertsMannschaftId}"),
                    "[grey]–[/]");
            }
        }

        AnsiConsole.MarkupLine($"[grey]{ergebnisse.Count} Paarung(en)[/]");
        AnsiConsole.Write(tabelle);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

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

    private async Task<TurnierInstanz?> TurnierMitRundenAuswaehlenAsync(CancellationToken ct)
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
                .EnableSearch()
                .AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
        if (auswahl is null) return null;

        return await instanzService.FindenNachIdMitRundenAsync(auswahl.Id, ct);
    }

    private static Lostopf? AuswaehlenLostopf(Auslosung auslosung)
    {
        if (!auslosung.Lostoepfe.Any()) return null;
        return AnsiConsole.Prompt(
            new SelectionPrompt<Lostopf?>()
                .Title("In welchen Lostopf?")
                .UseConverter(l => l is null ? "← Zurück" : $"{l.Name} ({l.Eintraege.Count} Teams)")
                .AddChoices(auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).Cast<Lostopf?>().Append(null)));
    }
}
