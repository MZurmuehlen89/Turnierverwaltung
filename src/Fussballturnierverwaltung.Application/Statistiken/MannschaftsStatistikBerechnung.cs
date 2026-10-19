using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Statistiken;

/// <summary>Auswertungen für eine einzelne Mannschaft (Statistik-Bereich „Mannschaft“, #324) — zustandslos, rechnet
/// auf den Spielen aus <see cref="StatistikBerechnung.AusMannschaftsSicht"/>.</summary>
public static class MannschaftsStatistikBerechnung
{
    private static readonly (int Von, int Bis, string Bezeichnung)[] _abschnitte =
    [
        (1, 15, "1.–15. Minute"),
        (16, 30, "16.–30. Minute"),
        (31, 45, "31.–45. Minute"),
        (46, 60, "46.–60. Minute"),
        (61, 75, "61.–75. Minute"),
        (76, 90, "76.–90. Minute")
    ];

    private const string VERLAENGERUNG = "Verlängerung";

    /// <summary>Platz eines Werts unter den Werten aller Mannschaften: eins mehr als die Zahl der Mannschaften mit
    /// besserem Wert. Gleiche Werte teilen sich den Platz (zwei Mannschaften mit den meisten Toren stehen beide auf
    /// Platz 1, die nächste auf Platz 3).</summary>
    public static int Platz(double wert, IEnumerable<double> alleWerte, bool hoeherIstBesser)
        => 1 + alleWerte.Count(w => hoeherIstBesser ? w > wert : w < wert);

    /// <summary>Tore und Gegentore nach 15-Minuten-Abschnitten aus den Torereignissen. Ein Spiel zählt nur, wenn
    /// seine Torereignisse den Spielstand vollständig abbilden (ausführliche Simulation); bei schnell simulierten
    /// oder von Hand eingetragenen Spielen fehlen sie. Die Verlängerung erscheint nur, wenn ein gezähltes Spiel in
    /// die Verlängerung ging.</summary>
    public static TorabschnittAuswertung Torabschnitte(IReadOnlyList<MannschaftsSpiel> spieleDerMannschaft)
    {
        var mitVerlauf = spieleDerMannschaft.Where(HatTorverlauf).ToList();
        var tore = mitVerlauf.SelectMany(s => TorMinuten(s, eigene: true)).ToList();
        var gegentore = mitVerlauf.SelectMany(s => TorMinuten(s, eigene: false)).ToList();

        var abschnitte = _abschnitte
            .Select(a => new Torabschnitt(
                a.Bezeichnung,
                tore.Count(m => m >= a.Von && m <= a.Bis),
                gegentore.Count(m => m >= a.Von && m <= a.Bis)))
            .ToList();

        if (mitVerlauf.Any(s => s.Spiel.Ergebnis!.HeimToreVerlaengerung is not null))
            abschnitte.Add(new Torabschnitt(VERLAENGERUNG, tore.Count(m => m > 90), gegentore.Count(m => m > 90)));

        return new TorabschnittAuswertung(abschnitte, mitVerlauf.Count, spieleDerMannschaft.Count);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static bool HatTorverlauf(MannschaftsSpiel spiel)
        => spiel.Spiel.Ereignisse.Count > 0
           && spiel.Spiel.Ereignisse.Count(IstTor) == spiel.Tore + spiel.Gegentore;

    private static IEnumerable<int> TorMinuten(MannschaftsSpiel spiel, bool eigene)
        => spiel.Spiel.Ereignisse
            .Where(e => IstTor(e) && (e.MannschaftId == spiel.Mannschaft.Id) == eigene)
            .Select(e => e.Minute);

    // Tore im Spiel; Elfmeterschießen zählt nicht zum Spielstand.
    private static bool IstTor(SpielEreignis ereignis)
        => ereignis.EreignisArt is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal;
}
