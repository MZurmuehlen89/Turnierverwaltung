using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

/// <summary>
/// Vordefinierte Ranglisten-Quellen (Issue #227) — jeweils gegen die echte Seite geprüft
/// (Tabellenindex + Spalten passend zum Stand der Seite zum Zeitpunkt der Recherche). Wikipedia
/// kann Tabellen im Lauf der Zeit umsortieren; falls eine Quelle nicht mehr passt, lässt sie sich
/// über den Ranglisten-Quelle-Dialog (#225) einfach neu prüfen und die richtige Tabelle neu wählen.
/// </summary>
public static class RanglistenQuellenSeedDaten
{
    private record E(string Name, string Url, int TabellenIndex, int MannschaftSpalte, int PunkteSpalte);

    private static readonly E[] Eintraege =
    [
        new("FIFA-Weltrangliste (Herren)",
            "https://de.wikipedia.org/wiki/FIFA-Weltrangliste",
            TabellenIndex: 1, MannschaftSpalte: 1, PunkteSpalte: 2),

        new("Fußball-Bundesliga – Abschlusstabelle",
            "https://de.wikipedia.org/wiki/Fußball-Bundesliga_2025/26",
            TabellenIndex: 20, MannschaftSpalte: 1, PunkteSpalte: 8),

        new("2. Fußball-Bundesliga – Abschlusstabelle",
            "https://de.wikipedia.org/wiki/2._Fußball-Bundesliga_2025/26",
            TabellenIndex: 21, MannschaftSpalte: 1, PunkteSpalte: 8),

        new("La Liga – Abschlusstabelle",
            "https://de.wikipedia.org/wiki/Primera_División_2025/26",
            TabellenIndex: 24, MannschaftSpalte: 1, PunkteSpalte: 8),

        new("Premier League – Abschlusstabelle",
            "https://de.wikipedia.org/wiki/Premier_League_2025/26",
            TabellenIndex: 24, MannschaftSpalte: 1, PunkteSpalte: 8),

        new("Ligue 1 – Abschlusstabelle",
            "https://de.wikipedia.org/wiki/Ligue_1_2025/26",
            TabellenIndex: 20, MannschaftSpalte: 1, PunkteSpalte: 8),

        new("Serie A – Abschlusstabelle",
            "https://de.wikipedia.org/wiki/Serie_A_2025/26",
            TabellenIndex: 21, MannschaftSpalte: 1, PunkteSpalte: 8),
    ];

    public static List<RanglistenQuelle> Erstellen() =>
        Eintraege
            .Select(e => new RanglistenQuelle
            {
                Name                   = e.Name,
                Url                     = e.Url,
                TabellenIndex           = e.TabellenIndex,
                TabellenSelektor        = null,
                MannschaftSpalte        = e.MannschaftSpalte,
                PunkteSpalte            = e.PunkteSpalte,
                KopfzeileUeberspringen  = true
            })
            .ToList();
}
