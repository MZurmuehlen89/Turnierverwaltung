using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Baut aus einer abgeschlossenen Ligaphasen-Auslosung die Gegner-Matrizen (#362) — genutzt von der
/// Ergebnisanzeige und von der TV-Ziehung der Ligaphase (#364), die jeweils eigene Instanzen brauchen.</summary>
public static class GegnerMatrixBuilder
{
    private const string LEERE_ZELLE = "–";

    /// <summary>Paarungen je (Team, Topf des Gegners), getrennt nach Heim- und Auswärtsspiel des Teams.</summary>
    private sealed record SpieleJeGegnerTopf(
        Dictionary<(string Team, int GegnerTopf), AuslosungsErgebnis> Heim,
        Dictionary<(string Team, int GegnerTopf), AuslosungsErgebnis> Auswaerts);

    /// <summary>Je Lostopf eine Matrix: Zeilen = Teams des Topfs (Reihenfolge wie in der Topf-Liste), Spalten =
    /// je Gegner-Topf Heim- und Auswärtsgegner. Topf des Gegners aus den Lostopf-Einträgen, Heim/Auswärts aus der
    /// Ergebnis-Paarung — jede Zelle ist eine eigene Instanz, damit die Hervorhebung pro Vorkommen gesetzt wird.</summary>
    public static ObservableCollection<GegnerMatrixTopf> Aufbauen(Auslosung detail)
    {
        var toepfe = detail.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var spiele = SpieleZuordnen(detail.Ergebnisse, TopfIndexJeTeam(toepfe));
        var spalten = toepfe
            .SelectMany(t => new[] { new GegnerMatrixSpalte(t.Name, "Heim"), new GegnerMatrixSpalte(t.Name, "Auswärts") })
            .ToList();

        return new ObservableCollection<GegnerMatrixTopf>(toepfe.Select(topf => new GegnerMatrixTopf(
            topf.Name,
            spalten,
            topf.Eintraege.OrderBy(e => e.Id).Select(eintrag => Zeile(eintrag, toepfe.Count, spiele)).ToList())));
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

    private static GegnerMatrixZeile Zeile(LostopfEintrag eintrag, int topfAnzahl, SpieleJeGegnerTopf spiele)
    {
        var schluessel = TeamSchluessel(eintrag.MannschaftId, eintrag.PlatzhalterName) ?? "";
        var gegner = new List<GegnerMatrixZelle>();
        for (int topf = 0; topf < topfAnzahl; topf++)
        {
            gegner.Add(spiele.Heim.TryGetValue((schluessel, topf), out var heimspiel)
                ? Zelle(heimspiel.AuswaertsMannschaft, heimspiel.AuswaertsPlatzhalterName)
                : new GegnerMatrixZelle { Kurzname = LEERE_ZELLE });
            gegner.Add(spiele.Auswaerts.TryGetValue((schluessel, topf), out var auswaertsspiel)
                ? Zelle(auswaertsspiel.HeimMannschaft, auswaertsspiel.HeimPlatzhalterName)
                : new GegnerMatrixZelle { Kurzname = LEERE_ZELLE });
        }
        return new GegnerMatrixZeile
        {
            Team   = Zelle(eintrag.Mannschaft, eintrag.PlatzhalterName),
            Gegner = gegner,
        };
    }

    private static GegnerMatrixZelle Zelle(Mannschaft? mannschaft, string? platzhalterName)
    {
        var name = mannschaft?.Name ?? platzhalterName ?? "?";
        return new GegnerMatrixZelle
        {
            Schluessel = TeamSchluessel(mannschaft?.Id, platzhalterName),
            Name       = name,
            Kurzname   = string.IsNullOrWhiteSpace(mannschaft?.Kurzname) ? name : mannschaft.Kurzname,
            Land       = mannschaft?.Land?.Name,
            Wappen     = TeilnehmerAnzeige.MannschaftsIcon(mannschaft),
        };
    }

    private static string? TeamSchluessel(int? mannschaftId, string? platzhalterName) =>
        mannschaftId?.ToString() ?? (platzhalterName is null ? null : $"P:{platzhalterName}");
}
