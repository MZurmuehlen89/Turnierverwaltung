using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Baut aus einer abgeschlossenen Ligaphasen-Auslosung die Gegner-Matrizen (#362) — genutzt von der
/// Ergebnisanzeige und von der TV-Ziehung der Ligaphase (#364), die jeweils eigene Instanzen brauchen. Wer
/// gegen wen spielt, liefert <see cref="GegnerMatrixPlan"/>; hier entstehen daraus nur die Anzeige-Zellen.</summary>
public static class GegnerMatrixBuilder
{
    private const string LEERE_ZELLE = "–";

    /// <summary>Je Lostopf eine Matrix: Zeilen = Teams des Topfs, Spalten = je Gegner-Topf Heim- und
    /// Auswärtsgegner — jede Zelle ist eine eigene Instanz, damit die Hervorhebung pro Vorkommen gesetzt wird.
    /// Ein Platzhalter erscheint mit der Mannschaft, die ihn schon sicher besetzt (<paramref name="qualifikanten"/>, #483).</summary>
    public static ObservableCollection<GegnerMatrixTopf> Aufbauen(Auslosung detail, Qualifikanten qualifikanten)
    {
        var plan = GegnerMatrixPlan.Aufbauen(detail);
        var spalten = plan
            .SelectMany(t => new[] { new GegnerMatrixSpalte(t.Name, "Heim"), new GegnerMatrixSpalte(t.Name, "Auswärts") })
            .ToList();

        return new ObservableCollection<GegnerMatrixTopf>(plan.Select(topf => new GegnerMatrixTopf(
            topf.Name,
            spalten,
            topf.Zeilen.Select(z => Zeile(z, qualifikanten)).ToList())));
    }

    private static GegnerMatrixZeile Zeile(GegnerPlanZeile zeile, Qualifikanten qualifikanten) => new()
    {
        Team   = Zelle(zeile.Team, qualifikanten),
        Gegner = zeile.GegnerJeTopf
            .SelectMany(g => new[] { Zelle(g.Heim, qualifikanten), Zelle(g.Auswaerts, qualifikanten) })
            .ToList(),
    };

    private static GegnerMatrixZelle Zelle(GegnerPlanTeam? team, Qualifikanten qualifikanten)
    {
        if (team is null) return new GegnerMatrixZelle { Kurzname = LEERE_ZELLE };

        var mannschaft = team.Mannschaft ?? qualifikanten.Fuer(team.PlatzhalterName);
        var name = mannschaft?.Name ?? team.PlatzhalterName ?? "?";
        return new GegnerMatrixZelle
        {
            Schluessel = team.Schluessel,
            Name       = name,
            Kurzname   = string.IsNullOrWhiteSpace(mannschaft?.Kurzname) ? name : mannschaft.Kurzname,
            Land       = mannschaft?.Land?.Name,
            Wappen     = MannschaftsIcon.Fuer(mannschaft),
        };
    }
}
