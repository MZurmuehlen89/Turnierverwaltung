using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Application.ImportExport;

public record ImportErgebnis(
    int Importiert,
    int Fehler,
    IReadOnlyList<string> FehlerMeldungen);

public class ImportExportService(
    IMannschaftRepository mannschaftRepository,
    ILandRepository landRepository,
    ISpielRepository spielRepository,
    ITurnierInstanzRepository instanzRepository,
    MannschaftService mannschaftService,
    ImportValidator importValidator,
    ILogger<ImportExportService> logger)
{
    private const char Trennzeichen = ';';

    private static readonly JsonSerializerOptions JsonOptionen = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    // ── IMPORT ────────────────────────────────────────────────────────────

    /// <summary>Importiert Mannschaften aus einer CSV-Datei.</summary>
    public async Task<ImportErgebnis> MannschaftenImportierenAsync(
        string dateipfad, CancellationToken ct = default)
    {
        if (!File.Exists(dateipfad))
            throw new FileNotFoundException($"Datei nicht gefunden: {dateipfad}");

        var zeilen = await File.ReadAllLinesAsync(dateipfad, Encoding.UTF8, ct);
        var datensaetze = ParseCsv(zeilen);

        if (!datensaetze.Any())
            throw new InvalidOperationException("Die Datei enthält keine Datenzeilen.");

        // Alle verfügbaren Länder als FIFA-Code-Lookup laden
        var laender = await landRepository.AlleFindenAsync(ct);
        var landLookup = laender.ToDictionary(
            l => l.FifaCode.ToUpperInvariant(), l => l.Id);

        int importiert = 0;
        var fehlerMeldungen = new List<string>();

        for (int i = 0; i < datensaetze.Count; i++)
        {
            var zeile = datensaetze[i];
            var valErgebnis = importValidator.ValidierenMannschaft(zeile, i + 2);

            if (valErgebnis.IstUngueltig)
            {
                fehlerMeldungen.AddRange(valErgebnis.Fehler);
                continue;
            }

            var fifaCode = Wert(zeile, "LandFifaCode").ToUpperInvariant();
            if (!landLookup.TryGetValue(fifaCode, out int landId))
            {
                fehlerMeldungen.Add($"Zeile {i + 2}: Land mit FIFA-Code '{fifaCode}' nicht gefunden.");
                continue;
            }

            try
            {
                var eingabe = new MannschaftErstellenEingabe(
                    Name:             Wert(zeile, "Name"),
                    Kurzname:         Wert(zeile, "Kurzname"),
                    Mannschaftsart:   Enum.TryParse<Mannschaftsart>(
                                          Wert(zeile, "Mannschaftsart", "Verein"), true, out var art)
                                      ? art : Mannschaftsart.Verein,
                    LandId:           landId,
                    OffensivRating:   ParseInt(zeile, "OffensivRating",   70),
                    MittelfeldRating: ParseInt(zeile, "MittelfeldRating", 70),
                    DefensivRating:   ParseInt(zeile, "DefensivRating",   70),
                    Heimstaerke:      ParseInt(zeile, "Heimstaerke",      75),
                    Auswaertsstaerke: ParseInt(zeile, "Auswaertsstaerke", 65));

                await mannschaftService.ErstellenAsync(eingabe, ct);
                importiert++;
            }
            catch (Exception ex)
            {
                fehlerMeldungen.Add($"Zeile {i + 2}: {ex.Message}");
            }
        }

        logger.LogInformation(
            "Import abgeschlossen: {Ok} importiert, {Err} Fehler.", importiert, fehlerMeldungen.Count);

        return new ImportErgebnis(importiert, fehlerMeldungen.Count, fehlerMeldungen);
    }

    // ── EXPORT – Mannschaften ─────────────────────────────────────────────

    /// <summary>Exportiert alle Mannschaften als CSV.</summary>
    public async Task<int> MannschaftenExportierenCsvAsync(
        string dateipfad, CancellationToken ct = default)
    {
        var mannschaften = await mannschaftRepository.AlleFindenMitLandAsync(ct);
        SicherstellenVerzeichnis(dateipfad);

        var sb = new StringBuilder();
        sb.AppendLine(KopfZeile(
            "Name", "Kurzname", "Mannschaftsart", "LandFifaCode",
            "OffensivRating", "MittelfeldRating", "DefensivRating",
            "Heimstaerke", "Auswaertsstaerke"));

        foreach (var m in mannschaften)
        {
            sb.AppendLine(DatenZeile(
                m.Name, m.Kurzname, m.Mannschaftsart.ToString(),
                m.Land?.FifaCode ?? "",
                m.OffensivRating.ToString(), m.MittelfeldRating.ToString(),
                m.DefensivRating.ToString(), m.Heimstaerke.ToString(),
                m.Auswaertsstaerke.ToString()));
        }

        await File.WriteAllTextAsync(dateipfad, sb.ToString(), Encoding.UTF8, ct);
        logger.LogInformation("{Anz} Mannschaften nach '{Pfad}' exportiert.", mannschaften.Count, dateipfad);
        return mannschaften.Count;
    }

    // ── EXPORT – Spielplan ────────────────────────────────────────────────

    /// <summary>Exportiert alle Spiele eines Turniers als CSV (mit Ergebnissen).</summary>
    public async Task<int> SpielplanExportierenCsvAsync(
        int turnierInstanzId, string dateipfad, CancellationToken ct = default)
    {
        var instanz = await instanzRepository.FindenNachIdMitRundenAsync(turnierInstanzId, ct)
            ?? throw new InvalidOperationException($"Turnier {turnierInstanzId} nicht gefunden.");

        SicherstellenVerzeichnis(dateipfad);

        var sb = new StringBuilder();
        sb.AppendLine(KopfZeile(
            "Turnier", "Saison", "Runde", "SpielNr", "Spieltag",
            "Heim", "Auswaerts",
            "HeimTore", "AuswaertsTore", "HeimToreNV", "AuswaertsToreNV",
            "HeimElf", "AuswaertsElf",
            "Status", "Entscheidung"));

        int gesamt = 0;
        foreach (var runde in instanz.Runden.OrderBy(r => r.Reihenfolge))
        {
            var spiele = await spielRepository.AlleNachRundeAsync(runde.Id, ct);
            foreach (var s in spiele)
            {
                var e = s.Ergebnis;
                sb.AppendLine(DatenZeile(
                    instanz.Name, instanz.SaisonOderJahr, runde.Name,
                    s.Spielnummer.ToString(),
                    s.Spieltag?.ToString() ?? "",
                    s.HeimMannschaft?.Name     ?? $"ID {s.HeimMannschaftId}",
                    s.AuswaertsMannschaft?.Name ?? $"ID {s.AuswaertsMannschaftId}",
                    e?.HeimToreRegulaer.ToString()             ?? "",
                    e?.AuswaertsToreRegulaer.ToString()        ?? "",
                    e?.HeimToreVerlaengerung?.ToString()        ?? "",
                    e?.AuswaertsToreVerlaengerung?.ToString()   ?? "",
                    e?.HeimElfmetertore?.ToString()             ?? "",
                    e?.AuswaertsElfmetertore?.ToString()        ?? "",
                    s.Status.ToString(),
                    e?.EntscheidungArt.ToString() ?? ""));
                gesamt++;
            }
        }

        await File.WriteAllTextAsync(dateipfad, sb.ToString(), Encoding.UTF8, ct);
        logger.LogInformation("{Anz} Spiele nach '{Pfad}' exportiert.", gesamt, dateipfad);
        return gesamt;
    }

    // ── EXPORT – Turnier JSON ─────────────────────────────────────────────

    /// <summary>Exportiert vollständige Turnierdaten (Runden, Spiele, Ergebnisse) als JSON.</summary>
    public async Task TurnierExportierenJsonAsync(
        int turnierInstanzId, string dateipfad, CancellationToken ct = default)
    {
        var instanz = await instanzRepository.FindenNachIdMitRundenAsync(turnierInstanzId, ct)
            ?? throw new InvalidOperationException($"Turnier {turnierInstanzId} nicht gefunden.");

        SicherstellenVerzeichnis(dateipfad);

        var rundenDtos = new List<object>();

        foreach (var runde in instanz.Runden.OrderBy(r => r.Reihenfolge))
        {
            var spiele = await spielRepository.AlleNachRundeAsync(runde.Id, ct);

            var spieleDtos = spiele.Select(s =>
            {
                var e = s.Ergebnis;
                return (object)new
                {
                    nr              = s.Spielnummer,
                    spieltag        = s.Spieltag,
                    datum           = s.Datum?.ToString("yyyy-MM-dd"),
                    heim            = s.HeimMannschaft?.Name     ?? $"ID {s.HeimMannschaftId}",
                    auswaerts       = s.AuswaertsMannschaft?.Name ?? $"ID {s.AuswaertsMannschaftId}",
                    heimTore        = e?.HeimToreRegulaer,
                    auswaertsTore   = e?.AuswaertsToreRegulaer,
                    heimToreNV      = e?.HeimToreVerlaengerung,
                    auswaertsToreNV = e?.AuswaertsToreVerlaengerung,
                    heimElf         = e?.HeimElfmetertore,
                    auswaertsElf    = e?.AuswaertsElfmetertore,
                    status          = s.Status.ToString(),
                    entscheidung    = e?.EntscheidungArt.ToString(),
                    sieger          = e?.SiegerMannschaft?.Name
                };
            }).ToList();

            rundenDtos.Add(new
            {
                reihenfolge = runde.Reihenfolge,
                name        = runde.Name,
                rundenart   = runde.Rundenart.ToString(),
                anzahlSpiele = spiele.Count,
                abgeschlossen = spiele.Count(s => s.Status == SpielStatus.Abgeschlossen),
                spiele      = spieleDtos
            });
        }

        var export = new
        {
            turnier       = instanz.Name,
            saison        = instanz.SaisonOderJahr,
            status        = instanz.Status.ToString(),
            exportiertAm  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            runden        = rundenDtos
        };

        var json = JsonSerializer.Serialize(export, JsonOptionen);
        await File.WriteAllTextAsync(dateipfad, json, Encoding.UTF8, ct);

        logger.LogInformation("Turnier '{Name}' nach '{Pfad}' exportiert.", instanz.Name, dateipfad);
    }

    // ── CSV-Hilfsmethoden ─────────────────────────────────────────────────

    private static IReadOnlyList<IDictionary<string, string>> ParseCsv(string[] zeilen)
    {
        if (zeilen.Length < 2) return [];

        var header = ParseZeile(zeilen[0]);
        var ergebnis = new List<IDictionary<string, string>>();

        for (int i = 1; i < zeilen.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(zeilen[i])) continue;
            var werte = ParseZeile(zeilen[i]);
            var zeile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int j = 0; j < Math.Min(header.Count, werte.Count); j++)
                zeile[header[j].Trim()] = werte[j].Trim();
            ergebnis.Add(zeile);
        }

        return ergebnis;
    }

    private static List<string> ParseZeile(string zeile)
    {
        var felder = new List<string>();
        var aktuell = new StringBuilder();
        bool inAnfuehrung = false;

        foreach (char c in zeile)
        {
            if (c == '"') { inAnfuehrung = !inAnfuehrung; continue; }
            if (c == Trennzeichen && !inAnfuehrung) { felder.Add(aktuell.ToString()); aktuell.Clear(); continue; }
            aktuell.Append(c);
        }
        felder.Add(aktuell.ToString());
        return felder;
    }

    private static string KopfZeile(params string[] felder)
        => string.Join(Trennzeichen, felder);

    private static string DatenZeile(params string[] felder)
        => string.Join(Trennzeichen, felder.Select(CsvFeld));

    private static string CsvFeld(string wert)
    {
        if (wert.Contains(Trennzeichen) || wert.Contains('"') || wert.Contains('\n'))
            return $"\"{wert.Replace("\"", "\"\"")}\"";
        return wert;
    }

    private static void SicherstellenVerzeichnis(string dateipfad)
    {
        var verz = Path.GetDirectoryName(dateipfad);
        if (!string.IsNullOrWhiteSpace(verz))
            Directory.CreateDirectory(verz);
    }

    private static string Wert(IDictionary<string, string> zeile, string feld, string standard = "")
        => zeile.TryGetValue(feld, out var v) ? v.Trim() : standard;

    private static int ParseInt(IDictionary<string, string> zeile, string feld, int standard)
        => zeile.TryGetValue(feld, out var s) && int.TryParse(s, out var v) ? v : standard;
}
