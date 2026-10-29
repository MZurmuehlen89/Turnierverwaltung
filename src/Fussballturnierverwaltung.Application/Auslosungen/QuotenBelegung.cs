using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Stand einer Quote während der Topf-Befüllung: <paramref name="Aktuell"/> Mannschaften liegen in den
/// Lostöpfen, <paramref name="Anzahl"/> sieht die Quote mindestens vor (#572). <paramref name="Schluessel"/> ist die
/// Id des Verbands bzw. Landes (#573), bei den freien Plätzen <c>null</c>.</summary>
public sealed record QuotenStand(string Name, int Aktuell, int Anzahl, int? Schluessel = null);

/// <summary>Zählt, wie weit die Lostöpfe einer Auslosung die Verbands- und Landesquoten des Turniers schon
/// ausschöpfen (#423). Zustandslos; Platzhalter in den Töpfen zählen nicht mit. Mehrere Quoten desselben Landes
/// (Kontingente, #572) bilden einen gemeinsamen Stand.</summary>
public static class QuotenBelegung
{
    /// <summary>Quoten gelten nur für die erste Runde einer Instanz (Zulassung), nicht kumulativ für Folgerunden mit
    /// eigenem Losvorgang (bestätigte Domänen-Annahme).</summary>
    public static bool GeltenFuer(int turnierRundeId, IEnumerable<TurnierRunde> instanzRunden) =>
        instanzRunden.OrderBy(r => r.Reihenfolge).FirstOrDefault()?.Id == turnierRundeId;

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

    /// <summary>Die freien Plätze (#572): der Eingang der Runde ohne die Summe der Quoten. Belegt sind sie von allen
    /// Mannschaften, die über ihre Quote hinausgehen oder aus einem Verband bzw. Land ohne Quote kommen.</summary>
    public static QuotenStand FreiePlaetze(IReadOnlyList<QuotenStand> staende, IEnumerable<Lostopf> lostoepfe, int eingang) =>
        new("Freie Plätze",
            Mannschaften(lostoepfe).Count() - staende.Sum(s => Math.Min(s.Aktuell, s.Anzahl)),
            eingang - staende.Sum(s => s.Anzahl));

    private static IEnumerable<Mannschaft> Mannschaften(IEnumerable<Lostopf> lostoepfe) =>
        lostoepfe.SelectMany(l => l.Eintraege).Select(e => e.Mannschaft).OfType<Mannschaft>();

    private static IReadOnlyList<QuotenStand> Staende<TQuote>(
        IEnumerable<TQuote> quoten, IEnumerable<int> belegteSchluessel,
        Func<TQuote, int> schluessel, Func<TQuote, string> name, Func<TQuote, int> anzahl)
    {
        var aktuell = belegteSchluessel.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        return quoten
            .GroupBy(schluessel)
            .Select(g => new QuotenStand(name(g.First()), aktuell.GetValueOrDefault(g.Key), g.Sum(anzahl), g.Key))
            .ToList();
    }
}
