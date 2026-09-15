using System.Text.Json;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Typisierter Zugriff auf <see cref="Domain.Entitaeten.Turniere.TurnierRunde.KonfigurationJson"/> —
/// ein generisches, per-Rundenart genutztes JSON-Feld. Aktuell nur ein Feld: <see cref="AnzahlSpieltage"/>
/// (Schweizer System, Issue #329 Teil D).</summary>
public record RundenKonfiguration(int? AnzahlSpieltage = null)
{
    private static readonly JsonSerializerOptions _optionen = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static RundenKonfiguration AusJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new RundenKonfiguration();
        try
        {
            return JsonSerializer.Deserialize<RundenKonfiguration>(json, _optionen) ?? new RundenKonfiguration();
        }
        catch (JsonException)
        {
            return new RundenKonfiguration();
        }
    }

    public string? ZuJson() => AnzahlSpieltage is null ? null : JsonSerializer.Serialize(this, _optionen);

    /// <summary>ceil(log2(n)) als fachlicher Standard-Vorschlag für die Rundenanzahl im Schweizer System —
    /// genug Spieltage, um bei n Teilnehmern eindeutige Platzierungen zu ermöglichen.</summary>
    public static int VorschlagAnzahlSpieltage(int mannschaftsanzahl) =>
        (int)Math.Ceiling(Math.Log2(Math.Max(2, mannschaftsanzahl)));

    /// <summary>Alle sinnvollen Gruppenanzahl/Gruppengröße-Kombinationen (Teiler von
    /// <paramref name="eingangsMannschaftsanzahl"/>, jeweils mindestens 2 Gruppen à mindestens 2 Mannschaften) —
    /// Vorschlagsliste für die Gruppenanzahl-Eingabe im Runden-Editor (Issue #215), damit nicht blind
    /// eingetippt werden muss.</summary>
    public static IReadOnlyList<(int GruppenAnzahl, int Gruppengroesse)> GruppenAnzahlVorschlaege(int eingangsMannschaftsanzahl)
    {
        var ergebnis = new List<(int, int)>();
        for (var gruppenAnzahl = 2; gruppenAnzahl <= eingangsMannschaftsanzahl / 2; gruppenAnzahl++)
        {
            if (eingangsMannschaftsanzahl % gruppenAnzahl != 0) continue;
            ergebnis.Add((gruppenAnzahl, eingangsMannschaftsanzahl / gruppenAnzahl));
        }
        return ergebnis;
    }
}
