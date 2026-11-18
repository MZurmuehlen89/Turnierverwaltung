using Fussballturnierverwaltung.Domain.Enums;

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

    // #431: Standards ≈ 22 % der Tore ohne Elfmeter (Kommentar „Schritt 1“ zu #431): 16,5 % nach Ecke, 5,5 % nach Freistoß.
    private const double ECKE_WAHRSCHEINLICHKEIT     = 0.165;
    private const double FREISTOSS_WAHRSCHEINLICHKEIT = 0.055;

    // Issue #128: Grobes 3x3-Raster angelehnt an die "Torzone" im DFL-Definitionskatalog "Offizielle Spieldaten"
    // (Abbildung 5) — Mitte/Mitte bewusst als "Mitte" statt "Mitte Mitte".
    private static readonly string[] _torzonen =
    [
        "oben links", "oben Mitte", "oben rechts",
        "Mitte links", "Mitte",      "Mitte rechts",
        "unten links", "unten Mitte", "unten rechts"
    ];

    public const string LEERES_TOR = "Tor ins leere Tor";

    /// <summary>Text eines Tors. Ob es ins leere Tor geht, entscheidet seit #431 die Verwertung im
    /// <see cref="Spielablauf"/>.</summary>
    public static string Zufaellig(Random rng, bool insLeereTor, Chancenart art)
    {
        if (art == Chancenart.Elfmeter)
            return $"Tor per Elfmeter ({Torzone(rng)})";

        return insLeereTor ? LEERES_TOR : Torstil(rng);
    }

    private static string Torstil(Random rng)
    {
        string basis = $"Tor per {_torstile[rng.Next(_torstile.Length)]}";
        return $"{basis}{Herkunft(rng.NextDouble())} ({Torzone(rng)})";
    }

    private static string Herkunft(double wurf) => wurf switch
    {
        < KONTERTOR_WAHRSCHEINLICHKEIT => " nach Konter",
        < KONTERTOR_WAHRSCHEINLICHKEIT + ECKE_WAHRSCHEINLICHKEIT => " nach Ecke",
        < KONTERTOR_WAHRSCHEINLICHKEIT + ECKE_WAHRSCHEINLICHKEIT + FREISTOSS_WAHRSCHEINLICHKEIT => " nach Freistoß",
        _ => string.Empty
    };

    private static string Torzone(Random rng) => _torzonen[rng.Next(_torzonen.Length)];
}
