namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Grenzen der Auslosungsregel „Konföderationsverteilung“ (Issue #356): Die Teams eines Kontinentalverbands werden
/// möglichst gleichmäßig auf die Gruppen verteilt — je Gruppe höchstens aufgerundet und mindestens abgerundet
/// (Teams des Verbands ÷ Gruppenanzahl). Das ergibt ohne einstellbare Werte genau die FIFA-Vorgaben seit 1994:
/// z. B. WM 2026 mit 16 UEFA-Teams in 12 Gruppen → je Gruppe 1–2 UEFA-Teams, alle übrigen Verbände höchstens 1.
/// Teilnehmer ohne bekannten Verband (Platzhalter) zählen nicht mit.
/// </summary>
public sealed class KonfoederationsGrenzen
{
    private readonly Dictionary<int, (int Min, int Max)> _grenzen;

    public KonfoederationsGrenzen(IEnumerable<int?> verbandJeTeilnehmer, int gruppenAnzahl)
    {
        if (gruppenAnzahl < 1) throw new ArgumentOutOfRangeException(nameof(gruppenAnzahl));
        _grenzen = verbandJeTeilnehmer.OfType<int>()
            .GroupBy(verband => verband)
            .ToDictionary(g => g.Key, g => (g.Count() / gruppenAnzahl, (g.Count() + gruppenAnzahl - 1) / gruppenAnzahl));
    }

    public IReadOnlyCollection<int> Verbaende => _grenzen.Keys;

    /// <summary>Mindestanzahl Teams dieses Verbands je Gruppe (0 für unbekannte Verbände).</summary>
    public int Min(int verbandId) => _grenzen.TryGetValue(verbandId, out var g) ? g.Min : 0;

    /// <summary>Höchstanzahl Teams dieses Verbands je Gruppe.</summary>
    public int Max(int verbandId) => _grenzen.TryGetValue(verbandId, out var g) ? g.Max : int.MaxValue;
}
