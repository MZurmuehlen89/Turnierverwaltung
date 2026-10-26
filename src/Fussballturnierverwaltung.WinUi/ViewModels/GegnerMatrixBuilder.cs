using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Baut aus einer abgeschlossenen Ligaphasen-Auslosung die Gegner-Matrizen (#362) — genutzt von der
/// Ergebnisanzeige und von der TV-Ziehung der Ligaphase (#364), die jeweils eigene Instanzen brauchen. Wer
/// gegen wen spielt, liefert <see cref="GegnerMatrixPlan"/>; hier entstehen daraus nur die Anzeige-Zellen.</summary>
public static class GegnerMatrixBuilder
{
    private const string LEERE_ZELLE = "–";
    private const string HEIM = "Heim";
    private const string AUSWAERTS = "Auswärts";
    private static readonly Thickness _rand = new(0, 0, 2, 0);
    private static readonly Thickness _randNachTopfPaar = new(0, 0, 14, 0);

    /// <summary>Je Lostopf eine Matrix: Zeilen = Teams des Topfs, Spalten = je Gegner-Topf Heim- und
    /// Auswärtsgegner — jede Zelle ist eine eigene Instanz, damit die Hervorhebung pro Vorkommen gesetzt wird.
    /// Hat jedes Team aus jedem Topf nur einen Gegner (UECL, #469), eine Spalte je Topf mit Heim/Auswärts in der
    /// Zelle. Ein Platzhalter erscheint mit der Mannschaft, die ihn schon sicher besetzt
    /// (<paramref name="qualifikanten"/>, #483).</summary>
    public static ObservableCollection<GegnerMatrixTopf> Aufbauen(Auslosung detail, Qualifikanten qualifikanten)
    {
        var plan = GegnerMatrixPlan.Aufbauen(detail);
        bool kompakt = GegnerMatrixPlan.HatEinenGegnerJeTopf(plan);
        var spalten = kompakt ? KompakteSpalten(plan) : Spalten(plan);

        return new ObservableCollection<GegnerMatrixTopf>(plan.Select(topf => new GegnerMatrixTopf(
            topf.Name,
            spalten,
            topf.Zeilen.Select(z => kompakt ? KompakteZeile(z, qualifikanten) : Zeile(z, qualifikanten)).ToList(),
            kompakt)));
    }

    private static List<GegnerMatrixSpalte> Spalten(IReadOnlyList<GegnerPlanTopf> plan) =>
        [.. plan.SelectMany(t => new[] { new GegnerMatrixSpalte(t.Name, HEIM, _rand), new GegnerMatrixSpalte(t.Name, AUSWAERTS, _rand) })];

    private static List<GegnerMatrixSpalte> KompakteSpalten(IReadOnlyList<GegnerPlanTopf> plan) =>
        [.. plan.Select((t, index) => new GegnerMatrixSpalte(t.Name, "", RandNach(index)))];

    // Nach jedem Topf-Paar (1/2, 3/4, …) ein breiterer Abstand: aus jedem Paar ein Heim- und ein Auswärtsgegner.
    private static Thickness RandNach(int topfIndex) => topfIndex % 2 == 1 ? _randNachTopfPaar : _rand;

    private static GegnerMatrixZeile Zeile(GegnerPlanZeile zeile, Qualifikanten qualifikanten) => new()
    {
        Team   = Zelle(zeile.Team, qualifikanten),
        Gegner = zeile.GegnerJeTopf
            .SelectMany(g => new[]
            {
                Zelle(g.Heim, qualifikanten, HEIM, kompakt: false, _rand),
                Zelle(g.Auswaerts, qualifikanten, AUSWAERTS, kompakt: false, _rand),
            })
            .ToList(),
    };

    private static GegnerMatrixZeile KompakteZeile(GegnerPlanZeile zeile, Qualifikanten qualifikanten) => new()
    {
        Team   = Zelle(zeile.Team, qualifikanten),
        Gegner = zeile.GegnerJeTopf
            .Select((g, index) => g.Heim is not null
                ? Zelle(g.Heim, qualifikanten, HEIM, kompakt: true, RandNach(index))
                : Zelle(g.Auswaerts, qualifikanten, AUSWAERTS, kompakt: true, RandNach(index)))
            .ToList(),
    };

    private static GegnerMatrixZelle Zelle(
        GegnerPlanTeam? team, Qualifikanten qualifikanten, string seite, bool kompakt, Thickness rand) =>
        team is null
            ? new GegnerMatrixZelle { Kurzname = LEERE_ZELLE, Rand = rand }
            : MitTeam(team, qualifikanten, seite, kompakt, rand);

    private static GegnerMatrixZelle Zelle(GegnerPlanTeam team, Qualifikanten qualifikanten) =>
        MitTeam(team, qualifikanten, seite: null, kompakt: false, _rand);

    private static GegnerMatrixZelle MitTeam(
        GegnerPlanTeam team, Qualifikanten qualifikanten, string? seite, bool kompakt, Thickness rand)
    {
        var mannschaft = team.Mannschaft ?? qualifikanten.Fuer(team.PlatzhalterName);
        var name = mannschaft?.Name ?? team.PlatzhalterName ?? "?";
        return new GegnerMatrixZelle
        {
            Schluessel = team.Schluessel,
            Name       = name,
            Kurzname   = string.IsNullOrWhiteSpace(mannschaft?.Kurzname) ? name : mannschaft.Kurzname,
            Land       = mannschaft?.Land?.Name,
            Wappen     = MannschaftsIcon.Fuer(mannschaft),
            Seite      = seite,
            ZeigeSeite = kompakt,
            Rand       = rand,
        };
    }
}
