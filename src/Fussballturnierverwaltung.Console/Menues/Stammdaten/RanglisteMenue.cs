using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Stammdaten;

public class RanglisteMenue(
    RanglisteService ranglisteService,
    MannschaftService mannschaftService,
    RanglistenImportService importService,
    RanglistenQuelleMenue quelleMenue,
    MannschaftMenue mannschaftMenue)
{
    /// <summary>Zuordnungsoption einer nicht aufgelösten Import-Zeile — Team null + NeuAnlegen
    /// false bedeutet "Überspringen", Team null + NeuAnlegen true löst den Schnell-Anlegen-Dialog
    /// aus (Issue #227).</summary>
    private record ZuordnungsOption(Mannschaft? Team, bool IstNeuAnlegen)
    {
        public static readonly ZuordnungsOption Ueberspringen = new(null, false);
        public static readonly ZuordnungsOption NeuAnlegenWahl = new(null, true);
    }

    private enum Option { AlleAnzeigen, Details, NeuAnlegen, ImportAusDemInternet, QuellenVerwalten, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("📋 Ranglisten");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen          => "Alle anzeigen",
                        Option.Details               => "Details anzeigen",
                        Option.NeuAnlegen            => "Neue Rangliste anlegen",
                        Option.ImportAusDemInternet  => "Aus dem Internet importieren",
                        Option.QuellenVerwalten      => "Import-Quellen verwalten",
                        Option.Loeschen              => "Rangliste löschen",
                        Option.Zurueck               => "← Zurück",
                        _                            => o.ToString()
                    })
                    .AddChoices(Option.AlleAnzeigen, Option.Details, Option.NeuAnlegen,
                        Option.ImportAusDemInternet, Option.QuellenVerwalten, Option.Loeschen, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.AlleAnzeigen:         await AlleAnzeigenAsync(ct); break;
            case Option.Details:              await DetailsAnzeigenAsync(ct); break;
            case Option.NeuAnlegen:           await NeuAnlegenAsync(ct); break;
            case Option.ImportAusDemInternet: await ImportAusDemInternetAsync(ct); break;
            case Option.QuellenVerwalten:     await quelleMenue.AnzeigenAsync(ct); break;
            case Option.Loeschen:             await LoeschenAsync(ct); break;
        }
    }

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Ranglisten");
        var ranglisten = await ranglisteService.AlleAbrufenAsync(ct);

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn("Datum")
            .AddColumn("Quelle")
            .AddColumn(new TableColumn("Einträge").RightAligned());

        foreach (var r in ranglisten)
            tabelle.AddRow($"[grey]{r.Id}[/]", Markup.Escape(r.Name), r.Datum.ToString("yyyy-MM-dd"),
                Markup.Escape(r.Quelle), r.Eintraege.Count.ToString());

        AnsiConsole.MarkupLine($"[grey]{ranglisten.Count} Rangliste(n)[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task DetailsAnzeigenAsync(CancellationToken ct)
    {
        var rangliste = await AuswaehlenAsync("Rangliste:", ct);
        if (rangliste is null) return;

        var details = await ranglisteService.FindenNachIdAsync(rangliste.Id, ct);
        if (details is null) return;

        KonsolenHilfsmittel.Kopf($"Rangliste: {details.Name}");
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Name:[/]",   Markup.Escape(details.Name));
        grid.AddRow("[grey]Datum:[/]",  details.Datum.ToString("yyyy-MM-dd"));
        grid.AddRow("[grey]Quelle:[/]", Markup.Escape(details.Quelle));
        AnsiConsole.Write(new Panel(grid));

        if (details.Eintraege.Any())
        {
            var eintraegeTabelle = new Table().Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("Rang").RightAligned())
                .AddColumn("Mannschaft")
                .AddColumn(new TableColumn("Punkte").RightAligned());
            foreach (var e in details.Eintraege.OrderBy(e => e.Rang))
                eintraegeTabelle.AddRow(e.Rang.ToString(), Markup.Escape(e.Mannschaft.Name), e.Punkte?.ToString() ?? "[grey]–[/]");
            AnsiConsole.Write(eintraegeTabelle);
        }

        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neue Rangliste anlegen");

        var name   = KonsolenHilfsmittel.TextEingeben("Name");
        var datum  = AnsiConsole.Prompt(new TextPrompt<DateOnly>("Datum (yyyy-MM-dd):").DefaultValue(DateOnly.FromDateTime(DateTime.Today)));
        var quelle = KonsolenHilfsmittel.TextEingeben("Quelle (z.B. \"FIFA-Weltrangliste\" oder \"Manuell\")");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Einträge (Mannschaft + Rang):[/]");
        var alleTeams = await mannschaftService.AlleAbrufenAsync(ct);
        var eintraege = new List<RanglistenEintragEingabe>();

        while (true)
        {
            var verbleibend = alleTeams.Where(t => eintraege.All(e => e.MannschaftId != t.Id)).ToList();
            if (!verbleibend.Any() || !AnsiConsole.Confirm($"Eintrag hinzufügen? ({eintraege.Count} bereits)", defaultValue: eintraege.Count == 0))
                break;

            var team = AnsiConsole.Prompt(new SelectionPrompt<Mannschaft?>()
                .Title("Mannschaft:").UseConverter(t => t is null ? "← Abbrechen" : t.Name).EnableSearch()
                .AddChoices(verbleibend.Cast<Mannschaft?>().Append(null)));
            if (team is null) break;

            var rang   = AnsiConsole.Prompt(new TextPrompt<int>("Rang:").DefaultValue(eintraege.Count + 1).Validate(v => v >= 1));
            var punkte = AnsiConsole.Confirm("Punkte angeben?", defaultValue: false)
                ? AnsiConsole.Prompt(new TextPrompt<int>("Punkte:").Validate(v => v >= 0))
                : (int?)null;

            eintraege.Add(new RanglistenEintragEingabe(team.Id, rang, punkte));
            AnsiConsole.MarkupLine($"  → {rang}. {Markup.Escape(team.Name)}");
        }

        var eingabe = new RanglisteErstellenEingabe(name, datum, quelle, eintraege);
        var neu = await ranglisteService.ErstellenAsync(eingabe, ct);

        AnsiConsole.WriteLine();
        KonsolenHilfsmittel.ErfolgAnzeigen($"Rangliste '{neu.Name}' angelegt (ID {neu.Id}).");
        KonsolenHilfsmittel.Pause();
    }

    private async Task ImportAusDemInternetAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Rangliste aus dem Internet importieren");

        var quelle = await quelleMenue.AuswaehlenAsync("Quelle:", ct);
        if (quelle is null) return;

        var vorschau = await AnsiConsole.Status()
            .StartAsync("Rufe Rangliste ab...", async _ => await importService.VorschauAbrufenAsync(quelle, ct));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]{vorschau.Aufgeloest.Count} automatisch zugeordnet, {vorschau.Unaufgeloest.Count} nicht erkannt.[/]");

        var alleTeams = await mannschaftService.AlleAbrufenAsync(ct);
        var bereitsVergeben = vorschau.Aufgeloest.Select(z => z.Mannschaft.Id).ToHashSet();
        var zuordnung = new Dictionary<int, Mannschaft?>();

        foreach (var zeile in vorschau.Unaufgeloest.OrderBy(z => z.Index))
        {
            while (true)
            {
                var verbleibend = alleTeams.Where(t => !bereitsVergeben.Contains(t.Id)).ToList();
                var optionen = new List<ZuordnungsOption> { ZuordnungsOption.Ueberspringen, ZuordnungsOption.NeuAnlegenWahl };
                optionen.AddRange(verbleibend.Select(t => new ZuordnungsOption(t, false)));

                var auswahl = AnsiConsole.Prompt(new SelectionPrompt<ZuordnungsOption>()
                    .Title($"'{Markup.Escape(zeile.RohName)}' zuordnen:")
                    .UseConverter(o => o.IstNeuAnlegen ? "+ Neue Mannschaft anlegen…" : o.Team is null ? "→ Überspringen" : o.Team.Name)
                    .EnableSearch()
                    .AddChoices(optionen));

                if (auswahl.IstNeuAnlegen)
                {
                    var neu = await mannschaftMenue.SchnellAnlegenAsync(zeile.RohName, ct);
                    if (neu is null) continue; // Abgebrochen -> Zeile erneut zuordnen

                    alleTeams = [.. alleTeams, neu];
                    zuordnung[zeile.Index] = neu;
                    bereitsVergeben.Add(neu.Id);
                    AnsiConsole.MarkupLine($"  → neu angelegt und zugeordnet: {Markup.Escape(neu.Name)}");
                    break;
                }

                zuordnung[zeile.Index] = auswahl.Team;
                if (auswahl.Team is not null) bereitsVergeben.Add(auswahl.Team.Id);
                break;
            }
        }

        var eintraege = importService.EintraegeErstellen(vorschau, zuordnung);
        if (eintraege.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Einträge zum Importieren (alle Zeilen übersprungen).");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.WriteLine();
        var name = KonsolenHilfsmittel.TextEingeben("Name", quelle.Name);
        var datum = AnsiConsole.Prompt(new TextPrompt<DateOnly>("Datum (yyyy-MM-dd):").DefaultValue(DateOnly.FromDateTime(DateTime.Today)));
        var quelleText = KonsolenHilfsmittel.TextEingeben("Quelle", $"{quelle.Name} ({quelle.Url})");

        var vorhandene = await ranglisteService.AlleAbrufenAsync(ct);
        Rangliste? ziel = null;
        if (vorhandene.Count > 0 && AnsiConsole.Confirm("Bestehende Rangliste aktualisieren statt neu anzulegen?", defaultValue: false))
        {
            ziel = await AuswaehlenAsync("Zu aktualisierende Rangliste:", ct);
            if (ziel is null) return;
        }

        if (ziel is not null)
        {
            KonsolenHilfsmittel.WarnungAnzeigen($"Alle bisherigen Einträge von '{ziel.Name}' werden ersetzt.");
            if (!KonsolenHilfsmittel.Bestaetigen("Fortfahren?"))
            { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

            var aktualisierenEingabe = new RanglisteAktualisierenEingabe(name, datum, quelleText, eintraege);
            await ranglisteService.AktualisierenAsync(ziel.Id, aktualisierenEingabe, ct);
            KonsolenHilfsmittel.ErfolgAnzeigen($"'{name}' aktualisiert: {eintraege.Count} Einträge.");
        }
        else
        {
            var erstellenEingabe = new RanglisteErstellenEingabe(name, datum, quelleText, eintraege);
            var neu = await ranglisteService.ErstellenAsync(erstellenEingabe, ct);
            KonsolenHilfsmittel.ErfolgAnzeigen($"Rangliste '{neu.Name}' angelegt (ID {neu.Id}): {eintraege.Count} Einträge.");
        }

        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var rangliste = await AuswaehlenAsync("Rangliste löschen:", ct);
        if (rangliste is null) return;

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(rangliste.Name)}'[/] wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await ranglisteService.LoeschenAsync(rangliste.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{rangliste.Name}' gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task<Rangliste?> AuswaehlenAsync(string titel, CancellationToken ct)
    {
        var ranglisten = await ranglisteService.AlleAbrufenAsync(ct);
        if (ranglisten.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Ranglisten vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<Rangliste?>()
            .Title(titel).UseConverter(r => r is null ? "← Zurück" : r.Name).EnableSearch()
            .AddChoices(ranglisten.Cast<Rangliste?>().Append(null)));
    }
}
