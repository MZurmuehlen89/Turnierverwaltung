using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Stand einer Quote im Teilnehmerpool: <paramref name="Aktuell"/> Mannschaften sind im Pool,
/// <paramref name="Anzahl"/> sieht die Quote mindestens vor (#572). <paramref name="Schluessel"/> ist die Id des Verbands
/// bzw. Landes (#573), bei den freien Plätzen <c>null</c>.</summary>
public sealed record QuotenStand(string Name, int Aktuell, int Anzahl, int? Schluessel = null)
{
    public bool IstFreiePlaetze => Schluessel is null;

    /// <summary>Eine Quote ist verletzt, solange sie nicht voll ist; die freien Plätze, wenn mehr Mannschaften sie belegen,
    /// als es gibt.</summary>
    public bool Verletzt => IstFreiePlaetze ? Aktuell > Anzahl : Aktuell < Anzahl;

    /// <summary>Mannschaften über der Quote — sie belegen freie Plätze (#576).</summary>
    public int Ueberzaehlig => IstFreiePlaetze ? 0 : Math.Max(0, Aktuell - Anzahl);

    /// <summary>Was die Quote erfüllt: bei einer Quote höchstens ihre Anzahl.</summary>
    public int Besetzt => IstFreiePlaetze ? Aktuell : Math.Min(Aktuell, Anzahl);
}

/// <summary>Zählt, wie weit ein Teilnehmerpool die Verbands- und Landesquoten des Turniers ausschöpft (#423, seit #576 der
/// Pool statt der Lostöpfe). Zustandslos. Mehrere Quoten desselben Landes (Kontingente, #572) bilden einen gemeinsamen
/// Stand.</summary>
public static class QuotenBelegung
{
    /// <summary>Quoten gelten nur für die erste Runde einer Instanz (Zulassung), nicht kumulativ für Folgerunden mit
    /// eigenem Losvorgang (bestätigte Domänen-Annahme).</summary>
    public static bool GeltenFuer(int turnierRundeId, IEnumerable<TurnierRunde> instanzRunden) =>
        instanzRunden.OrderBy(r => r.Reihenfolge).FirstOrDefault()?.Id == turnierRundeId;

    /// <summary>Mannschaften ohne bekannten Verband zählen nicht.</summary>
    public static IReadOnlyList<QuotenStand> JeVerband(IEnumerable<VerbandsKontingent> quoten, IEnumerable<AuswahlKandidat> pool) =>
        Staende(quoten, pool.Select(k => k.VerbandId).OfType<int>(), q => q.VerbandId, q => q.Name, q => q.Anzahl);

    public static IReadOnlyList<QuotenStand> JeLand(IEnumerable<LandesKontingent> quoten, IEnumerable<AuswahlKandidat> pool) =>
        Staende(quoten, pool.Select(k => k.LandId), q => q.LandId, q => q.LandName, q => q.Anzahl);

    /// <summary>Die freien Plätze (#572): der Eingang der Runde ohne die Summe der Quoten. Belegt sind sie von allen
    /// Mannschaften, die über ihre Quote hinausgehen oder aus einem Verband bzw. Land ohne Quote kommen.</summary>
    public static QuotenStand FreiePlaetze(IReadOnlyList<QuotenStand> staende, int poolGroesse, int eingang) =>
        new("Freie Plätze", poolGroesse - staende.Sum(s => s.Besetzt), eingang - staende.Sum(s => s.Anzahl));

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
