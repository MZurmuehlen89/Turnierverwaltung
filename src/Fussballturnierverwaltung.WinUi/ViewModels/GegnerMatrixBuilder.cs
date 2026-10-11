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
    /// Auswärtsgegner — jede Zelle ist eine eigene Instanz, damit die Hervorhebung pro Vorkommen gesetzt wird.</summary>
    public static ObservableCollection<GegnerMatrixTopf> Aufbauen(Auslosung detail)
    {
        var plan = GegnerMatrixPlan.Aufbauen(detail);
        var spalten = plan
            .SelectMany(t => new[] { new GegnerMatrixSpalte(t.Name, "Heim"), new GegnerMatrixSpalte(t.Name, "Auswärts") })
            .ToList();

        return new ObservableCollection<GegnerMatrixTopf>(plan.Select(topf => new GegnerMatrixTopf(
            topf.Name,
            spalten,
            topf.Zeilen.Select(Zeile).ToList())));
    }

    private static GegnerMatrixZeile Zeile(GegnerPlanZeile zeile) => new()
    {
        Team   = Zelle(zeile.Team),
        Gegner = zeile.GegnerJeTopf.SelectMany(g => new[] { Zelle(g.Heim), Zelle(g.Auswaerts) }).ToList(),
    };

    private static GegnerMatrixZelle Zelle(GegnerPlanTeam? team)
    {
        if (team is null) return new GegnerMatrixZelle { Kurzname = LEERE_ZELLE };

        var name = team.Mannschaft?.Name ?? team.PlatzhalterName ?? "?";
        return new GegnerMatrixZelle
        {
            Schluessel = team.Schluessel,
            Name       = name,
            Kurzname   = string.IsNullOrWhiteSpace(team.Mannschaft?.Kurzname) ? name : team.Mannschaft.Kurzname,
            Land       = team.Mannschaft?.Land?.Name,
            Wappen     = MannschaftsIcon.Fuer(team.Mannschaft),
        };
    }
}
