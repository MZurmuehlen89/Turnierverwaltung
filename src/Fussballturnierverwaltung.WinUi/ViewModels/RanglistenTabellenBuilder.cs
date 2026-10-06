using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Bringt die Ranglisten aus <see cref="StatistikBerechnung"/> in die Spalten- und Zellenform der
/// Ansicht (#327) — je Auswertung die Spalten, die sie braucht.</summary>
public static class RanglistenTabellenBuilder
{
    private const double SCHMAL = 40;
    private const double BREIT = 64;
    private const double KAESTCHEN_BREITE = 26;
    private const int FORM_SPIELE = 5;

    public static StatistikTabelle Erstellen(StatistikAuswertungsart art, IReadOnlyList<MannschaftsSpiel> spiele) => art switch
    {
        StatistikAuswertungsart.Heim      => BilanzTabelle(StatistikBerechnung.BilanzRangliste(spiele, SpielortFilter.Heim)),
        StatistikAuswertungsart.Auswaerts => BilanzTabelle(StatistikBerechnung.BilanzRangliste(spiele, SpielortFilter.Auswaerts)),
        StatistikAuswertungsart.Form      => FormTabelle(StatistikBerechnung.Formtabelle(spiele, FORM_SPIELE)),
        _                                 => BilanzTabelle(StatistikBerechnung.BilanzRangliste(spiele))
    };

    // ── Tabellen je Auswertung ────────────────────────────────────────────

    private static StatistikTabelle BilanzTabelle(IReadOnlyList<BilanzEintrag> rangliste) => new(
        _bilanzSpalten,
        Zeilen(rangliste, e => e.Mannschaft, e => BilanzZellen(e.Bilanz)));

    private static StatistikTabelle FormTabelle(IReadOnlyList<FormEintrag> rangliste)
    {
        double formBreite = FORM_SPIELE * KAESTCHEN_BREITE;
        return new StatistikTabelle(
            [new StatistikSpalte("Pkt", "Punkte aus diesen Spielen", SCHMAL)],
            [.. rangliste.Select((e, index) => new StatistikZeile(
                index + 1, e.Mannschaft.Name, e.Mannschaft,
                [Zahl(e.Punkte, hervorgehoben: true)],
                [.. e.LetzteSpiele.Select(Kaestchen)],
                formBreite))],
            formBreite);
    }

    // ── Bausteine ─────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<StatistikSpalte> _bilanzSpalten =
    [
        new("Sp", "Spiele", SCHMAL),
        new("S", "Siege", SCHMAL),
        new("U", "Unentschieden (auch Spiele, die erst im Elfmeterschießen entschieden wurden)", SCHMAL),
        new("N", "Niederlagen", SCHMAL),
        new("Tore", "Tore : Gegentore, inklusive Verlängerung", BREIT),
        new("Diff", "Tordifferenz", SCHMAL),
        new("Pkt", "Punkte nach dem Regelwerk der jeweiligen Runde", SCHMAL)
    ];

    private static IReadOnlyList<StatistikZelle> BilanzZellen(Bilanz bilanz) =>
    [
        Zahl(bilanz.Spiele),
        Zahl(bilanz.Siege),
        Zahl(bilanz.Unentschieden),
        Zahl(bilanz.Niederlagen),
        new StatistikZelle($"{bilanz.Tore}:{bilanz.Gegentore}", BREIT),
        new StatistikZelle(bilanz.Tordifferenz.ToString("+0;-0;0"), SCHMAL),
        Zahl(bilanz.Punkte, hervorgehoben: true)
    ];

    private static IReadOnlyList<StatistikZeile> Zeilen<T>(
        IReadOnlyList<T> rangliste, Func<T, Mannschaft> mannschaft, Func<T, IReadOnlyList<StatistikZelle>> zellen)
        => [.. rangliste.Select((e, index) => new StatistikZeile(
            index + 1, mannschaft(e).Name, mannschaft(e), zellen(e), [], 0))];

    private static StatistikZelle Zahl(int wert, bool hervorgehoben = false)
        => new(wert.ToString(), SCHMAL, hervorgehoben);

    private static FormKaestchen Kaestchen(SpielAusgang ausgang) => ausgang switch
    {
        SpielAusgang.Sieg          => new FormKaestchen("S", 0.30),
        SpielAusgang.Unentschieden => new FormKaestchen("U", 0.10),
        _                          => new FormKaestchen("N", 0)
    };
}
