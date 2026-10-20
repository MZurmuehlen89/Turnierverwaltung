using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Statistiken;

/// <summary>Auswertungen über das ganze Turnier (Statistik-Bereich „Turnier“, #432) — zustandslos, rechnet auf den
/// Spielen aus <see cref="StatistikBerechnung.AusMannschaftsSicht"/>. Dort steht jedes Spiel zweimal; wo es einmal
/// zählen soll, gilt die Sicht der Heimmannschaft, bei Siegen die des Siegers.</summary>
public static class TurnierStatistikBerechnung
{
    /// <summary>Mindestabstand im Gesamtrating, ab dem ein Sieg als Überraschung gilt (die Ratings der Stammdaten
    /// reichen etwa von 35 bis 90).</summary>
    public const decimal UEBERRASCHUNG_MINDESTABSTAND = 5m;

    // Diese drei erscheinen immer, auch mit 0 — nach ihnen fragt die Auswertung ausdrücklich.
    private static readonly EntscheidungArt[] _immerGezeigt =
        [EntscheidungArt.RegulaereSpielzeit, EntscheidungArt.Verlaengerung, EntscheidungArt.Elfmeterschiessen];

    /// <summary>Tore je Runde in der Reihenfolge der Spiele.</summary>
    public static IReadOnlyList<RundenTore> ToreJeRunde(IEnumerable<MannschaftsSpiel> spiele)
        => [.. JeSpiel(spiele)
            .GroupBy(s => s.Spiel.TurnierRundeId)
            .Select(g => new RundenTore(g.First().Spiel.TurnierRunde.Name, g.Count(), g.Sum(s => s.Tore + s.Gegentore)))];

    /// <summary>Heimsieg/Unentschieden/Auswärtssieg nur aus Spielen mit Heimrecht; Spiele auf neutralem Platz werden
    /// nur gezählt (#327).</summary>
    public static Ausgangsverteilung Ausgaenge(IEnumerable<MannschaftsSpiel> spiele)
    {
        var alle = JeSpiel(spiele).ToList();
        var mitHeimrecht = alle.Where(s => !s.NeutralerSpielort).ToList();
        return new Ausgangsverteilung(
            mitHeimrecht.Count(s => s.Ausgang == SpielAusgang.Sieg),
            mitHeimrecht.Count(s => s.Ausgang == SpielAusgang.Unentschieden),
            mitHeimrecht.Count(s => s.Ausgang == SpielAusgang.Niederlage),
            alle.Count - mitHeimrecht.Count);
    }

    /// <summary>Spiele je Entscheidungsart: reguläre Spielzeit, Verlängerung und Elfmeterschießen immer, alle übrigen
    /// (Golden Goal, Auswärtstorregel, Los …) nur, wenn sie vorkamen.</summary>
    public static IReadOnlyList<EntscheidungsAnzahl> Entscheidungen(IEnumerable<MannschaftsSpiel> spiele)
    {
        var anzahl = JeSpiel(spiele)
            .GroupBy(s => s.Spiel.Ergebnis!.EntscheidungArt)
            .ToDictionary(g => g.Key, g => g.Count());

        return [.. Enum.GetValues<EntscheidungArt>()
            .Where(art => _immerGezeigt.Contains(art) || anzahl.ContainsKey(art))
            .Select(art => new EntscheidungsAnzahl(art, anzahl.GetValueOrDefault(art)))];
    }

    /// <summary>Die höchsten Siege aus Sicht des Siegers: größte Tordifferenz, dann die meisten Tore, sonst das
    /// frühere Spiel.</summary>
    public static IReadOnlyList<MannschaftsSpiel> HoechsteSiege(IEnumerable<MannschaftsSpiel> spiele, int anzahl)
        => [.. spiele
            .Where(s => s.Ausgang == SpielAusgang.Sieg)
            .OrderByDescending(s => s.Tore - s.Gegentore)
            .ThenByDescending(s => s.Tore)
            .Take(anzahl)];

    /// <summary>Die Spiele mit den meisten Toren (Sicht der Heimmannschaft); torlose Spiele zählen nicht.</summary>
    public static IReadOnlyList<MannschaftsSpiel> TorreichsteSpiele(IEnumerable<MannschaftsSpiel> spiele, int anzahl)
        => [.. JeSpiel(spiele)
            .Where(s => s.Tore + s.Gegentore > 0)
            .OrderByDescending(s => s.Tore + s.Gegentore)
            .Take(anzahl)];

    /// <summary>Die häufigsten Endstände, der Sieger zuerst — ein 1:2 zählt wie ein 2:1. Bei gleicher Häufigkeit
    /// steht das torärmere Ergebnis vorn.</summary>
    public static IReadOnlyList<ErgebnisHaeufigkeit> HaeufigsteErgebnisse(IEnumerable<MannschaftsSpiel> spiele, int anzahl)
        => [.. JeSpiel(spiele)
            .GroupBy(s => (Sieger: Math.Max(s.Tore, s.Gegentore), Verlierer: Math.Min(s.Tore, s.Gegentore)))
            .Select(g => new ErgebnisHaeufigkeit(g.Key.Sieger, g.Key.Verlierer, g.Count()))
            .OrderByDescending(e => e.Anzahl)
            .ThenBy(e => e.ToreSieger + e.ToreVerlierer)
            .ThenBy(e => e.ToreSieger)
            .Take(anzahl)];

    /// <summary>Siege einer im Gesamtrating um mindestens <see cref="UEBERRASCHUNG_MINDESTABSTAND"/> schwächeren
    /// Mannschaft, der größte Abstand zuerst. Ein gewonnenes Elfmeterschießen ist kein Sieg (#327).</summary>
    public static IReadOnlyList<Ueberraschung> Ueberraschungen(IEnumerable<MannschaftsSpiel> spiele, int anzahl)
        => [.. spiele
            .Where(s => s.Ausgang == SpielAusgang.Sieg)
            .Select(s => new Ueberraschung(s, s.Gegner.GesamtRating - s.Mannschaft.GesamtRating))
            .Where(u => u.RatingAbstand >= UEBERRASCHUNG_MINDESTABSTAND)
            .OrderByDescending(u => u.RatingAbstand)
            .Take(anzahl)];

    /// <summary>Tore nach Spielabschnitt über alle Spiele mit Spielverlauf (wie im Bereich „Mannschaft“, #324).
    /// <see cref="Torabschnitt.Tore"/> sind die der Heim-, <see cref="Torabschnitt.Gegentore"/> die der
    /// Auswärtsmannschaft; zusammen ergeben sie alle Tore des Abschnitts.</summary>
    public static TorabschnittAuswertung Torabschnitte(IEnumerable<MannschaftsSpiel> spiele)
        => MannschaftsStatistikBerechnung.Torabschnitte([.. JeSpiel(spiele)]);

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static IEnumerable<MannschaftsSpiel> JeSpiel(IEnumerable<MannschaftsSpiel> spiele)
        => spiele.Where(s => s.IstHeim);
}
