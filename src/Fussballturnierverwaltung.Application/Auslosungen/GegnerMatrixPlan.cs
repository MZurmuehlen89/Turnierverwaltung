using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Ein Team der Gegner-Matrix: echte Mannschaft oder Platzhalter einer Folgerunde.</summary>
public sealed record GegnerPlanTeam(Mannschaft? Mannschaft, string? PlatzhalterName)
{
    /// <summary>Eindeutiger Schlüssel des Teams (MannschaftId bzw. Platzhaltername), null = weder noch.</summary>
    public string? Schluessel => GegnerMatrixPlan.TeamSchluessel(Mannschaft?.Id, PlatzhalterName);
}

/// <summary>Die beiden Gegner eines Teams aus einem Lostopf: <paramref name="Heim"/> = Gegner im Heimspiel des
/// Teams, <paramref name="Auswaerts"/> = Gegner im Auswärtsspiel; null = (noch) kein Spiel.</summary>
public sealed record GegnerPlanPaar(GegnerPlanTeam? Heim, GegnerPlanTeam? Auswaerts);

/// <summary>Ein Team mit seinen Gegnern, je Gegner-Topf in Topf-Reihenfolge ein Paar.</summary>
public sealed record GegnerPlanZeile(GegnerPlanTeam Team, IReadOnlyList<GegnerPlanPaar> GegnerJeTopf);

public sealed record GegnerPlanTopf(string Name, IReadOnlyList<GegnerPlanZeile> Zeilen);

/// <summary>Leitet aus einer abgeschlossenen Ligaphasen-Auslosung ab, wer gegen wen aus welchem Topf zu Hause
/// und auswärts spielt (#362) — die Grundlage der Gegner-Matrix, ohne Anzeigezustand.</summary>
public static class GegnerMatrixPlan
{
    /// <summary>Paarungen je (Team, Topf des Gegners), getrennt nach Heim- und Auswärtsspiel des Teams.</summary>
    private sealed record SpieleJeGegnerTopf(
        Dictionary<(string Team, int GegnerTopf), AuslosungsErgebnis> Heim,
        Dictionary<(string Team, int GegnerTopf), AuslosungsErgebnis> Auswaerts);

    /// <summary>Je Lostopf (nach <see cref="Lostopf.Reihenfolge"/>) die Teams des Topfs in der Reihenfolge der
    /// Topf-Liste, jeweils mit Heim- und Auswärtsgegner je Gegner-Topf. Topf des Gegners aus den
    /// Lostopf-Einträgen, Heim/Auswärts aus der Ergebnis-Paarung.</summary>
    public static IReadOnlyList<GegnerPlanTopf> Aufbauen(Auslosung detail)
    {
        var toepfe = detail.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var spiele = SpieleZuordnen(detail.Ergebnisse, TopfIndexJeTeam(toepfe));

        return toepfe
            .Select(topf => new GegnerPlanTopf(
                topf.Name,
                topf.Eintraege.OrderBy(e => e.Id).Select(eintrag => Zeile(eintrag, toepfe.Count, spiele)).ToList()))
            .ToList();
    }

    private static Dictionary<string, int> TopfIndexJeTeam(IReadOnlyList<Lostopf> toepfe)
    {
        var topfJeTeam = new Dictionary<string, int>();
        for (int i = 0; i < toepfe.Count; i++)
            foreach (var eintrag in toepfe[i].Eintraege)
                if (TeamSchluessel(eintrag.MannschaftId, eintrag.PlatzhalterName) is { } schluessel)
                    topfJeTeam[schluessel] = i;
        return topfJeTeam;
    }

    private static SpieleJeGegnerTopf SpieleZuordnen(
        IEnumerable<AuslosungsErgebnis> ergebnisse, Dictionary<string, int> topfJeTeam)
    {
        var spiele = new SpieleJeGegnerTopf([], []);
        foreach (var e in ergebnisse.Where(e => !e.IstFreilos))
        {
            var heim = TeamSchluessel(e.HeimMannschaftId, e.HeimPlatzhalterName);
            var gast = TeamSchluessel(e.AuswaertsMannschaftId, e.AuswaertsPlatzhalterName);
            if (heim is null || gast is null ||
                !topfJeTeam.TryGetValue(heim, out int heimTopf) || !topfJeTeam.TryGetValue(gast, out int gastTopf))
                continue;
            spiele.Heim[(heim, gastTopf)]      = e;
            spiele.Auswaerts[(gast, heimTopf)] = e;
        }
        return spiele;
    }

    private static GegnerPlanZeile Zeile(LostopfEintrag eintrag, int topfAnzahl, SpieleJeGegnerTopf spiele)
    {
        var schluessel = TeamSchluessel(eintrag.MannschaftId, eintrag.PlatzhalterName) ?? "";
        var gegner = new List<GegnerPlanPaar>();
        for (int topf = 0; topf < topfAnzahl; topf++)
        {
            gegner.Add(new GegnerPlanPaar(
                spiele.Heim.TryGetValue((schluessel, topf), out var heimspiel)
                    ? new GegnerPlanTeam(heimspiel.AuswaertsMannschaft, heimspiel.AuswaertsPlatzhalterName)
                    : null,
                spiele.Auswaerts.TryGetValue((schluessel, topf), out var auswaertsspiel)
                    ? new GegnerPlanTeam(auswaertsspiel.HeimMannschaft, auswaertsspiel.HeimPlatzhalterName)
                    : null));
        }
        return new GegnerPlanZeile(new GegnerPlanTeam(eintrag.Mannschaft, eintrag.PlatzhalterName), gegner);
    }

    internal static string? TeamSchluessel(int? mannschaftId, string? platzhalterName) =>
        mannschaftId?.ToString() ?? (platzhalterName is null ? null : $"P:{platzhalterName}");
}
