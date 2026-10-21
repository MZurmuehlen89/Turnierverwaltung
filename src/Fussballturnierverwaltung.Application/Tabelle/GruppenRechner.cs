using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Teilt die Spiele einer Gruppenphase auf ihre Gruppen auf und rechnet daraus die Gruppentabellen und
/// die Rangfolge über Gruppen hinweg, ohne Datenbankzugriff. Die einzelne Tabelle rechnet
/// <see cref="TabellenRechner"/> (#439).</summary>
public static class GruppenRechner
{
    /// <summary>Gruppen einer Gruppenphase mit ihren Spielen. Seit Issue #397 aus der gespeicherten
    /// <see cref="Spiel.GruppenNummer"/> — auch mit noch offenen Platzhaltern vollständig und mit der
    /// ausgelosten Nummer. Rückfall für vor #397 generierte Spiele: Rekonstruktion aus den Paarungen.</summary>
    public static List<(int Nummer, IReadOnlyList<Spiel> Spiele)> GruppenErmitteln(IReadOnlyList<Spiel> alleSpiele)
    {
        if (alleSpiele.All(s => s.GruppenNummer.HasValue))
            return [.. alleSpiele
                .GroupBy(s => s.GruppenNummer!.Value)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, (IReadOnlyList<Spiel>)[.. g]))];

        return [.. GruppenAusSpielenAbleiten(alleSpiele).Select((gruppenTeamIds, index) =>
        {
            var teamIds = gruppenTeamIds.ToHashSet();
            return (index + 1, (IReadOnlyList<Spiel>)[.. alleSpiele
                .Where(s => s.HeimMannschaftId.HasValue && teamIds.Contains(s.HeimMannschaftId.Value))]);
        })];
    }

    /// <summary>Tabelle einer Gruppe samt ihrer noch offenen Plätze. <paramref name="bisSpieltag"/> begrenzt nur
    /// die gewerteten Spiele - Teilnehmer und offene Plätze ergeben sich immer aus allen Spielen der Gruppe.</summary>
    public static GruppenTabelle GruppenTabelleBerechnen(
        int nummer, IReadOnlyList<Spiel> gruppenSpiele, int? bisSpieltag,
        PunkteRegel punkteRegel, IReadOnlyList<RankingRegel> rankingRegeln, IReadOnlyDictionary<int, int>? ranglistenPlaetze = null)
    {
        var tabelle = TabellenRechner.TabelleBerechnen(
            TabellenRechner.BisSpieltag(gruppenSpiele, bisSpieltag), punkteRegel, rankingRegeln,
            teilnehmer: AufgeloesteTeilnehmer(gruppenSpiele), ranglistenPlaetze: ranglistenPlaetze);
        return new GruppenTabelle(nummer, tabelle) { OffenePlaetze = OffenePlaetze(gruppenSpiele) };
    }

    /// <summary>Rangfolge der Kandidaten an einer festen Platzierung (z.B. "Platz 3") über alle Gruppen hinweg,
    /// sortiert nach denselben Ranking-Regeln wie innerhalb einer Gruppe (Issue #184).
    /// "Direkter Vergleich"-Kriterien liefern zwischen Teams aus verschiedenen Gruppen naturgemäß keinen Treffer
    /// (kein gemeinsames Spiel) und fallen automatisch auf das nächste Kriterium durch - kein Sonderfall nötig.
    /// Die Kandidaten kommen aus denselben Gruppentabellen wie in <see cref="GruppenTabelleBerechnen"/> - gleiche
    /// Teilnehmer, gleicher Stand <paramref name="bisSpieltag"/> (#480, vorher #449).</summary>
    public static IReadOnlyList<TabellenEintrag> UebergreifendeRangfolgeBerechnen(
        IReadOnlyList<Spiel> alleSpiele, int platzierung, int? bisSpieltag, PunkteRegel punkteRegel,
        IReadOnlyList<RankingRegel> rankingRegeln, IReadOnlyDictionary<int, int>? ranglistenPlaetze = null)
    {
        // Gesamttabelle über ALLE Teams der Runde - liefert korrekte Team-Stats (die Statistik filtert
        // ohnehin je Team auf dessen eigene Spiele) und die richtige Sortierreihenfolge in einem Aufwasch.
        var gesamtTabelle = TabellenRechner.TabelleBerechnen(
            TabellenRechner.BisSpieltag(alleSpiele, bisSpieltag), punkteRegel, rankingRegeln,
            teilnehmer: AufgeloesteTeilnehmer(alleSpiele), ranglistenPlaetze: ranglistenPlaetze);

        var kandidatenIds = GruppenErmitteln(alleSpiele)
            .Select(g => GruppenTabelleBerechnen(g.Nummer, g.Spiele, bisSpieltag, punkteRegel, rankingRegeln, ranglistenPlaetze)
                .Tabelle.FirstOrDefault(e => e.Rang == platzierung)?.Mannschaft.Id)
            .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();

        var ergebnis = gesamtTabelle.Where(e => kandidatenIds.Contains(e.Mannschaft.Id)).ToList();
        for (int i = 0; i < ergebnis.Count; i++) ergebnis[i].Rang = i + 1;
        return ergebnis;
    }

    /// <summary>Alle bereits feststehenden Teams einer Gruppe — auch solche, die bisher nur gegen noch offene
    /// Platzhalter angesetzt sind und daher in keinem Spiel mit zwei bekannten Teams vorkommen.</summary>
    public static IReadOnlyList<Mannschaft> AufgeloesteTeilnehmer(IEnumerable<Spiel> spiele) =>
        [.. spiele
            .SelectMany(s => new[] { s.HeimMannschaft, s.AuswaertsMannschaft })
            .Where(m => m is not null)
            .Select(m => m!)
            .DistinctBy(m => m.Id)];

    /// <summary>Die Platzhalternamen der noch nicht aufgelösten Plätze einer Gruppe, in Spielreihenfolge.</summary>
    public static IReadOnlyList<string> OffenePlaetze(IEnumerable<Spiel> spiele) =>
        [.. spiele
            .OrderBy(s => s.Spielnummer)
            .SelectMany(s => new[]
            {
                s.HeimMannschaftId is null ? s.HeimPlatzhalterName : null,
                s.AuswaertsMannschaftId is null ? s.AuswaertsPlatzhalterName : null
            })
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Rekonstruiert Gruppen aus Spielpaarungen via Union-Find.
    /// Reihenfolge der Gruppen entspricht der Generierungsreihenfolge (kleinste Spielnummer zuerst).
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<int>> GruppenAusSpielenAbleiten(IEnumerable<Spiel> spiele)
    {
        var spieleListe = spiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .ToList();

        var parent = new Dictionary<int, int>();

        int Find(int x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        void Union(int x, int y)
        {
            var rx = Find(x); var ry = Find(y);
            if (rx != ry) parent[rx] = ry;
        }

        foreach (var s in spieleListe)
            Union(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value);

        // Gruppierungsreihenfolge anhand erster Spielnummer sicherstellen
        var teamErsteSpielnr = new Dictionary<int, int>();
        foreach (var s in spieleListe.OrderBy(s => s.Spielnummer))
        {
            teamErsteSpielnr.TryAdd(s.HeimMannschaftId!.Value, s.Spielnummer);
            teamErsteSpielnr.TryAdd(s.AuswaertsMannschaftId!.Value, s.Spielnummer);
        }

        return parent.Keys
            .GroupBy(id => Find(id))
            .OrderBy(g => g.Min(id => teamErsteSpielnr.GetValueOrDefault(id, int.MaxValue)))
            .Select(g => (IReadOnlyList<int>)g.OrderBy(id => id).ToList())
            .ToList();
    }
}
