using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ein Wert der Mannschaftsseite mit seinem Platz unter allen Mannschaften des Turniers (<c>null</c>, wo ein
/// Platz nichts aussagt, z.B. bei der Zahl der Spiele).</summary>
public record Kennzahl(string Bezeichnung, string Wert, string? PlatzText, string? Erlaeuterung = null);

public record KennzahlBlock(string Titel, IReadOnlyList<Kennzahl> Kennzahlen, string? Hinweis = null)
{
    public bool HatHinweis => Hinweis is not null;
}

/// <summary>Ein Spiel der Mannschaft aus ihrer Sicht: Ergebnis eigene Tore zuerst, „H“/„A“/„N“ für Heim, Auswärts,
/// neutralen Platz.</summary>
public record MannschaftsSpielZeile(string Runde, Mannschaft Gegner, string Ort, string Ergebnis, string Detail, FormKaestchen Ausgang)
{
    public bool HatDetail => Detail.Length > 0;
}

/// <summary>Alles, was der Statistik-Bereich „Mannschaft“ (#324) für eine Mannschaft zeigt.</summary>
public record MannschaftsSeite(
    Mannschaft Mannschaft,
    string Untertitel,
    IReadOnlyList<FormKaestchen> Form,
    StatistikTabelle Bilanz,
    IReadOnlyList<KennzahlBlock> Bloecke,
    IReadOnlyList<Torabschnitt> Torabschnitte,
    string? TorabschnittHinweis,
    IReadOnlyList<MannschaftsSpielZeile> Spiele)
{
    public bool HatTorabschnitte => Torabschnitte.Count > 0;
    public bool HatTorabschnittHinweis => TorabschnittHinweis is not null;
}

/// <summary>Baut die <see cref="MannschaftsSeite"/> aus den Ranglisten von <see cref="StatistikBerechnung"/>: je Wert
/// der Platz unter allen Mannschaften, bei der Bilanz der Platz in der Rangliste Gesamt/Heim/Auswärts.</summary>
public static class MannschaftsSeitenBuilder
{
    private const int FORM_SPIELE = 5;

    public static MannschaftsSeite? Erstellen(int mannschaftId, IReadOnlyList<MannschaftsSpiel> alleSpiele)
    {
        var eigene = alleSpiele.Where(s => s.Mannschaft.Id == mannschaftId).ToList();
        if (eigene.Count == 0) return null;

        var mannschaft = eigene[0].Mannschaft;
        var gesamt = StatistikBerechnung.BilanzRangliste(alleSpiele);
        var form = Eigener(StatistikBerechnung.Formtabelle(alleSpiele, FORM_SPIELE), mannschaftId, e => e.Mannschaft);
        var torabschnitte = MannschaftsStatistikBerechnung.Torabschnitte(eigene);

        return new MannschaftsSeite(
            mannschaft,
            $"{Anzahl(eigene.Count, "Spiel", "Spiele")} · Platz {RanglistenPlatz(gesamt, mannschaftId)} von {gesamt.Count} der Gesamtbilanz",
            [.. form.LetzteSpiele.Select(a => new FormKaestchen(a))],
            BilanzTabelle(mannschaftId, alleSpiele),
            [.. new[]
            {
                FormUndSerien(mannschaftId, alleSpiele),
                ToreUndZuNull(mannschaftId, alleSpiele),
                Halbzeit(mannschaftId, alleSpiele, eigene.Count),
                Fairplay(mannschaftId, alleSpiele)
            }.OfType<KennzahlBlock>()],
            torabschnitte.SpieleMitVerlauf > 0 ? torabschnitte.Abschnitte : [],
            TorabschnittHinweis(torabschnitte),
            [.. eigene.Select(SpielZeile)]);
    }

    // ── Bilanz ────────────────────────────────────────────────────────────

    private static StatistikTabelle BilanzTabelle(int mannschaftId, IReadOnlyList<MannschaftsSpiel> alleSpiele)
    {
        var filter = StatistikBerechnung.HatHeimUndAuswaertsSpiele(alleSpiele)
            ? new[] { SpielortFilter.Alle, SpielortFilter.Heim, SpielortFilter.Auswaerts }
            : [SpielortFilter.Alle];

        var zeilen = new List<StatistikZeile>();
        foreach (var f in filter)
        {
            var rangliste = StatistikBerechnung.BilanzRangliste(alleSpiele, f);
            int index = IndexVon(rangliste, mannschaftId, e => e.Mannschaft);
            if (index < 0) continue;
            zeilen.Add(new StatistikZeile(index + 1, BilanzName(f), null,
                RanglistenTabellenBuilder.BilanzZellen(rangliste[index].Bilanz), [], 0));
        }
        return new StatistikTabelle(RanglistenTabellenBuilder.BilanzSpalten, zeilen, NamensTitel: "");
    }

    private static string BilanzName(SpielortFilter filter) => filter switch
    {
        SpielortFilter.Heim      => "Heim",
        SpielortFilter.Auswaerts => "Auswärts",
        _                        => "Gesamt"
    };

    // ── Kennzahlblöcke ────────────────────────────────────────────────────

    private static KennzahlBlock FormUndSerien(int mannschaftId, IReadOnlyList<MannschaftsSpiel> alleSpiele)
    {
        var form = StatistikBerechnung.Formtabelle(alleSpiele, FORM_SPIELE);
        var serien = StatistikBerechnung.Serien(alleSpiele);
        var f = Eigener(form, mannschaftId, e => e.Mannschaft);
        var s = Eigener(serien, mannschaftId, e => e.Mannschaft);

        return new KennzahlBlock("Form und Serien",
        [
            Wert($"Punkte aus den letzten {FORM_SPIELE} Spielen", f.Punkte, form, e => e.Punkte, hoeherIstBesser: true),
            Wert("Längste Siegesserie", s.Siege.Laengste, serien, e => e.Siege.Laengste, hoeherIstBesser: true),
            Wert("Laufende Siegesserie", s.Siege.Aktuell, serien, e => e.Siege.Aktuell, hoeherIstBesser: true),
            Wert("Längste Serie ohne Niederlage", s.Ungeschlagen.Laengste, serien, e => e.Ungeschlagen.Laengste, hoeherIstBesser: true),
            Wert("Laufende Serie ohne Niederlage", s.Ungeschlagen.Aktuell, serien, e => e.Ungeschlagen.Aktuell, hoeherIstBesser: true),
            Wert("Längste Serie ohne Gegentor", s.OhneGegentor.Laengste, serien, e => e.OhneGegentor.Laengste, hoeherIstBesser: true),
            Wert("Laufende Serie ohne Gegentor", s.OhneGegentor.Aktuell, serien, e => e.OhneGegentor.Aktuell, hoeherIstBesser: true)
        ]);
    }

    private static KennzahlBlock ToreUndZuNull(int mannschaftId, IReadOnlyList<MannschaftsSpiel> alleSpiele)
    {
        var tore = StatistikBerechnung.ToreUndZuNull(alleSpiele);
        var t = Eigener(tore, mannschaftId, e => e.Mannschaft);

        return new KennzahlBlock("Tore und Zu-Null",
        [
            Wert("Tore", t.Tore, tore, e => e.Tore, hoeherIstBesser: true, "Inklusive Verlängerung"),
            Wert("Tore pro Spiel", t.ToreProSpiel, tore, e => e.ToreProSpiel, hoeherIstBesser: true, format: "0.00"),
            Wert("Gegentore", t.Gegentore, tore, e => e.Gegentore, hoeherIstBesser: false, "Inklusive Verlängerung"),
            Wert("Gegentore pro Spiel", t.GegentoreProSpiel, tore, e => e.GegentoreProSpiel, hoeherIstBesser: false, format: "0.00"),
            Wert("Spiele ohne Gegentor", t.ZuNullSpiele, tore, e => e.ZuNullSpiele, hoeherIstBesser: true),
            Wert("Spiele ohne eigenes Tor", t.SpieleOhneEigenesTor, tore, e => e.SpieleOhneEigenesTor, hoeherIstBesser: false)
        ]);
    }

    /// <summary>Nur mit Halbzeitstand (ausführliche Simulation oder eingetragen); fehlt er in allen Spielen der
    /// Mannschaft, entfällt der Block.</summary>
    private static KennzahlBlock? Halbzeit(int mannschaftId, IReadOnlyList<MannschaftsSpiel> alleSpiele, int spieleGesamt)
    {
        var halbzeit = StatistikBerechnung.Halbzeittabelle(alleSpiele);
        int index = IndexVon(halbzeit, mannschaftId, e => e.Mannschaft);
        if (index < 0) return null;

        var h = halbzeit[index];
        var b = h.Bilanz;
        return new KennzahlBlock("Halbzeit",
        [
            new Kennzahl("Bilanz zur Pause", $"{b.Siege} S · {b.Unentschieden} U · {b.Niederlagen} N",
                PlatzText(index + 1, halbzeit.Count), "Als wäre das Spiel zur Halbzeit beendet; Platz in der Halbzeittabelle"),
            new Kennzahl("Tore zur Pause", $"{b.Tore}:{b.Gegentore}", null),
            Wert("Gedreht", h.Gedreht, halbzeit, e => e.Gedreht, hoeherIstBesser: true, "Rückstand zur Pause, am Ende gewonnen"),
            Wert("Verspielt", h.Verspielt, halbzeit, e => e.Verspielt, hoeherIstBesser: false, "Führung zur Pause, am Ende verloren")
        ],
        b.Spiele < spieleGesamt ? $"Aus {b.Spiele} von {spieleGesamt} Spielen – nur Spiele mit Halbzeitstand." : null);
    }

    private static KennzahlBlock Fairplay(int mannschaftId, IReadOnlyList<MannschaftsSpiel> alleSpiele)
    {
        var fairplay = StatistikBerechnung.Fairplay(alleSpiele);
        var f = Eigener(fairplay, mannschaftId, e => e.Mannschaft);

        return new KennzahlBlock("Fairplay",
        [
            Wert("Gelbe Karten", f.GelbeKarten, fairplay, e => e.GelbeKarten, hoeherIstBesser: false),
            Wert("Rote Karten", f.RoteKarten, fairplay, e => e.RoteKarten, hoeherIstBesser: false),
            Wert("Wertung", f.Wertung, fairplay, e => e.Wertung, hoeherIstBesser: false,
                "Gelb zählt 1, Rot zählt 3 – wie in der Fairplay-Wertung der Gruppentabelle")
        ]);
    }

    private static string? TorabschnittHinweis(TorabschnittAuswertung auswertung)
    {
        if (auswertung.SpieleMitVerlauf == 0)
            return "Keine Torminuten vorhanden – sie entstehen nur bei ausführlicher Simulation.";
        return auswertung.SpieleMitVerlauf < auswertung.Spiele
            ? $"Aus {auswertung.SpieleMitVerlauf} von {auswertung.Spiele} Spielen – nur Spiele mit Spielverlauf (ausführliche Simulation)."
            : null;
    }

    // ── Spiele ────────────────────────────────────────────────────────────

    private static MannschaftsSpielZeile SpielZeile(MannschaftsSpiel spiel)
    {
        var e = spiel.Spiel.Ergebnis!;
        bool heim = spiel.IstHeim;
        string detail = SpielErgebnisFormatierer.DetailText(
            e.EntscheidungArt,
            heim ? e.HeimToreRegulaer : e.AuswaertsToreRegulaer,
            heim ? e.AuswaertsToreRegulaer : e.HeimToreRegulaer,
            heim ? e.HeimToreVerlaengerung : e.AuswaertsToreVerlaengerung,
            heim ? e.AuswaertsToreVerlaengerung : e.HeimToreVerlaengerung,
            heim ? e.HeimElfmetertore : e.AuswaertsElfmetertore,
            heim ? e.AuswaertsElfmetertore : e.HeimElfmetertore);

        return new MannschaftsSpielZeile(
            RundenText(spiel),
            spiel.Gegner,
            spiel.NeutralerSpielort ? "N" : heim ? "H" : "A",
            $"{spiel.Tore}:{spiel.Gegentore}",
            detail,
            new FormKaestchen(spiel.Ausgang));
    }

    /// <summary>Wie die Spalte „Runde“ der Mannschaftsansicht im Spielplan (#479): Runde, Gruppe, bei K.-o.-Paarungen
    /// Hin-/Rückspiel, sonst der Spieltag.</summary>
    private static string RundenText(MannschaftsSpiel spiel)
    {
        var s = spiel.Spiel;
        var teile = new List<string> { s.TurnierRunde.Name };
        if (s.GruppenNummer is int gruppe) teile.Add($"Gr.{gruppe}");

        if (HinRueckspielWertung.IstKoRunde(s.TurnierRunde))
        {
            if (s.TurnierRunde.MitRueckspiel) teile.Add(s.IstRueckspiel ? "Rückspiel" : "Hinspiel");
        }
        else if (s.Spieltag is int spieltag)
        {
            teile.Add($"{spieltag}. Spieltag");
        }
        return string.Join(" · ", teile);
    }

    // ── Bausteine ─────────────────────────────────────────────────────────

    private static Kennzahl Wert<T>(
        string bezeichnung, double eigenerWert, IReadOnlyList<T> rangliste, Func<T, double> wert,
        bool hoeherIstBesser, string? erlaeuterung = null, string format = "0")
    {
        int platz = MannschaftsStatistikBerechnung.Platz(eigenerWert, rangliste.Select(wert), hoeherIstBesser);
        return new Kennzahl(bezeichnung, eigenerWert.ToString(format), PlatzText(platz, rangliste.Count), erlaeuterung);
    }

    private static string PlatzText(int platz, int von) => $"Platz {platz} von {von}";

    private static int RanglistenPlatz(IReadOnlyList<BilanzEintrag> rangliste, int mannschaftId)
        => IndexVon(rangliste, mannschaftId, e => e.Mannschaft) + 1;

    private static T Eigener<T>(IReadOnlyList<T> rangliste, int mannschaftId, Func<T, Mannschaft> mannschaft)
        => rangliste[IndexVon(rangliste, mannschaftId, mannschaft)];

    private static int IndexVon<T>(IReadOnlyList<T> rangliste, int mannschaftId, Func<T, Mannschaft> mannschaft)
    {
        for (int i = 0; i < rangliste.Count; i++)
            if (mannschaft(rangliste[i]).Id == mannschaftId) return i;
        return -1;
    }

    private static string Anzahl(int anzahl, string einzahl, string mehrzahl) => $"{anzahl} {(anzahl == 1 ? einzahl : mehrzahl)}";
}
