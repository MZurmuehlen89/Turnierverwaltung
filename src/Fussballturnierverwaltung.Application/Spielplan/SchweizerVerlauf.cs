using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Was der Generator des Schweizer Systems über die bisherigen Runden wissen muss — reine Auswertung der
/// vorhandenen Spiele, ohne Datenbank.</summary>
/// <param name="NaechsterSpieltag">Der Spieltag, der jetzt generiert wird.</param>
/// <param name="BereitsGespielt">Gespielte Paarungen als (kleinere, größere Id), unabhängig vom Heimrecht.</param>
/// <param name="FreilosHistorie">Teams, die schon einmal ausgesetzt haben.</param>
/// <param name="HeimBilanz">Heimspiele je Team, für den Farbausgleich.</param>
public sealed record SchweizerVerlauf(
    int NaechsterSpieltag,
    IReadOnlyList<(int TeamAId, int TeamBId)> BereitsGespielt,
    IReadOnlySet<int> FreilosHistorie,
    IReadOnlyDictionary<int, int> HeimBilanz)
{
    /// <summary>Wertet <paramref name="vorhandeneSpiele"/> der Runde aus; Spiele mit Platzhalter zählen nicht.</summary>
    public static SchweizerVerlauf AusSpielen(IReadOnlyList<Spiel> vorhandeneSpiele, IReadOnlyList<int> teilnehmerIds)
    {
        int naechsterSpieltag = vorhandeneSpiele.Count > 0
            ? vorhandeneSpiele.Select(s => s.Spieltag ?? 0).Max() + 1
            : 1;

        var echteSpiele = vorhandeneSpiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .Select(s => (Heim: s.HeimMannschaftId!.Value, Gast: s.AuswaertsMannschaftId!.Value))
            .ToList();

        return new SchweizerVerlauf(
            naechsterSpieltag,
            BereitsGespieltePaarungen(echteSpiele),
            Freilose(echteSpiele, teilnehmerIds, naechsterSpieltag),
            echteSpiele.GroupBy(s => s.Heim).ToDictionary(g => g.Key, g => g.Count()));
    }

    private static List<(int TeamAId, int TeamBId)> BereitsGespieltePaarungen(IEnumerable<(int Heim, int Gast)> spiele) =>
        [.. spiele.Select(s => (Math.Min(s.Heim, s.Gast), Math.Max(s.Heim, s.Gast))).Distinct()];

    // Für ein Freilos wird kein Spiel angelegt — ein Team, das an weniger Spieltagen gespielt hat, als bisher
    // stattgefunden haben, hat mindestens einmal ausgesetzt (jedes Team spielt je Spieltag höchstens einmal).
    private static HashSet<int> Freilose(
        IEnumerable<(int Heim, int Gast)> spiele, IEnumerable<int> teilnehmerIds, int naechsterSpieltag)
    {
        var spieltageGespielt = spiele
            .SelectMany(s => new[] { s.Heim, s.Gast })
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return [.. teilnehmerIds.Where(id => spieltageGespielt.GetValueOrDefault(id) < naechsterSpieltag - 1)];
    }
}
