using Fussballturnierverwaltung.Application.ImportExport;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.ImportExport;

public class ImportExportMenue(
    ImportExportService importExportService,
    TurnierInstanzService instanzService)
{
    private static readonly string StandardExportPfad =
        Path.Combine(AppContext.BaseDirectory, "Exports");

    private enum Option
    {
        MannschaftenImportCsv,
        MannschaftenExportCsv,
        SpielplanExportCsv,
        TurnierExportJson,
        Zurueck
    }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("📂 Import / Export");
            AnsiConsole.MarkupLine($"[grey]Standard-Exportpfad: {Markup.Escape(StandardExportPfad)}[/]");
            AnsiConsole.WriteLine();

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.MannschaftenImportCsv => "Mannschaften importieren (CSV)",
                        Option.MannschaftenExportCsv => "Mannschaften exportieren (CSV)",
                        Option.SpielplanExportCsv    => "Spielplan exportieren (CSV)",
                        Option.TurnierExportJson     => "Turnierdaten exportieren (JSON)",
                        Option.Zurueck               => "← Zurück",
                        _                            => o.ToString()
                    })
                    .AddChoices(Option.MannschaftenImportCsv, Option.MannschaftenExportCsv,
                                Option.SpielplanExportCsv, Option.TurnierExportJson, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.MannschaftenImportCsv: await MannschaftenImportierenAsync(ct); break;
            case Option.MannschaftenExportCsv: await MannschaftenExportierenAsync(ct); break;
            case Option.SpielplanExportCsv:    await SpielplanExportierenAsync(ct);    break;
            case Option.TurnierExportJson:     await TurnierExportierenAsync(ct);      break;
        }
    }

    // ── Import ────────────────────────────────────────────────────────────

    private async Task MannschaftenImportierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Mannschaften importieren (CSV)");

        AnsiConsole.MarkupLine("[grey]Erwartetes Format (Semikolon-getrennt):[/]");
        AnsiConsole.MarkupLine("[grey]Name;Kurzname;Mannschaftsart;LandFifaCode;OffensivRating;MittelfeldRating;DefensivRating;Heimstaerke;Auswaertsstaerke[/]");
        AnsiConsole.MarkupLine("[grey]Mannschaftsart: Nationalmannschaft | Verein[/]");
        AnsiConsole.MarkupLine("[grey]LandFifaCode: dreistelliger FIFA-Code (z.B. GER, BRA)[/]");
        AnsiConsole.WriteLine();

        var dateipfad = DateipfadEingeben("CSV-Dateipfad (Import):", null, nurLesen: true);
        if (dateipfad is null) return;

        var ergebnis = await AnsiConsole.Status()
            .StartAsync("Importiere...", async _ =>
                await importExportService.MannschaftenImportierenAsync(dateipfad, ct));

        AnsiConsole.WriteLine();
        if (ergebnis.Neu > 0 || ergebnis.Aktualisiert > 0)
        {
            var teile = new List<string>();
            if (ergebnis.Neu          > 0) teile.Add($"{ergebnis.Neu} neu");
            if (ergebnis.Aktualisiert > 0) teile.Add($"{ergebnis.Aktualisiert} aktualisiert");
            KonsolenHilfsmittel.ErfolgAnzeigen($"Mannschaft(en) importiert: {string.Join(", ", teile)}.");
        }

        if (ergebnis.Fehler > 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen($"{ergebnis.Fehler} Zeile(n) mit Fehlern:");
            foreach (var f in ergebnis.FehlerMeldungen.Take(10))
                AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(f)}");
            if (ergebnis.FehlerMeldungen.Count > 10)
                AnsiConsole.MarkupLine($"  [grey]... und {ergebnis.FehlerMeldungen.Count - 10} weitere.[/]");
        }

        KonsolenHilfsmittel.Pause();
    }

    // ── Export – Mannschaften ─────────────────────────────────────────────

    private async Task MannschaftenExportierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Mannschaften exportieren (CSV)");

        var standardDatei = Path.Combine(StandardExportPfad, "Mannschaften.csv");
        var dateipfad = DateipfadEingeben("Speicherpfad:", standardDatei, nurLesen: false);
        if (dateipfad is null) return;

        int anzahl = await AnsiConsole.Status()
            .StartAsync("Exportiere...", async _ =>
                await importExportService.MannschaftenExportierenCsvAsync(dateipfad, ct));

        KonsolenHilfsmittel.ErfolgAnzeigen($"{anzahl} Mannschaft(en) exportiert → {Markup.Escape(dateipfad)}");
        KonsolenHilfsmittel.Pause();
    }

    // ── Export – Spielplan ────────────────────────────────────────────────

    private async Task SpielplanExportierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Spielplan exportieren (CSV)");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var dateiname = $"Spielplan_{Sicher(instanz.Name)}_{instanz.SaisonOderJahr}.csv";
        var standardDatei = Path.Combine(StandardExportPfad, dateiname);
        var dateipfad = DateipfadEingeben("Speicherpfad:", standardDatei, nurLesen: false);
        if (dateipfad is null) return;

        int anzahl = await AnsiConsole.Status()
            .StartAsync("Exportiere...", async _ =>
                await importExportService.SpielplanExportierenCsvAsync(instanz.Id, dateipfad, ct));

        KonsolenHilfsmittel.ErfolgAnzeigen($"{anzahl} Spiel(e) exportiert → {Markup.Escape(dateipfad)}");
        KonsolenHilfsmittel.Pause();
    }

    // ── Export – Turnier JSON ─────────────────────────────────────────────

    private async Task TurnierExportierenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Turnierdaten exportieren (JSON)");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var dateiname = $"Turnier_{Sicher(instanz.Name)}_{instanz.SaisonOderJahr}.json";
        var standardDatei = Path.Combine(StandardExportPfad, dateiname);
        var dateipfad = DateipfadEingeben("Speicherpfad:", standardDatei, nurLesen: false);
        if (dateipfad is null) return;

        await AnsiConsole.Status()
            .StartAsync("Exportiere...", async _ =>
                await importExportService.TurnierExportierenJsonAsync(instanz.Id, dateipfad, ct));

        KonsolenHilfsmittel.ErfolgAnzeigen($"Turnierdaten exportiert → {Markup.Escape(dateipfad)}");
        KonsolenHilfsmittel.Pause();
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
            new SelectionPrompt<TurnierInstanz>()
                .Title("Turnier:")
                .UseConverter(ti => $"{ti.Name} ({ti.SaisonOderJahr})")
                .EnableSearch()
                .AddChoices(instanzen));
    }

    /// <summary>
    /// Zeigt den Pfad an und lässt den Nutzer ihn bestätigen oder ändern.
    /// Gibt null zurück, wenn der Nutzer abbricht.
    /// </summary>
    private static string? DateipfadEingeben(string beschriftung, string? standard, bool nurLesen)
    {
        if (!string.IsNullOrWhiteSpace(standard))
            AnsiConsole.MarkupLine($"[grey]Standard: {Markup.Escape(standard)}[/]");

        var pfad = AnsiConsole.Prompt(
            new TextPrompt<string>(beschriftung)
                .AllowEmpty()
                .DefaultValue(standard ?? ""));

        if (string.IsNullOrWhiteSpace(pfad)) pfad = standard ?? "";
        if (string.IsNullOrWhiteSpace(pfad)) return null;

        if (nurLesen && !File.Exists(pfad))
        {
            KonsolenHilfsmittel.FehlerAnzeigen($"Datei nicht gefunden: {pfad}");
            return null;
        }

        return pfad;
    }

    private static string Sicher(string name)
        => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
