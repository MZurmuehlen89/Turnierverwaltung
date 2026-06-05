using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turniere;

public class TurnierInstanzMenue(
    TurnierInstanzService instanzService,
    TurnierVorlageService vorlageService,
    RegelwerkService regelwerkService)
{
    private enum Option { AlleAnzeigen, Details, NeuAnlegen, StatusAendern, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🏆 Turniere");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen  => "Alle anzeigen",
                        Option.Details       => "Details und Runden",
                        Option.NeuAnlegen    => "Neues Turnier anlegen",
                        Option.StatusAendern => "Status ändern",
                        Option.Loeschen      => "Turnier löschen",
                        Option.Zurueck       => "← Zurück",
                        _                    => o.ToString()
                    })
                    .AddChoices(Option.AlleAnzeigen, Option.Details, Option.NeuAnlegen,
                                Option.StatusAendern, Option.Loeschen, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;
            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.AlleAnzeigen:  await AlleAnzeigenAsync(ct); break;
            case Option.Details:       await DetailsAnzeigenAsync(ct); break;
            case Option.NeuAnlegen:    await NeuAnlegenAsync(ct); break;
            case Option.StatusAendern: await StatusAendernAsync(ct); break;
            case Option.Loeschen:      await LoeschenAsync(ct); break;
        }
    }

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Turniere");
        var instanzen = await instanzService.AlleAbrufenAsync(ct);

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name").AddColumn("Saison").AddColumn("Vorlage")
            .AddColumn(new TableColumn("Status").Centered())
            .AddColumn(new TableColumn("Runden").RightAligned());

        foreach (var ti in instanzen)
        {
            var statusMarkup = ti.Status switch
            {
                TurnierStatus.Laufend      => "[green]Laufend[/]",
                TurnierStatus.Abgeschlossen => "[grey]Abgeschlossen[/]",
                TurnierStatus.Abgebrochen   => "[red]Abgebrochen[/]",
                TurnierStatus.Auslosung     => "[yellow]Auslosung[/]",
                _                           => ti.Status.ToString()
            };
            tabelle.AddRow($"[grey]{ti.Id}[/]", Markup.Escape(ti.Name), ti.SaisonOderJahr,
                ti.TurnierVorlage != null ? Markup.Escape(ti.TurnierVorlage.Name) : "[grey]–[/]",
                statusMarkup, $"{ti.Runden.Count}");
        }

        AnsiConsole.MarkupLine($"[grey]{instanzen.Count} Turnier(e)[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task DetailsAnzeigenAsync(CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (instanzen.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(new SelectionPrompt<TurnierInstanz?>()
            .Title("Turnier:").UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.SaisonOderJahr})").EnableSearch().AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
        if (auswahl is null) return;

        var details = await instanzService.FindenNachIdMitRundenAsync(auswahl.Id, ct);
        if (details is null) return;

        KonsolenHilfsmittel.Kopf($"Turnier: {details.Name} ({details.SaisonOderJahr})");

        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Status:[/]",  details.Status.ToString());
        grid.AddRow("[grey]Vorlage:[/]", details.TurnierVorlage?.Name ?? "–");
        if (details.Startdatum.HasValue) grid.AddRow("[grey]Start:[/]", details.Startdatum.Value.ToString("d"));
        if (details.Enddatum.HasValue)   grid.AddRow("[grey]Ende:[/]",  details.Enddatum.Value.ToString("d"));
        AnsiConsole.Write(new Panel(grid));

        if (details.Runden.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Runden:[/]");
            var rundenTabelle = new Table().Border(TableBorder.Simple)
                .AddColumn(new TableColumn("#").RightAligned())
                .AddColumn("Name").AddColumn("Art")
                .AddColumn(new TableColumn("Eingang").RightAligned())
                .AddColumn(new TableColumn("Ausgang").RightAligned())
                .AddColumn("Regelwerk").AddColumn("Auswärtsregel");

            foreach (var r in details.Runden.OrderBy(r => r.Reihenfolge))
                rundenTabelle.AddRow(
                    r.Reihenfolge.ToString(), Markup.Escape(r.Name), r.Rundenart.ToString(),
                    r.EingangsMannschaftsanzahl.ToString(), r.AusgangsMannschaftsanzahl.ToString(),
                    Markup.Escape(r.Regelwerk?.Name ?? "?"),
                    r.AuswaertstorRegelAktiv ? "[yellow]ja[/]" : "nein");

            AnsiConsole.Write(rundenTabelle);
        }
        else
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Runden definiert.");
        }

        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neues Turnier anlegen");

        var name   = KonsolenHilfsmittel.TextEingeben("Turniername");
        var saison = KonsolenHilfsmittel.TextEingeben("Saison/Jahr (z.B. 2024 oder 2023/24)");

        int? vorlageId = null;
        var vorlagen = await vorlageService.AlleAbrufenAsync(ct);
        if (vorlagen.Any() && AnsiConsole.Confirm("Turniervorlage verwenden?", defaultValue: true))
        {
            var vorlage = AnsiConsole.Prompt(new SelectionPrompt<TurnierVorlage?>()
                .Title("Vorlage:").UseConverter(v => v is null ? "← Keine Vorlage" : $"{v.Name} ({v.ErforderlicheMannschaftsanzahl} Teams)")
                .EnableSearch().AddChoices(vorlagen.Cast<TurnierVorlage?>().Append(null)));
            vorlageId = vorlage?.Id;
        }

        // Runden anlegen
        var regelwerke = await regelwerkService.AlleAbrufenAsync(ct);
        if (regelwerke.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Regelwerke vorhanden. Bitte zuerst Regelwerke anlegen.");
            KonsolenHilfsmittel.Pause(); return;
        }

        var runden = new List<TurnierRundeEingabe>();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Runden definieren[/] (mindestens 1, Enter = fertig nach erster Runde)");

        while (true)
        {
            AnsiConsole.Write(new Rule($"Runde {runden.Count + 1}").LeftJustified());

            if (runden.Count > 0 && !AnsiConsole.Confirm("Weitere Runde hinzufügen?", defaultValue: true)) break;

            var rundeName   = KonsolenHilfsmittel.TextEingeben("Name (z.B. Gruppenphase)");
            var rundenart = AnsiConsole.Prompt(new SelectionPrompt<Rundenart?>()
                .Title("Rundenart:")
                .UseConverter(r => r is null ? "← Abbrechen" : r.ToString())
                .AddChoices(Enum.GetValues<Rundenart>().Select(r => (Rundenart?)r).Append(null)));
            if (rundenart is null) break;
            var eingang   = AnsiConsole.Prompt(new TextPrompt<int>("Eingehende Mannschaften:").Validate(n => n >= 2));
            var ausgang   = AnsiConsole.Prompt(new TextPrompt<int>("Weiterkommende Mannschaften:").Validate(n => n >= 1 && n < eingang));
            var regelwerk = AnsiConsole.Prompt(new SelectionPrompt<Regelwerk?>()
                .Title("Regelwerk:").UseConverter(r => r is null ? "← Abbrechen" : r.Name).EnableSearch().AddChoices(regelwerke.Cast<Regelwerk?>().Append(null)));
            if (regelwerk is null) break;
            var auswaerts    = rundenart == Rundenart.KoRundeMitHinUndRueckspiel && AnsiConsole.Confirm("Auswärtstorregel aktiv?", defaultValue: false);

            var rundeEingabe = new TurnierRundeEingabe(rundeName, rundenart.Value, eingang, ausgang, regelwerk.Id, auswaerts);
            var tempListe = runden.Append(rundeEingabe).ToList();

            var validierung = TurnierInstanzService.RundenUebergaengeValidieren(tempListe);
            if (validierung.IstUngueltig)
            {
                KonsolenHilfsmittel.FehlerAnzeigen(validierung.FehlerZusammenfassung);
                continue;
            }

            runden.Add(rundeEingabe);
            AnsiConsole.MarkupLine($"[green]✓[/] Runde '{rundeName}' hinzugefügt.");

            if (runden.Count == 1 && !AnsiConsole.Confirm("Weitere Runde hinzufügen?", defaultValue: true)) break;
        }

        if (runden.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen – keine Runden definiert."); KonsolenHilfsmittel.Pause(); return; }

        var instanzEingabe = new TurnierInstanzErstellenEingabe(name, saison, vorlageId, null, null);
        var neu = await instanzService.ErstellenMitRundenAsync(instanzEingabe, runden, ct);

        AnsiConsole.WriteLine();
        KonsolenHilfsmittel.ErfolgAnzeigen($"Turnier '{neu.Name}' ({saison}) mit {runden.Count} Runde(n) angelegt (ID {neu.Id}).");
        KonsolenHilfsmittel.Pause();
    }

    private async Task StatusAendernAsync(CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (instanzen.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(new SelectionPrompt<TurnierInstanz?>()
            .Title("Turnier:").UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.Status})").EnableSearch().AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
        if (auswahl is null) return;

        var neuerStatus = AnsiConsole.Prompt(new SelectionPrompt<TurnierStatus?>()
            .Title("Neuer Status:")
            .UseConverter(s => s is null ? "← Zurück" : s.ToString())
            .AddChoices(Enum.GetValues<TurnierStatus>().Select(s => (TurnierStatus?)s).Append(null)));
        if (neuerStatus is null) return;

        await instanzService.StatusAendernAsync(auswahl.Id, neuerStatus.Value, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Status von '{auswahl.Name}' auf '{neuerStatus}' geändert.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (instanzen.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(new SelectionPrompt<TurnierInstanz?>()
            .Title("Turnier löschen:").UseConverter(ti => ti is null ? "← Zurück" : $"{ti.Name} ({ti.SaisonOderJahr})").EnableSearch().AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
        if (auswahl is null) return;

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(auswahl.Name)}'[/] wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await instanzService.LoeschenAsync(auswahl.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{auswahl.Name}' gelöscht.");
        KonsolenHilfsmittel.Pause();
    }
}
