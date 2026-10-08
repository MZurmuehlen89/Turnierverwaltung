using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Stand einer Quote während der Topf-Befüllung: <paramref name="Aktuell"/> Mannschaften liegen in den
/// Lostöpfen, <paramref name="Anzahl"/> sieht die Quote vor.</summary>
public sealed record QuotenStand(string Name, int Aktuell, int Anzahl);

/// <summary>Zählt, wie weit die Lostöpfe einer Auslosung die Verbands- und Landesquoten des Turniers schon
/// ausschöpfen (#423). Zustandslos; Platzhalter in den Töpfen zählen nicht mit.</summary>
public static class QuotenBelegung
{
    /// <summary>Mannschaften, deren Land nicht mitgeladen ist, lassen sich keinem Verband zuordnen und zählen nicht.</summary>
    public static IReadOnlyList<QuotenStand> JeVerband(IEnumerable<VerbandsQuote> quoten, IEnumerable<Lostopf> lostoepfe) =>
        Staende(
            quoten,
            Mannschaften(lostoepfe).Where(m => m.Land is not null).Select(m => m.Land.VerbandId),
            q => q.VerbandId, q => q.Verband?.Name ?? $"Verband {q.VerbandId}", q => q.Anzahl);

    /// <summary>Gruppiert über <see cref="Mannschaft.LandId"/> direkt statt der Verband-Indirektion (#189).</summary>
    public static IReadOnlyList<QuotenStand> JeLand(IEnumerable<LandesQuote> quoten, IEnumerable<Lostopf> lostoepfe) =>
        Staende(
            quoten,
            Mannschaften(lostoepfe).Select(m => m.LandId),
            q => q.LandId, q => q.Land?.Name ?? $"Land {q.LandId}", q => q.Anzahl);

    private static IEnumerable<Mannschaft> Mannschaften(IEnumerable<Lostopf> lostoepfe) =>
        lostoepfe.SelectMany(l => l.Eintraege).Select(e => e.Mannschaft).OfType<Mannschaft>();

    private static IReadOnlyList<QuotenStand> Staende<TQuote>(
        IEnumerable<TQuote> quoten, IEnumerable<int> belegteSchluessel,
        Func<TQuote, int> schluessel, Func<TQuote, string> name, Func<TQuote, int> anzahl)
    {
        var aktuell = belegteSchluessel.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        return quoten
            .Select(q => new QuotenStand(name(q), aktuell.GetValueOrDefault(schluessel(q)), anzahl(q)))
            .ToList();
    }
}
