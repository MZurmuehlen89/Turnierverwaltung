using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Baut aus einer abgeschlossenen Ligaphasen-Auslosung die Gegner-Matrizen (#362) — genutzt von der
/// Ergebnisanzeige und von der TV-Ziehung der Ligaphase (#364), die jeweils eigene Instanzen brauchen.</summary>
public static class GegnerMatrixBuilder
{
    private static string? TeamSchluessel(int? mannschaftId, string? platzhalterName) =>
        mannschaftId?.ToString() ?? (platzhalterName is null ? null : $"P:{platzhalterName}");

    private static GegnerMatrixZelle MatrixZelle(Mannschaft? mannschaft, string? platzhalterName) => new()
    {
        Schluessel = TeamSchluessel(mannschaft?.Id, platzhalterName),
        Name       = mannschaft?.Name ?? platzhalterName ?? "?",
        Kurzname   = string.IsNullOrWhiteSpace(mannschaft?.Kurzname) ? mannschaft?.Name ?? platzhalterName ?? "?" : mannschaft.Kurzname,
        Land       = mannschaft?.Land?.Name,
        Wappen     = TeilnehmerAnzeige.MannschaftsIcon(mannschaft),
    };

    /// <summary>Je Lostopf eine Matrix: Zeilen = Teams des Topfs (Reihenfolge wie in der Topf-Liste), Spalten =
    /// je Gegner-Topf Heim- und Auswärtsgegner. Topf des Gegners aus den Lostopf-Einträgen, Heim/Auswärts aus der
    /// Ergebnis-Paarung — jede Zelle ist eine eigene Instanz, damit die Hervorhebung pro Vorkommen gesetzt wird.</summary>
    public static ObservableCollection<GegnerMatrixTopf> Aufbauen(Auslosung detail)
    {
        var toepfe = detail.Lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        var topfJeTeam = new Dictionary<string, int>();
        for (int i = 0; i < toepfe.Count; i++)
            foreach (var eintrag in toepfe[i].Eintraege)
                if (TeamSchluessel(eintrag.MannschaftId, eintrag.PlatzhalterName) is { } schluessel)
                    topfJeTeam[schluessel] = i;

        // (Team, Topf des Gegners) → Ergebnis-Paarung, getrennt nach Heim- und Auswärtsspiel
        var heimSpiele      = new Dictionary<(string, int), AuslosungsErgebnis>();
        var auswaertsSpiele = new Dictionary<(string, int), AuslosungsErgebnis>();
        foreach (var e in detail.Ergebnisse.Where(e => !e.IstFreilos))
        {
            var heim = TeamSchluessel(e.HeimMannschaftId, e.HeimPlatzhalterName);
            var gast = TeamSchluessel(e.AuswaertsMannschaftId, e.AuswaertsPlatzhalterName);
            if (heim is null || gast is null ||
                !topfJeTeam.TryGetValue(heim, out int heimTopf) || !topfJeTeam.TryGetValue(gast, out int gastTopf))
                continue;
            heimSpiele[(heim, gastTopf)]      = e;
            auswaertsSpiele[(gast, heimTopf)] = e;
        }

        var spalten = toepfe
            .SelectMany(t => new[] { new GegnerMatrixSpalte(t.Name, "Heim"), new GegnerMatrixSpalte(t.Name, "Auswärts") })
            .ToList();

        return new ObservableCollection<GegnerMatrixTopf>(toepfe.Select(topf => new GegnerMatrixTopf(
            topf.Name,
            spalten,
            topf.Eintraege.OrderBy(e => e.Id).Select(eintrag =>
            {
                var schluessel = TeamSchluessel(eintrag.MannschaftId, eintrag.PlatzhalterName) ?? "";
                var gegner = new List<GegnerMatrixZelle>();
                for (int j = 0; j < toepfe.Count; j++)
                {
                    gegner.Add(heimSpiele.TryGetValue((schluessel, j), out var h)
                        ? MatrixZelle(h.AuswaertsMannschaft, h.AuswaertsPlatzhalterName)
                        : new GegnerMatrixZelle { Kurzname = "–" });
                    gegner.Add(auswaertsSpiele.TryGetValue((schluessel, j), out var a)
                        ? MatrixZelle(a.HeimMannschaft, a.HeimPlatzhalterName)
                        : new GegnerMatrixZelle { Kurzname = "–" });
                }
                return new GegnerMatrixZeile
                {
                    Team   = MatrixZelle(eintrag.Mannschaft, eintrag.PlatzhalterName),
                    Gegner = gegner,
                };
            }).ToList())));
    }
}
