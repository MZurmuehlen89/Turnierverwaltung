using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Statistiken;

/// <summary>Ranglisten eines Turniers über alle Mannschaften (#327) — zustandslos, rechnet nur auf den
/// übergebenen Spielen. Alle Ranglisten bauen auf <see cref="AusMannschaftsSicht"/> auf, damit Sieg,
/// Unentschieden und Niederlage an genau einer Stelle bestimmt werden.</summary>
public static class StatistikBerechnung
{
    /// <summary>Zerlegt jedes abgeschlossene Spiel in die Sicht beider Mannschaften, in der Reihenfolge der
    /// übergebenen Spiele (Form und Serien hängen daran). Jedes Spiel wird nach der Punkteregel seiner Runde
    /// gewertet (<paramref name="punkteRegelJeRunde"/>, Schlüssel: <see cref="Spiel.TurnierRundeId"/>).</summary>
    public static IReadOnlyList<MannschaftsSpiel> AusMannschaftsSicht(
        IEnumerable<Spiel> spiele, IReadOnlyDictionary<int, PunkteRegel> punkteRegelJeRunde)
        => [.. spiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Ergebnis is not null
                     && s.HeimMannschaft is not null && s.AuswaertsMannschaft is not null)
            .SelectMany(s => new[]
            {
                SichtErstellen(s, istHeim: true,  punkteRegelJeRunde[s.TurnierRundeId]),
                SichtErstellen(s, istHeim: false, punkteRegelJeRunde[s.TurnierRundeId])
            })];

    /// <summary>Spiele auf neutralem Platz gehören weder zur Heim- noch zur Auswärtsbilanz.</summary>
    public static bool HatHeimUndAuswaertsSpiele(IEnumerable<MannschaftsSpiel> spiele)
        => spiele.Any(s => !s.NeutralerSpielort);

    public static IReadOnlyList<BilanzEintrag> BilanzRangliste(
        IEnumerable<MannschaftsSpiel> spiele, SpielortFilter filter = SpielortFilter.Alle)
        => [.. JeMannschaft(spiele.Where(s => PasstZuFilter(s, filter)))
            .Select(g => new BilanzEintrag(g.Mannschaft, BilanzAus(g.Spiele)))
            .OrderByDescending(e => e.Bilanz.Punkte)
            .ThenByDescending(e => e.Bilanz.Tordifferenz)
            .ThenByDescending(e => e.Bilanz.Tore)
            .ThenBy(e => e.Mannschaft.Name)];

    public static IReadOnlyList<FormEintrag> Formtabelle(IEnumerable<MannschaftsSpiel> spiele, int letzte = 5)
        => [.. JeMannschaft(spiele)
            .Select(g =>
            {
                var letzteSpiele = g.Spiele.TakeLast(letzte).ToList();
                return new FormEintrag(
                    g.Mannschaft, [.. letzteSpiele.Select(s => s.Ausgang)], letzteSpiele.Sum(s => s.Punkte));
            })
            .OrderByDescending(e => e.Punkte)
            .ThenByDescending(e => e.LetzteSpiele.Count(a => a == SpielAusgang.Sieg))
            .ThenBy(e => e.Mannschaft.Name)];

    public static IReadOnlyList<HalbzeitEintrag> Halbzeittabelle(IEnumerable<MannschaftsSpiel> spiele)
        => [.. JeMannschaft(spiele.Where(s => s.HalbzeitAusgang is not null))
            .Select(g => new HalbzeitEintrag(
                g.Mannschaft,
                HalbzeitBilanzAus(g.Spiele),
                g.Spiele.Count(s => s.HalbzeitAusgang == SpielAusgang.Niederlage && s.Ausgang == SpielAusgang.Sieg),
                g.Spiele.Count(s => s.HalbzeitAusgang == SpielAusgang.Sieg && s.Ausgang == SpielAusgang.Niederlage)))
            .OrderByDescending(e => e.Bilanz.Punkte)
            .ThenByDescending(e => e.Bilanz.Tordifferenz)
            .ThenByDescending(e => e.Bilanz.Tore)
            .ThenBy(e => e.Mannschaft.Name)];

    public static IReadOnlyList<SerienEintrag> Serien(IEnumerable<MannschaftsSpiel> spiele)
        => [.. JeMannschaft(spiele)
            .Select(g => new SerienEintrag(
                g.Mannschaft,
                SerieAus(g.Spiele, s => s.Ausgang == SpielAusgang.Sieg),
                SerieAus(g.Spiele, s => s.Ausgang != SpielAusgang.Niederlage),
                SerieAus(g.Spiele, s => s.Gegentore == 0)))
            .OrderByDescending(e => e.Siege.Laengste)
            .ThenByDescending(e => e.Ungeschlagen.Laengste)
            .ThenByDescending(e => e.OhneGegentor.Laengste)
            .ThenBy(e => e.Mannschaft.Name)];

    public static IReadOnlyList<ToreEintrag> ToreUndZuNull(IEnumerable<MannschaftsSpiel> spiele)
        => [.. JeMannschaft(spiele)
            .Select(g => new ToreEintrag(
                g.Mannschaft,
                g.Spiele.Count,
                g.Spiele.Sum(s => s.Tore),
                g.Spiele.Sum(s => s.Gegentore),
                g.Spiele.Count(s => s.Gegentore == 0),
                g.Spiele.Count(s => s.Tore == 0)))
            .OrderByDescending(e => e.ToreProSpiel)
            .ThenBy(e => e.GegentoreProSpiel)
            .ThenBy(e => e.Mannschaft.Name)];

    /// <summary>Die fairste Mannschaft steht oben.</summary>
    public static IReadOnlyList<FairplayEintrag> Fairplay(IEnumerable<MannschaftsSpiel> spiele)
        => [.. JeMannschaft(spiele)
            .Select(g => new FairplayEintrag(
                g.Mannschaft, g.Spiele.Count, g.Spiele.Sum(s => s.GelbeKarten), g.Spiele.Sum(s => s.RoteKarten)))
            .OrderBy(e => e.Wertung)
            .ThenBy(e => e.RoteKarten)
            .ThenBy(e => e.Mannschaft.Name)];

    /// <summary>Nationalmannschaften werden je Verband zusammengefasst, Vereine je Land.</summary>
    public static IReadOnlyList<VerbandEintrag> Verbaende(IEnumerable<MannschaftsSpiel> spiele)
        => [.. spiele
            .GroupBy(s => VerbandName(s.Mannschaft))
            .Select(g => new VerbandEintrag(
                g.Key, g.Select(s => s.Mannschaft.Id).Distinct().Count(), BilanzAus([.. g])))
            .OrderByDescending(e => e.Bilanz.Punkte)
            .ThenByDescending(e => e.Bilanz.Tordifferenz)
            .ThenByDescending(e => e.Bilanz.Tore)
            .ThenBy(e => e.Name)];

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static MannschaftsSpiel SichtErstellen(Spiel spiel, bool istHeim, PunkteRegel punkteRegel)
    {
        var ergebnis   = spiel.Ergebnis!;
        var mannschaft = istHeim ? spiel.HeimMannschaft! : spiel.AuswaertsMannschaft!;
        return new MannschaftsSpiel(
            mannschaft,
            istHeim,
            spiel.TurnierRunde.NeutralerSpielort,
            istHeim ? ergebnis.HeimToreGesamt        : ergebnis.AuswaertsToreGesamt,
            istHeim ? ergebnis.AuswaertsToreGesamt   : ergebnis.HeimToreGesamt,
            istHeim ? ergebnis.HalbzeitHeimTore      : ergebnis.HalbzeitAuswaertsTore,
            istHeim ? ergebnis.HalbzeitAuswaertsTore : ergebnis.HalbzeitHeimTore,
            spiel.Ereignisse.Count(e => e.MannschaftId == mannschaft.Id && e.EreignisArt == EreignisArt.GelbeKarte),
            spiel.Ereignisse.Count(e => e.MannschaftId == mannschaft.Id && e.EreignisArt == EreignisArt.RoteKarte),
            punkteRegel);
    }

    private static bool PasstZuFilter(MannschaftsSpiel spiel, SpielortFilter filter) => filter switch
    {
        SpielortFilter.Heim      => !spiel.NeutralerSpielort && spiel.IstHeim,
        SpielortFilter.Auswaerts => !spiel.NeutralerSpielort && !spiel.IstHeim,
        _                        => true
    };

    private static IEnumerable<(Mannschaft Mannschaft, IReadOnlyList<MannschaftsSpiel> Spiele)> JeMannschaft(
        IEnumerable<MannschaftsSpiel> spiele)
        => spiele
            .GroupBy(s => s.Mannschaft.Id)
            .Select(g => (g.First().Mannschaft, (IReadOnlyList<MannschaftsSpiel>)[.. g]));

    private static Bilanz BilanzAus(IReadOnlyList<MannschaftsSpiel> spiele) => new(
        spiele.Count,
        spiele.Count(s => s.Ausgang == SpielAusgang.Sieg),
        spiele.Count(s => s.Ausgang == SpielAusgang.Unentschieden),
        spiele.Count(s => s.Ausgang == SpielAusgang.Niederlage),
        spiele.Sum(s => s.Tore),
        spiele.Sum(s => s.Gegentore),
        spiele.Sum(s => s.Punkte));

    private static Bilanz HalbzeitBilanzAus(IReadOnlyList<MannschaftsSpiel> spiele) => new(
        spiele.Count,
        spiele.Count(s => s.HalbzeitAusgang == SpielAusgang.Sieg),
        spiele.Count(s => s.HalbzeitAusgang == SpielAusgang.Unentschieden),
        spiele.Count(s => s.HalbzeitAusgang == SpielAusgang.Niederlage),
        spiele.Sum(s => s.HalbzeitTore!.Value),
        spiele.Sum(s => s.HalbzeitGegentore!.Value),
        spiele.Sum(s => s.PunkteFuer(s.HalbzeitAusgang!.Value)));

    private static Serie SerieAus(IEnumerable<MannschaftsSpiel> spiele, Func<MannschaftsSpiel, bool> haelt)
    {
        int laengste = 0, aktuell = 0;
        foreach (var spiel in spiele)
        {
            aktuell  = haelt(spiel) ? aktuell + 1 : 0;
            laengste = Math.Max(laengste, aktuell);
        }
        return new Serie(laengste, aktuell);
    }

    private static string VerbandName(Mannschaft mannschaft)
        => mannschaft.Mannschaftsart == Mannschaftsart.Nationalmannschaft
            ? mannschaft.Land.Verband.Name
            : mannschaft.Land.Name;
}
