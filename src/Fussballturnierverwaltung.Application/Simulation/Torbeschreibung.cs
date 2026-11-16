namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Beschreibungstext eines Tors in der ausführlichen Simulation – reine Text-Anreicherung ohne Einfluss auf
/// Spielstand oder Torerwartung (aus der <see cref="SimulationsEngine"/> hierher verschoben, #361).</summary>
internal static class Torbeschreibung
{
    // Issue #126: Auswahl angelehnt an das Attribut "Schussart II" im DFL-Definitionskatalog
    // "Offizielle Spieldaten" (2189_original.pdf), das u.a. Fallrückzieher, Hacke, Chip und Schlenzer führt.
    private static readonly string[] _torstile =
    [
        "Kopfball", "Flachschuss", "Volleyschuss", "Fallrückzieher", "Hacke",
        "Chipball", "Schlenzer", "Abstauber", "Distanzschuss", "Direktabnahme"
    ];

    // Issue #127: Realistischer Anteil von Kontertoren an allen Toren im Amateur-/Profifußball liegt grob im
    // niedrigen zweistelligen Prozentbereich.
    private const double KONTERTOR_WAHRSCHEINLICHKEIT = 0.15;

    // Issue #128: Grobes 3x3-Raster angelehnt an die "Torzone" im DFL-Definitionskatalog "Offizielle Spieldaten"
    // (Abbildung 5) — Mitte/Mitte bewusst als "Mitte" statt "Mitte Mitte".
    private static readonly string[] _torzonen =
    [
        "oben links", "oben Mitte", "oben rechts",
        "Mitte links", "Mitte",      "Mitte rechts",
        "unten links", "unten Mitte", "unten rechts"
    ];

    // #361: Anteil der Tore gegen ein Team mit Brechstange, die ins leere Tor gehen – der Teil der Torerwartung,
    // den die Brechstange gegenüber einer neutralen Abwehr hinzufügt.
    private static readonly double _leeresTorAnteil = 1 - 1 / Spielsituation.AngriffDesGegners(Risikostufe.Brechstange);

    public const string LEERES_TOR = "Tor ins leere Tor";

    /// <summary>Text eines Tors; steht der Torhüter des Gegners mit vorn, geht ein Teil der Tore ins leere Tor.</summary>
    public static string Zufaellig(Random rng, bool gegnerMitBrechstange)
        => gegnerMitBrechstange && rng.NextDouble() < _leeresTorAnteil
            ? LEERES_TOR
            : Torstil(rng);

    private static string Torstil(Random rng)
    {
        string basis = $"Tor per {_torstile[rng.Next(_torstile.Length)]}";
        string mitKonter = rng.NextDouble() < KONTERTOR_WAHRSCHEINLICHKEIT ? $"{basis} nach Konter" : basis;
        return $"{mitKonter} ({_torzonen[rng.Next(_torzonen.Length)]})";
    }
}
