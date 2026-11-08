using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Große Kennzahl oben auf der Turnierseite.</summary>
public record Kachel(string Bezeichnung, string Wert, string? Erlaeuterung = null);

/// <summary>Ein Spiel einer Bestenliste in Heim-/Auswärts-Reihenfolge; <paramref name="Wert"/> ist der Wert, nach dem
/// die Liste sortiert ist.</summary>
public record TurnierSpielZeile(string Runde, Mannschaft Heim, Mannschaft Auswaerts, string Ergebnis, string Detail, string Wert)
{
    public bool HatDetail => Detail.Length > 0;
}

public record SpielListe(string Titel, string WertTitel, IReadOnlyList<TurnierSpielZeile> Zeilen, string? Hinweis)
{
    public bool HatZeilen => Zeilen.Count > 0;
    public bool HatHinweis => Hinweis is not null;
}

/// <summary>Alles, was der Statistik-Bereich „Turnier“ (#432) zeigt: ganz oben die Platzierungen (#527, <c>null</c> ohne
/// Turniersieger), dann Kacheln, darunter Verteilungen, Bestenlisten von Spielen und zuletzt Ergebnisse und Torabschnitte.</summary>
public record TurnierSeite(
    PlatzierungsBlock? Platzierungen,
    IReadOnlyList<Kachel> Kacheln,
    IReadOnlyList<KennzahlBlock> Verteilungen,
    IReadOnlyList<SpielListe> Spiellisten,
    IReadOnlyList<KennzahlBlock> Weitere);

/// <summary>Baut die <see cref="TurnierSeite"/> aus <see cref="TurnierStatistikBerechnung"/>.</summary>
public static class TurnierSeitenBuilder
{
    private const int LISTEN_LAENGE = 5;

    public static TurnierSeite Erstellen(IReadOnlyList<MannschaftsSpiel> spiele, Turnierausgang ausgang)
    {
        var runden = TurnierStatistikBerechnung.ToreJeRunde(spiele);

        return new TurnierSeite(
            PlatzierungsBlockBuilder.Erstellen(ausgang),
            Kacheln(runden),
            [.. new[] { Ausgaenge(spiele), Entscheidungen(spiele), ToreJeRunde(runden) }.OfType<KennzahlBlock>()],
            [HoechsteSiege(spiele), TorreichsteSpiele(spiele), Ueberraschungen(spiele)],
            [HaeufigsteErgebnisse(spiele), Torabschnitte(spiele)]);
    }

    // ── Kacheln und Verteilungen ──────────────────────────────────────────

    private static IReadOnlyList<Kachel> Kacheln(IReadOnlyList<RundenTore> runden)
    {
        int spiele = runden.Sum(r => r.Spiele);
        int tore = runden.Sum(r => r.Tore);
        return
        [
            new Kachel("Spiele", spiele.ToString()),
            new Kachel("Tore", tore.ToString(), "Inklusive Verlängerung, ohne Elfmeterschießen"),
            new Kachel("Tore pro Spiel", (spiele > 0 ? (double)tore / spiele : 0).ToString("0.00"))
        ];
    }

    private static KennzahlBlock Ausgaenge(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        var a = TurnierStatistikBerechnung.Ausgaenge(spiele);
        int n = a.SpieleMitHeimrecht;
        if (n == 0) return new KennzahlBlock("Spielausgänge", [], "Alle Spiele fanden auf neutralem Platz statt.");

        return new KennzahlBlock("Spielausgänge",
        [
            new Kennzahl("Heimsieg", a.Heimsiege.ToString(), Anteil(a.Heimsiege, n)),
            new Kennzahl("Unentschieden", a.Unentschieden.ToString(), Anteil(a.Unentschieden, n),
                "Nach Toren inklusive Verlängerung – ein Elfmeterschießen zählt als Unentschieden"),
            new Kennzahl("Auswärtssieg", a.Auswaertssiege.ToString(), Anteil(a.Auswaertssiege, n))
        ],
        a.NeutraleSpiele > 0 ? $"Ohne {Anzahl(a.NeutraleSpiele, "Spiel", "Spiele")} auf neutralem Platz." : null,
        "Spiele", "Anteil");
    }

    private static KennzahlBlock Entscheidungen(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        var entscheidungen = TurnierStatistikBerechnung.Entscheidungen(spiele);
        int n = entscheidungen.Sum(e => e.Anzahl);
        return new KennzahlBlock("Entscheidung",
            [.. entscheidungen.Select(e => new Kennzahl(EntscheidungsText(e.Art), e.Anzahl.ToString(), Anteil(e.Anzahl, n)))],
            null, "Spiele", "Anteil");
    }

    private static string EntscheidungsText(EntscheidungArt art) => art switch
    {
        EntscheidungArt.RegulaereSpielzeit => "In der regulären Spielzeit",
        EntscheidungArt.Verlaengerung      => "In der Verlängerung",
        EntscheidungArt.GoldenGoal         => "Durch Golden Goal",
        EntscheidungArt.SilverGoal         => "Durch Silver Goal",
        EntscheidungArt.Elfmeterschiessen  => "Im Elfmeterschießen",
        EntscheidungArt.Auswaertstorregel  => "Durch die Auswärtstorregel",
        EntscheidungArt.Wiederholungsspiel => "Durch Wiederholungsspiel",
        EntscheidungArt.Losentscheid       => "Per Losentscheid",
        _                                  => "Per Münzwurf"
    };

