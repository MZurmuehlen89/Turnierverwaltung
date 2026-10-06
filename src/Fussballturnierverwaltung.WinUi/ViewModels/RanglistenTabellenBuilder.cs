using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Bringt die Ranglisten aus <see cref="StatistikBerechnung"/> in die Spalten- und Zellenform der
/// Ansicht (#327) — je Auswertung die Spalten, die sie braucht.</summary>
public static class RanglistenTabellenBuilder
{
    private const double SCHMAL = 40;
    private const double BREIT = 64;
    private const double SEHR_BREIT = 104;
    private const double KAESTCHEN_BREITE = 26;
    private const int FORM_SPIELE = 5;

    public static StatistikTabelle Erstellen(StatistikAuswertungsart art, IReadOnlyList<MannschaftsSpiel> spiele) => art switch
    {
        StatistikAuswertungsart.Heim      => BilanzTabelle(StatistikBerechnung.BilanzRangliste(spiele, SpielortFilter.Heim)),
        StatistikAuswertungsart.Auswaerts => BilanzTabelle(StatistikBerechnung.BilanzRangliste(spiele, SpielortFilter.Auswaerts)),
        StatistikAuswertungsart.Form      => FormTabelle(StatistikBerechnung.Formtabelle(spiele, FORM_SPIELE)),
        StatistikAuswertungsart.Halbzeit  => HalbzeitTabelle(StatistikBerechnung.Halbzeittabelle(spiele)),
        StatistikAuswertungsart.Serien    => SerienTabelle(StatistikBerechnung.Serien(spiele)),
        StatistikAuswertungsart.Tore      => ToreTabelle(StatistikBerechnung.ToreUndZuNull(spiele)),
        StatistikAuswertungsart.Fairplay  => FairplayTabelle(StatistikBerechnung.Fairplay(spiele)),
        StatistikAuswertungsart.Verbaende => VerbaendeTabelle(StatistikBerechnung.Verbaende(spiele)),
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
                [.. e.LetzteSpiele.Select(ausgang => new FormKaestchen(ausgang))],
                formBreite))],
            formBreite);
    }

    private static StatistikTabelle HalbzeitTabelle(IReadOnlyList<HalbzeitEintrag> rangliste) => new(
        [
            .. _bilanzSpalten.Select(s => s.Titel == "Tore" ? s with { Erlaeuterung = "Tore : Gegentore zur Pause" } : s),
            new("Gedreht", "Rückstand zur Pause, am Ende gewonnen", BREIT),
            new("Verspielt", "Führung zur Pause, am Ende verloren", BREIT)
        ],
        Zeilen(rangliste, e => e.Mannschaft, e =>
            [.. BilanzZellen(e.Bilanz), Zahl(e.Gedreht, breite: BREIT), Zahl(e.Verspielt, breite: BREIT)]));

    private static StatistikTabelle SerienTabelle(IReadOnlyList<SerienEintrag> rangliste) => new(
        [
            new("Siege", "Längste Siegesserie", SEHR_BREIT),
            new("aktuell", "Laufende Siegesserie", BREIT),
            new("Ungeschlagen", "Längste Serie ohne Niederlage", SEHR_BREIT),
            new("aktuell", "Laufende Serie ohne Niederlage", BREIT),
            new("Ohne Gegentor", "Längste Serie ohne Gegentor", SEHR_BREIT),
            new("aktuell", "Laufende Serie ohne Gegentor", BREIT)
        ],
        Zeilen(rangliste, e => e.Mannschaft, e =>
            [.. SerienZellen(e.Siege), .. SerienZellen(e.Ungeschlagen), .. SerienZellen(e.OhneGegentor)]));

    private static StatistikTabelle ToreTabelle(IReadOnlyList<ToreEintrag> rangliste) => new(
        [
            new("Sp", "Spiele", SCHMAL),
            new("Tore", "Erzielte Tore, inklusive Verlängerung", BREIT),
            new("Ø", "Tore pro Spiel", BREIT),
            new("Gegentore", "Kassierte Tore, inklusive Verlängerung", SEHR_BREIT),
            new("Ø", "Gegentore pro Spiel", BREIT),
            new("Zu Null", "Spiele ohne Gegentor", BREIT),
            new("Ohne Tor", "Spiele ohne eigenes Tor", BREIT)
        ],
        Zeilen(rangliste, e => e.Mannschaft, e =>
        [
            Zahl(e.Spiele),
            Zahl(e.Tore, breite: BREIT),
            new StatistikZelle(e.ToreProSpiel.ToString("0.00"), BREIT, Hervorgehoben: true),
            Zahl(e.Gegentore, breite: SEHR_BREIT),
            new StatistikZelle(e.GegentoreProSpiel.ToString("0.00"), BREIT),
            Zahl(e.ZuNullSpiele, breite: BREIT),
            Zahl(e.SpieleOhneEigenesTor, breite: BREIT)
        ]));

    private static StatistikTabelle FairplayTabelle(IReadOnlyList<FairplayEintrag> rangliste) => new(
        [
            new("Sp", "Spiele", SCHMAL),
            new("Gelb", "Gelbe Karten", BREIT),
            new("Rot", "Rote Karten", BREIT),
            new("Wertung", "Gelb zählt 1, Rot zählt 3 – wie in der Fairplay-Wertung der Gruppentabelle", BREIT)
        ],
        Zeilen(rangliste, e => e.Mannschaft, e =>
        [
            Zahl(e.Spiele),
            Zahl(e.GelbeKarten, breite: BREIT),
            Zahl(e.RoteKarten, breite: BREIT),
            Zahl(e.Wertung, hervorgehoben: true, breite: BREIT)
        ]));

    private static StatistikTabelle VerbaendeTabelle(IReadOnlyList<VerbandEintrag> rangliste) => new(
        [new("Teams", "Mannschaften im Turnier", BREIT), .. _bilanzSpalten],
        [.. rangliste.Select((e, index) => new StatistikZeile(
            index + 1, e.Name, null, [Zahl(e.Mannschaften, breite: BREIT), .. BilanzZellen(e.Bilanz)], [], 0))],
        NamensTitel: "Verband / Land");

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

    private static IReadOnlyList<StatistikZelle> SerienZellen(Serie serie) =>
        [Zahl(serie.Laengste, hervorgehoben: true, breite: SEHR_BREIT), Zahl(serie.Aktuell, breite: BREIT)];

    private static StatistikZelle Zahl(int wert, bool hervorgehoben = false, double breite = SCHMAL)
        => new(wert.ToString(), breite, hervorgehoben);
}
