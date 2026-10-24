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
    /// <summary>Name der Quelle für den UEFA-Klub-Koeffizienten — Standard-Rangliste der Ligaphase von CL/EL/ECL
    /// ab 2024/25 (Issue #349). Die Migration StandardRanglisteJeRunde legt sie mit denselben Werten an.</summary>
    public const string UEFA_KLUB_KOEFFIZIENT = "UEFA-Klub-Koeffizient (Kassies)";

    private sealed record E(string Name, string Url, int TabellenIndex, int MannschaftSpalte, int PunkteSpalte);

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

        // Issue #349: Bert Kassies' vollständige Klubrangliste (Fünfjahreswertung, ~470 Klubs, eine einzige
        // Tabelle). Die deutsche Wikipedia listet nur die Top ~56 plus deutschsprachige Klubs — für die
        // Topfbildung der Ligaphase zu lückenhaft. trank2026 = Stand nach 2025/26, gilt für die Auslosung 2026/27.
        new(UEFA_KLUB_KOEFFIZIENT,
            "https://kassiesa.net/uefa/data/method5/trank2026.html",
            TabellenIndex: 0, MannschaftSpalte: 2, PunkteSpalte: 9),
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