    /// <summary>Erst ab zwei Runden — bei einer stünde hier nur noch einmal die Gesamtzahl.</summary>
    private static KennzahlBlock? ToreJeRunde(IReadOnlyList<RundenTore> runden)
        => runden.Count < 2
            ? null
            : new KennzahlBlock("Tore je Runde",
                [.. runden.Select(r => new Kennzahl(
                    $"{r.Runde} ({Anzahl(r.Spiele, "Spiel", "Spiele")})", r.Tore.ToString(), r.ToreProSpiel.ToString("0.00")))],
                null, "Tore", "pro Spiel");

    // ── Bestenlisten ──────────────────────────────────────────────────────

    private static SpielListe HoechsteSiege(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        var zeilen = TurnierStatistikBerechnung.HoechsteSiege(spiele, LISTEN_LAENGE)
            .Select(s => SpielZeile(s, $"+{s.Tore - s.Gegentore}"))
            .ToList();
        return new SpielListe("Höchste Siege", "Differenz", zeilen, zeilen.Count == 0 ? "Noch kein Spiel mit Sieger." : null);
    }

    private static SpielListe TorreichsteSpiele(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        var zeilen = TurnierStatistikBerechnung.TorreichsteSpiele(spiele, LISTEN_LAENGE)
            .Select(s => SpielZeile(s, (s.Tore + s.Gegentore).ToString()))
            .ToList();
        return new SpielListe("Torreichste Spiele", "Tore", zeilen, zeilen.Count == 0 ? "Noch kein Tor gefallen." : null);
    }

    private static SpielListe Ueberraschungen(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        var zeilen = TurnierStatistikBerechnung.Ueberraschungen(spiele, LISTEN_LAENGE)
            .Select(u => SpielZeile(u.Sieger, u.RatingAbstand.ToString("0.0")))
            .ToList();
        string erklaerung = $"Siege gegen eine im Gesamtrating um mindestens "
            + $"{TurnierStatistikBerechnung.UEBERRASCHUNG_MINDESTABSTAND:0} Punkte stärkere Mannschaft; "
            + "ein Elfmeterschießen zählt nicht als Sieg.";
        return new SpielListe("Größte Überraschungen", "Rating-Abstand", zeilen,
            zeilen.Count == 0 ? $"Keine Überraschung bisher. {erklaerung}" : erklaerung);
    }

    // Immer in Heim-/Auswärts-Reihenfolge, auch wenn die Liste aus Sicht des Siegers rechnet.
    private static TurnierSpielZeile SpielZeile(MannschaftsSpiel sicht, string wert)
    {
        var spiel = sicht.Spiel;
        var e = spiel.Ergebnis!;
        return new TurnierSpielZeile(
            MannschaftsSeitenBuilder.RundenText(sicht),
            spiel.HeimMannschaft!,
            spiel.AuswaertsMannschaft!,
            $"{e.HeimToreGesamt}:{e.AuswaertsToreGesamt}",
            SpielErgebnisFormatierer.DetailText(e),
            wert);
    }

    // ── Ergebnisse und Torabschnitte ──────────────────────────────────────

    private static KennzahlBlock HaeufigsteErgebnisse(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        int n = spiele.Count(s => s.IstHeim);
        return new KennzahlBlock("Häufigste Ergebnisse",
            [.. TurnierStatistikBerechnung.HaeufigsteErgebnisse(spiele, LISTEN_LAENGE)
                .Select(e => new Kennzahl($"{e.ToreSieger}:{e.ToreVerlierer}", e.Anzahl.ToString(), Anteil(e.Anzahl, n)))],
            "Sieger zuerst, gleich ob Heim- oder Auswärtssieg; Tore inklusive Verlängerung.",
            "Spiele", "Anteil");
    }

    private static KennzahlBlock Torabschnitte(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        var auswertung = TurnierStatistikBerechnung.Torabschnitte(spiele);
        var hinweis = MannschaftsSeitenBuilder.TorabschnittHinweis(auswertung);
        if (auswertung.SpieleMitVerlauf == 0) return new KennzahlBlock("Tore nach Spielabschnitt", [], hinweis);

        int alle = auswertung.Abschnitte.Sum(a => a.Tore + a.Gegentore);
        return new KennzahlBlock("Tore nach Spielabschnitt",
            [.. auswertung.Abschnitte.Select(a => new Kennzahl(
                a.Bezeichnung, (a.Tore + a.Gegentore).ToString(), Anteil(a.Tore + a.Gegentore, alle)))],
            hinweis, "Tore", "Anteil");
    }

    // ── Bausteine ─────────────────────────────────────────────────────────

    private static string Anteil(int teil, int ganzes) => ganzes > 0 ? $"{100.0 * teil / ganzes:0} %" : "–";

    private static string Anzahl(int anzahl, string einzahl, string mehrzahl) => $"{anzahl} {(anzahl == 1 ? einzahl : mehrzahl)}";
}
