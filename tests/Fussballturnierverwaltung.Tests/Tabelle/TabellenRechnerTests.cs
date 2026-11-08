using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #439: Rechenregeln der Tabelle direkt gegen den Rechner, ohne Datenbank. Der Weg über Runde,
/// Regelwerk und Repositories ist in <see cref="TabellenServiceTests"/> abgesichert.</summary>
public class TabellenRechnerTests
{
    private static readonly PunkteRegel _dreiPunkte =
        new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

    private static readonly Dictionary<int, Mannschaft> _teams = Enumerable.Range(1, 8)
        .ToDictionary(id => id, id => new Mannschaft { Id = id, Name = $"Team {id}" });

    private static int _naechsteSpielnummer;

    private static Spiel Spiel(int heimId, int auswaertsId, int? heimTore = null, int? auswaertsTore = null, int? gruppe = null)
    {
        var spiel = Angesetzt(_teams[heimId], _teams[auswaertsId], gruppe);
        if (heimTore is null || auswaertsTore is null) return spiel;

        spiel.Status = SpielStatus.Abgeschlossen;
        spiel.Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer = heimTore.Value,
            AuswaertsToreRegulaer = auswaertsTore.Value,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
        };
        return spiel;
    }

    private static Spiel Angesetzt(Mannschaft? heim, Mannschaft? auswaerts, int? gruppe = null) => new()
    {
        Spielnummer = Interlocked.Increment(ref _naechsteSpielnummer),
        HeimMannschaftId = heim?.Id, HeimMannschaft = heim,
        AuswaertsMannschaftId = auswaerts?.Id, AuswaertsMannschaft = auswaerts,
        GruppenNummer = gruppe,
        Status = SpielStatus.Geplant
    };

    private static RankingRegel[] Regeln(params RankingRegelArt[] arten) =>
        [.. arten.Select(art => new RankingRegel
        {
            Regelart = art,
            Sortierrichtung = art is RankingRegelArt.Losentscheid or RankingRegelArt.Koeffizient
                ? Sortierrichtung.Aufsteigend
                : Sortierrichtung.Absteigend
        })];

    private static int[] Reihenfolge(IReadOnlyList<TabellenEintrag> tabelle) =>
        [.. tabelle.OrderBy(e => e.Rang).Select(e => e.Mannschaft.Id)];

    // ── Tabelle ───────────────────────────────────────────────────────────

    [Fact]
    public void TabelleBerechnen_OhneSpiele_IstLeer()
    {
        TabellenRechner.TabelleBerechnen([], new(_dreiPunkte, [])).Should().BeEmpty();
    }

    [Fact]
    public void TabelleBerechnen_ZaehltNurSpieleMitErgebnis()
    {
        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(1, 2, 2, 0), Spiel(2, 1)], new(_dreiPunkte, []));

        var sieger = tabelle.Single(e => e.Mannschaft.Id == 1);
        sieger.Spiele.Should().Be(1);
        sieger.Punkte.Should().Be(3);
        sieger.Tordifferenz.Should().Be(2);
        tabelle.Single(e => e.Mannschaft.Id == 2).Niederlagen.Should().Be(1);
    }

    [Fact]
    public void TabelleBerechnen_OhneRankingRegeln_OrdnetNachPunktenTordifferenzToren()
    {
        // Alle vier haben drei Punkte: 2 vor 1 über die Tordifferenz (+3 gegen +1), 4 vor 3 bei gleicher
        // Tordifferenz (0) über die erzielten Tore (5 gegen 2).
        Spiel[] spiele = [Spiel(1, 3, 1, 0), Spiel(2, 4, 3, 0), Spiel(3, 5, 2, 1), Spiel(4, 6, 5, 2)];

        var tabelle = TabellenRechner.TabelleBerechnen(spiele, new(_dreiPunkte, []));

        Reihenfolge(tabelle).Take(4).Should().Equal(2, 1, 4, 3);
    }

    [Fact]
    public void TabelleBerechnen_DirekterVergleichEntscheidetVorDerTordifferenz()
    {
        // 1 schlägt 2 im direkten Duell, 2 hat aber die deutlich bessere Tordifferenz.
        Spiel[] spiele = [Spiel(1, 2, 1, 0), Spiel(2, 3, 5, 0), Spiel(3, 1, 0, 1), Spiel(2, 1, 0, 0), Spiel(3, 2, 0, 1), Spiel(1, 3, 0, 1)];

        var mitDirektemVergleich = TabellenRechner.TabelleBerechnen(
            spiele, new(_dreiPunkte, Regeln(RankingRegelArt.Punkte, RankingRegelArt.DirekterVergleichPunkte, RankingRegelArt.Tordifferenz)));
        var nurTordifferenz = TabellenRechner.TabelleBerechnen(
            spiele, new(_dreiPunkte, Regeln(RankingRegelArt.Punkte, RankingRegelArt.Tordifferenz)));

        mitDirektemVergleich.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(7);
        mitDirektemVergleich.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(7);
        Reihenfolge(mitDirektemVergleich).Should().Equal(1, 2, 3);
        Reihenfolge(nurTordifferenz).Should().Equal(2, 1, 3);
    }

    // ── Direkter Vergleich als Mini-Tabelle (#628) ────────────────────────

    private static RankingRegel[] UefaRegeln() => Regeln(
        RankingRegelArt.Punkte, RankingRegelArt.DirekterVergleichPunkte, RankingRegelArt.DirekterVergleichTordifferenz,
        RankingRegelArt.DirekterVergleichErzielteTore, RankingRegelArt.Tordifferenz, RankingRegelArt.ErzielteTore,
        RankingRegelArt.Losentscheid);

    private static IEnumerable<T[]> Permutationen<T>(T[] elemente) =>
        elemente.Length <= 1
            ? [elemente]
            : elemente.SelectMany((e, i) => Permutationen([.. elemente.Where((_, j) => j != i)]).Select(rest => (T[])[e, .. rest]));

    [Fact]
    public void TabelleBerechnen_DreierZyklus_EntscheidetDieMiniTabelle_InJederSpielreihenfolge()
    {
        // 1 schlägt 2 1:0, 2 schlägt 3 1:0, 3 schlägt 1 3:0 – alle drei mit 3 Punkten. Paarweise ist das ein Kreis
        // (bisher je nach Spielreihenfolge eine andere Rangfolge), die Mini-Tabelle ordnet nach Tordifferenz:
        // 3 (+2), 2 (0), 1 (−2).
        var paare = new[] { (1, 2, 1, 0), (2, 3, 1, 0), (3, 1, 3, 0) };

        foreach (var reihenfolge in Permutationen(paare))
        {
            var tabelle = TabellenRechner.TabelleBerechnen(
                [.. reihenfolge.Select(p => Spiel(p.Item1, p.Item2, p.Item3, p.Item4))], new(_dreiPunkte, UefaRegeln()));

            Reihenfolge(tabelle).Should().Equal([3, 2, 1], $"Spielreihenfolge {string.Join(", ", reihenfolge)}");
        }
    }

    [Theory]
    // Die Mini-Tabelle von 1, 2 und 3 trennt nur 3 ab (mehr Tore untereinander). 1 und 2 bleiben gleich: neu
    // gerechnet entscheidet ihr Duell (1 gewann), sonst die Tordifferenz aus allen Spielen (2 schlug 4 höher).
    [InlineData(true, new[] { 3, 1, 2, 4 })]
    [InlineData(false, new[] { 3, 2, 1, 4 })]
    public void TabelleBerechnen_MiniTabelleTrenntNurEinenTeil_RechnetSieJeNachRegelsetFuerDieUebrigenNeu(
        bool wiederholen, int[] erwartet)
    {
        Spiel[] spiele =
        [
            Spiel(1, 2, 1, 0), Spiel(2, 3, 2, 1), Spiel(3, 1, 2, 1),
            Spiel(1, 4, 1, 0), Spiel(2, 4, 3, 0), Spiel(3, 4, 1, 0)
        ];

        var tabelle = TabellenRechner.TabelleBerechnen(
            spiele, new(_dreiPunkte, UefaRegeln(), DirektenVergleichWiederholen: wiederholen));

        tabelle.Where(e => e.Mannschaft.Id != 4).Should().OnlyContain(e => e.Punkte == 6);
        Reihenfolge(tabelle).Should().Equal(erwartet);
    }

    [Fact]
    public void TabelleBerechnen_DuelleDerGleichenStehenNochAus_DirekterVergleichEntscheidetNichts()
    {
        // 1, 2 und 3 haben je 3 Punkte; gespielt ist nur 1–2 (1:0), 1–3 und 2–3 stehen noch aus. Die Mini-Tabelle
        // setzte 1 allein deshalb vorn, weil es schon gegen einen Rivalen gespielt hat – stattdessen entscheiden
        // Tordifferenz (2 und 3 je +2, 1 nur +1) und Tore (2 vor 3).
        Spiel[] spiele =
        [
            Spiel(1, 2, 1, 0), Spiel(2, 4, 3, 0), Spiel(3, 4, 2, 0),
            Spiel(1, 3), Spiel(2, 3), Spiel(1, 4)
        ];

        var tabelle = TabellenRechner.TabelleBerechnen(spiele, new(_dreiPunkte, UefaRegeln(), DirektenVergleichWiederholen: true));

        Reihenfolge(tabelle).Should().Equal(2, 3, 1, 4);
    }

    [Fact]
    public void TabelleBerechnen_StandNachSpieltag_RueckspielDanachSteht_ZuDiesemStandNochAus()
    {
        // Nach Spieltag 1 haben 1 (schlug 2 1:0) und 2 (schlug 3 3:0) je 3 Punkte. Das Rückspiel 2–1 an Spieltag 2 ist
        // längst gespielt, zum Stand nach Spieltag 1 aber noch offen: der direkte Vergleich entscheidet dort nichts,
        // 2 steht mit der besseren Tordifferenz vorn. Ohne den Spielplan sähe der Rechner nur das Hinspiel.
        var hinspiel = Spiel(1, 2, 1, 0);
        hinspiel.Spieltag = 1;
        var zweiGegenDrei = Spiel(2, 3, 3, 0);
        zweiGegenDrei.Spieltag = 1;
        var rueckspiel = Spiel(2, 1, 0, 0);
        rueckspiel.Spieltag = 2;
        Spiel[] spielplan = [hinspiel, zweiGegenDrei, rueckspiel];
        var stand = TabellenRechner.BisSpieltag(spielplan, 1);

        var mitSpielplan = TabellenRechner.TabelleBerechnen(stand, new(_dreiPunkte, UefaRegeln()), spielplan: spielplan);
        var ohneSpielplan = TabellenRechner.TabelleBerechnen(stand, new(_dreiPunkte, UefaRegeln()));

        Reihenfolge(mitSpielplan).Should().Equal(2, 1, 3);
        Reihenfolge(ohneSpielplan).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void TabelleBerechnen_FifaReihenfolge_WertetDieMiniTabelleNurUnterDenNachToreGleichen()
    {
        // FIFA: erst Punkte, Tordifferenz und Tore aus allen Spielen. 1, 2 und 3 haben gleich viele Punkte, aber nur
        // 1 und 2 auch gleiche Tordifferenz und Tore – deren Duell entscheidet (2 gewann), 3 spielt in der Mini-Tabelle
        // nicht mit. Mit 3 wäre sie ein Kreis aus drei 1:0-Siegen, und der Losentscheid setzte 1 vor 2.
        Spiel[] spiele =
        [
            Spiel(2, 1, 1, 0), Spiel(1, 3, 1, 0), Spiel(3, 2, 1, 0),
            Spiel(1, 4, 1, 0), Spiel(2, 4, 1, 0), Spiel(3, 4, 3, 0)
        ];
        var fifa = Regeln(
            RankingRegelArt.Punkte, RankingRegelArt.Tordifferenz, RankingRegelArt.ErzielteTore,
            RankingRegelArt.DirekterVergleichPunkte, RankingRegelArt.DirekterVergleichTordifferenz,
            RankingRegelArt.DirekterVergleichErzielteTore, RankingRegelArt.Losentscheid);

        var tabelle = TabellenRechner.TabelleBerechnen(spiele, new(_dreiPunkte, fifa));

        Reihenfolge(tabelle).Should().Equal(3, 2, 1, 4);
    }

    [Fact]
    public void TabelleBerechnen_Losentscheid_OrdnetGleichstaendeNachTeamId()
    {
        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(3, 1, 1, 1), Spiel(2, 4, 1, 1)], new(_dreiPunkte, Regeln(RankingRegelArt.Punkte, RankingRegelArt.Losentscheid)));

        Reihenfolge(tabelle).Should().Equal(1, 2, 3, 4);
    }

    public static TheoryData<RankingRegelArt[]> RegelsetsOhneLosentscheid => new()
    {
        Array.Empty<RankingRegelArt>(),
        new[] { RankingRegelArt.Punkte, RankingRegelArt.Tordifferenz }
    };

    [Theory]
    [MemberData(nameof(RegelsetsOhneLosentscheid))]
    public void TabelleBerechnen_GleichstandNachAllenKriterien_OrdnetNachTeamId_UnabhaengigVonDerSpielreihenfolge(
        RankingRegelArt[] arten)
    {
        // #622: Alle acht Teams spielen 1:1 – kein Kriterium trennt sie, auch ohne Losentscheid im Regelset.
        Spiel[] spiele = [Spiel(8, 3, 1, 1), Spiel(5, 2, 1, 1), Spiel(7, 1, 1, 1), Spiel(4, 6, 1, 1)];

        var vorwaerts = TabellenRechner.TabelleBerechnen(spiele, new(_dreiPunkte, Regeln(arten)));
        var rueckwaerts = TabellenRechner.TabelleBerechnen([.. Enumerable.Reverse(spiele)], new(_dreiPunkte, Regeln(arten)));

        Reihenfolge(vorwaerts).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        Reihenfolge(rueckwaerts).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void TabelleBerechnen_Koeffizient_OrdnetGleichstaendeNachRanglistenplatz_TeamsOhneEintragHinten()
    {
        // Alle vier Teams punkt- und torgleich; Team 1 fehlt in der Rangliste.
        var plaetze = new Dictionary<int, int> { [4] = 1, [2] = 5, [3] = 9 };

        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(1, 2, 1, 1), Spiel(3, 4, 1, 1)],
            new(_dreiPunkte, Regeln(RankingRegelArt.Punkte, RankingRegelArt.Koeffizient, RankingRegelArt.Losentscheid), plaetze));

        Reihenfolge(tabelle).Should().Equal(4, 2, 3, 1);
    }

    [Fact]
    public void TabelleBerechnen_KoeffizientOhneRangliste_UeberlaesstDieEntscheidungDemNaechstenKriterium()
    {
        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(4, 3, 1, 1), Spiel(2, 1, 1, 1)],
            new(_dreiPunkte, Regeln(RankingRegelArt.Punkte, RankingRegelArt.Koeffizient, RankingRegelArt.Losentscheid)));

        Reihenfolge(tabelle).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void TabelleBerechnen_EntscheidungNachElfmeterschiessen_ZaehltAlsSiegDesSiegers()
    {
        var spiel = Spiel(1, 2, 1, 1);
        spiel.Ergebnis!.EntscheidungArt = EntscheidungArt.Elfmeterschiessen;
        spiel.Ergebnis.SiegerMannschaftId = 2;

        var tabelle = TabellenRechner.TabelleBerechnen([spiel], new(_dreiPunkte, []));

        tabelle.Single(e => e.Mannschaft.Id == 2).Siege.Should().Be(1);
        tabelle.Single(e => e.Mannschaft.Id == 1).Niederlagen.Should().Be(1);
        tabelle.Should().OnlyContain(e => e.Unentschieden == 0);
    }

    [Fact]
    public void TabelleBerechnen_SiegInDerVerlaengerung_ZaehltDieToreMit_AuchOhneEingetragenenSieger()
    {
        // 1:1 nach 90 Minuten, 1:0 in der Verlängerung; der Sieger ergibt sich aus den Toren.
        var spiel = Spiel(1, 2, 1, 1);
        spiel.Ergebnis!.EntscheidungArt = EntscheidungArt.Verlaengerung;
        spiel.Ergebnis.HeimToreVerlaengerung = 1;
        spiel.Ergebnis.AuswaertsToreVerlaengerung = 0;

        var tabelle = TabellenRechner.TabelleBerechnen([spiel], new(_dreiPunkte, []));

        var sieger = tabelle.Single(e => e.Mannschaft.Id == 1);
        (sieger.Siege, sieger.Tore, sieger.Gegentore, sieger.Punkte).Should().Be((1, 2, 1, 3));
        var verlierer = tabelle.Single(e => e.Mannschaft.Id == 2);
        (verlierer.Niederlagen, verlierer.Tore, verlierer.Gegentore, verlierer.Punkte).Should().Be((1, 1, 2, 0));
    }

    // ── Statistik je Team und Kriterien (#505: vor dem Umbau exakt festgehalten) ──

    [Fact]
    public void TabelleBerechnen_ZaehltJeTeamSpieleAusgaengeToreUndPunkte_BuchholzAusDenGegnerpunkten()
    {
        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(1, 2, 3, 1), Spiel(3, 1, 2, 2), Spiel(1, 4, 0, 1)], new(_dreiPunkte, []));

        var team1 = tabelle.Single(e => e.Mannschaft.Id == 1);
        team1.Spiele.Should().Be(3);
        team1.Siege.Should().Be(1);
        team1.Unentschieden.Should().Be(1);
        team1.Niederlagen.Should().Be(1);
        team1.Tore.Should().Be(5);
        team1.Gegentore.Should().Be(4);
        team1.Punkte.Should().Be(4);
        // Gegner 2 (0 Punkte) + 3 (1 Punkt) + 4 (3 Punkte)
        team1.Buchholz.Should().Be(4);
    }

    [Fact]
    public void TabelleBerechnen_PunkteRegel_BewertetSiegUnentschiedenUndNiederlage()
    {
        var punkteRegel = new PunkteRegel { PunkteFuerSieg = 2, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = -1 };

        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(1, 2, 1, 0), Spiel(2, 3, 0, 0)], new(punkteRegel, []));

        tabelle.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(2);
        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(0);
        tabelle.Single(e => e.Mannschaft.Id == 3).Punkte.Should().Be(1);
    }

    [Theory]
    // Auswärtstore: 2 trifft zweimal auswärts, 4 einmal; 3 trifft fünfmal, aber nur daheim.
    [InlineData(RankingRegelArt.AuswaertsToreGesamt, new[] { 2, 4, 1, 3 })]
    [InlineData(RankingRegelArt.ErzielteTore, new[] { 3, 2, 4, 1 })]
    public void TabelleBerechnen_AuswaertsToreGesamt_ZaehltNurToreAufDerAuswaertsseite(RankingRegelArt art, int[] erwartet)
    {
        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(1, 2, 0, 2), Spiel(3, 4, 5, 1)], new(_dreiPunkte, Regeln(art, RankingRegelArt.Losentscheid)));

        Reihenfolge(tabelle).Should().Equal(erwartet);
    }

    [Theory]
    // 2 gewinnt auswärts, 3 daheim.
    [InlineData(RankingRegelArt.AuswaertsSiege, new[] { 2, 1, 3, 4 })]
    [InlineData(RankingRegelArt.Siege, new[] { 2, 3, 1, 4 })]
    public void TabelleBerechnen_AuswaertsSiege_ZaehltNurSiegeAufDerAuswaertsseite(RankingRegelArt art, int[] erwartet)
    {
        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(1, 2, 0, 1), Spiel(3, 4, 2, 0)], new(_dreiPunkte, Regeln(art, RankingRegelArt.Losentscheid)));

        Reihenfolge(tabelle).Should().Equal(erwartet);
    }

    [Fact]
    public void TabelleBerechnen_WenigerGegentore_Aufsteigend_StelltDieBesteAbwehrNachVorn()
    {
        RankingRegel[] regeln =
        [
            new() { Regelart = RankingRegelArt.WenigerGegentore, Sortierrichtung = Sortierrichtung.Aufsteigend },
            .. Regeln(RankingRegelArt.Losentscheid)
        ];

        var tabelle = TabellenRechner.TabelleBerechnen([Spiel(1, 2, 3, 0), Spiel(3, 4, 1, 1)], new(_dreiPunkte, regeln));

        Reihenfolge(tabelle).Should().Equal(1, 3, 4, 2);
    }

    [Fact]
    public void TabelleBerechnen_FairplayWertung_GelbZaehltEinenRotDreiStrafpunkte()
    {
        var spiel = Spiel(1, 2, 0, 0);
        spiel.Ereignisse.Add(new SpielEreignis { EreignisArt = EreignisArt.GelbeKarte, MannschaftId = 1 });
        spiel.Ereignisse.Add(new SpielEreignis { EreignisArt = EreignisArt.RoteKarte, MannschaftId = 2 });
        spiel.Ereignisse.Add(new SpielEreignis { EreignisArt = EreignisArt.Tor, MannschaftId = 2 });

        var tabelle = TabellenRechner.TabelleBerechnen(
            [spiel, Spiel(3, 4, 0, 0)], new(_dreiPunkte, Regeln(RankingRegelArt.FairplayWertung, RankingRegelArt.Losentscheid)));

        Reihenfolge(tabelle).Should().Equal(3, 4, 1, 2);
    }

    [Theory]
    // 1 schlägt 2 mit 5:0, 3 und 4 spielen 0:0, 2 schlägt 4 mit 1:0. Gegner von 2 sind 1 und 4, von 4 sind 3 und 2.
    // Summen je Team 1/2/3/4 – Punkte 3/4/1/4, Tordifferenz -4/4/-1/-4, Tore 1/5/0/1: je Kriterium eine andere Reihenfolge.
    [InlineData(RankingRegelArt.GegnerPunkte, new[] { 2, 4, 1, 3 })]
    [InlineData(RankingRegelArt.GegnerTordifferenz, new[] { 2, 3, 1, 4 })]
    [InlineData(RankingRegelArt.GegnerErzielteTore, new[] { 2, 1, 4, 3 })]
    public void TabelleBerechnen_GegnerKriterien_SummierenUeberAlleBisherigenGegner(RankingRegelArt art, int[] erwartet)
    {
        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(1, 2, 5, 0), Spiel(3, 4, 0, 0), Spiel(2, 4, 1, 0)], new(_dreiPunkte, Regeln(art, RankingRegelArt.Losentscheid)));

        Reihenfolge(tabelle).Should().Equal(erwartet);
    }

    [Fact]
    public void TabelleBerechnen_NurHeimspiele_ZaehltJeTeamNurSeineHeimspiele()
    {
        // Team 1 gewinnt daheim 2:0 und verliert auswärts 1:3.
        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(1, 2, 2, 0), Spiel(2, 1, 3, 1)], new(_dreiPunkte, []), nurHeimspiele: true);

        var team1 = tabelle.Single(e => e.Mannschaft.Id == 1);
        (team1.Spiele, team1.Siege, team1.Niederlagen, team1.Tore, team1.Gegentore, team1.Punkte).Should().Be((1, 1, 0, 2, 0, 3));
        var team2 = tabelle.Single(e => e.Mannschaft.Id == 2);
        (team2.Spiele, team2.Siege, team2.Niederlagen, team2.Tore, team2.Gegentore, team2.Punkte).Should().Be((1, 1, 0, 3, 1, 3));
    }

    [Fact]
    public void TabelleBerechnen_NurAuswaertsspiele_ZaehltJeTeamNurSeineAuswaertsspiele()
    {
        var tabelle = TabellenRechner.TabelleBerechnen(
            [Spiel(1, 2, 2, 0), Spiel(2, 1, 3, 1)], new(_dreiPunkte, []), nurHeimspiele: false);

        var team1 = tabelle.Single(e => e.Mannschaft.Id == 1);
        (team1.Spiele, team1.Siege, team1.Niederlagen, team1.Tore, team1.Gegentore, team1.Punkte).Should().Be((1, 0, 1, 1, 3, 0));
        var team2 = tabelle.Single(e => e.Mannschaft.Id == 2);
        (team2.Spiele, team2.Siege, team2.Niederlagen, team2.Tore, team2.Gegentore, team2.Punkte).Should().Be((1, 0, 1, 0, 2, 0));
    }

    [Fact]
    public void TabelleBerechnen_TeilnehmerOhneSpielGegenBekanntenGegner_StehtMitNullPunktenInDerTabelle()
    {
        // Issue #397: Team 3 ist bisher nur gegen einen offenen Platzhalter angesetzt.
        Spiel[] spiele = [Spiel(1, 2, 1, 0), Angesetzt(_teams[3], null)];

        var tabelle = TabellenRechner.TabelleBerechnen(
            spiele, new(_dreiPunkte, []), teilnehmer: GruppenRechner.AufgeloesteTeilnehmer(spiele));

        tabelle.Should().HaveCount(3);
        var ohneSpiel = tabelle.Single(e => e.Mannschaft.Id == 3);
        ohneSpiel.Spiele.Should().Be(0);
        ohneSpiel.Punkte.Should().Be(0);
    }

    [Fact]
    public void MitErgebnis_LaesstAngesetzteUndErgebnisloseSpieleWeg()
    {
        var gewertet = Spiel(1, 2, 1, 0);
        var ohneErgebnis = Spiel(3, 4);
        ohneErgebnis.Status = SpielStatus.Abgeschlossen;

        TabellenRechner.MitErgebnis([gewertet, Spiel(2, 1), ohneErgebnis]).Should().Equal(gewertet);
    }

    // ── Gruppen einer Gruppenphase ────────────────────────────────────────

    [Fact]
    public void GruppenErmitteln_MitGruppenNummer_OrdnetNachNummer_AuchSpieleMitOffenemPlatz()
    {
        var offenerPlatz = Angesetzt(_teams[3], null, gruppe: 1);
        Spiel[] spiele = [Spiel(5, 6, gruppe: 2), Spiel(1, 2, gruppe: 1), offenerPlatz];

        var gruppen = GruppenRechner.GruppenErmitteln(spiele);

        gruppen.Select(g => g.Nummer).Should().Equal(1, 2);
        gruppen[0].Spiele.Should().HaveCount(2).And.Contain(offenerPlatz);
        gruppen[1].Spiele.Should().ContainSingle();
    }

    [Fact]
    public void GruppenErmitteln_OhneGruppenNummer_LeitetGruppenAusDenPaarungenAb()
    {
        // Vor #397 generierte Spiele: zusammenhängende Paarungen bilden eine Gruppe, die Gruppe mit dem
        // früheren ersten Spiel kommt zuerst.
        Spiel[] spiele = [Spiel(5, 6), Spiel(1, 2), Spiel(6, 7), Spiel(2, 3), Spiel(7, 5), Spiel(3, 1)];

        var gruppen = GruppenRechner.GruppenErmitteln(spiele);

        gruppen.Select(g => g.Nummer).Should().Equal(1, 2);
        gruppen[0].Spiele.Select(s => s.HeimMannschaftId).Should().BeEquivalentTo(new int?[] { 5, 6, 7 });
        gruppen[1].Spiele.Select(s => s.HeimMannschaftId).Should().BeEquivalentTo(new int?[] { 1, 2, 3 });
    }

    [Fact]
    public void OffenePlaetze_NenntJedenPlatzhalterEinmal_InSpielreihenfolge()
    {
        var erstes = Angesetzt(_teams[1], null);
        erstes.AuswaertsPlatzhalterName = "Sieger Playoff B";
        var zweites = Angesetzt(null, _teams[2]);
        zweites.HeimPlatzhalterName = "Sieger Playoff A";
        var drittes = Angesetzt(null, null);
        drittes.HeimPlatzhalterName = "sieger playoff a";
        drittes.AuswaertsPlatzhalterName = "Sieger Playoff B";

        GruppenRechner.OffenePlaetze([drittes, zweites, erstes])
            .Should().Equal("Sieger Playoff B", "Sieger Playoff A");
    }

    [Fact]
    public void OffenePlaetze_OhnePlatzhalter_IstLeer()
    {
        GruppenRechner.OffenePlaetze([Spiel(1, 2), Spiel(2, 1, 1, 0)]).Should().BeEmpty();
    }

    [Fact]
    public void UebergreifendeRangfolge_VergleichtDieKandidatenNurUntereinander()
    {
        // #628: Die Dritten 3 (Gruppe 1) und 7 (Gruppe 2) haben je 3 Punkte, 7 die bessere Tordifferenz (−1 gegen −2).
        // In Gruppe 1 stehen auch 2 und 4 bei 3 Punkten; eine Mini-Tabelle über alle punktgleichen Teams der Runde
        // gäbe 3 seine 3 Punkte aus dem Kreis 2–3–4 und 7 keinen, und 3 stünde vorn.
        Spiel[] spiele =
        [
            Spiel(1, 2, 1, 0, gruppe: 1), Spiel(1, 3, 2, 0, gruppe: 1), Spiel(1, 4, 3, 0, gruppe: 1),
            Spiel(2, 3, 1, 0, gruppe: 1), Spiel(3, 4, 1, 0, gruppe: 1), Spiel(4, 2, 1, 0, gruppe: 1),
            Spiel(5, 6, 1, 0, gruppe: 2), Spiel(5, 7, 1, 0, gruppe: 2), Spiel(5, 8, 1, 0, gruppe: 2),
            Spiel(6, 7, 1, 0, gruppe: 2), Spiel(6, 8, 1, 0, gruppe: 2), Spiel(7, 8, 1, 0, gruppe: 2)
        ];

        var rangfolge = GruppenRechner.UebergreifendeRangfolgeBerechnen(
            spiele, platzierung: 3, bisSpieltag: null, new(_dreiPunkte, UefaRegeln(), DirektenVergleichWiederholen: true));

        Reihenfolge(rangfolge).Should().Equal(7, 3);
        rangfolge.Select(e => e.Rang).Should().Equal(1, 2);
    }

    [Fact]
    public void GruppenTabelleBerechnen_BisSpieltag_BegrenztNurDieGewertetenSpiele()
    {
        var spaeter = Spiel(3, 1, 4, 0, gruppe: 1);
        spaeter.Spieltag = 2;
        var frueher = Spiel(1, 2, 1, 0, gruppe: 1);
        frueher.Spieltag = 1;

        var gruppe = GruppenRechner.GruppenTabelleBerechnen(1, [frueher, spaeter], bisSpieltag: 1, new(_dreiPunkte, []));

        gruppe.Tabelle.Should().HaveCount(3, "Team 3 steht als Teilnehmer der Gruppe auch ohne gewertetes Spiel in der Tabelle");
        Reihenfolge(gruppe.Tabelle)[0].Should().Be(1);
        gruppe.Tabelle.Single(e => e.Mannschaft.Id == 3).Spiele.Should().Be(0);
    }

    // ── Spieltage ─────────────────────────────────────────────────────────

    [Fact]
    public void BisSpieltag_OhneAngabe_LiefertAlleSpiele_SonstNurDieBisDahin()
    {
        var erster = Spiel(1, 2, 1, 0);
        erster.Spieltag = 1;
        var zweiter = Spiel(2, 1, 1, 0);
        zweiter.Spieltag = 2;
        var ohneSpieltag = Spiel(3, 4, 1, 0);
        Spiel[] spiele = [erster, zweiter, ohneSpieltag];

        TabellenRechner.BisSpieltag(spiele, null).Should().Equal(spiele);
        TabellenRechner.BisSpieltag(spiele, 1).Should().Equal(erster);
    }

    [Fact]
    public void SpieltageMitErgebnis_ReichtBisZumLetztenSpieltagMitErgebnis()
    {
        var erster = Spiel(1, 2, 1, 0);
        erster.Spieltag = 1;
        var dritter = Spiel(3, 4, 1, 0);
        dritter.Spieltag = 3;
        var vierterOffen = Spiel(2, 1);
        vierterOffen.Spieltag = 4;

        TabellenRechner.SpieltageMitErgebnis([erster, dritter, vierterOffen]).Should().Equal(1, 2, 3);
        TabellenRechner.SpieltageMitErgebnis([vierterOffen]).Should().BeEmpty();
    }

    [Fact]
    public void SpieltageMitErgebnis_AbgeschlossenOhneErgebnis_ZaehltNicht()
    {
        // #622: dieselbe Bedingung wie die Tabelle selbst (TabellenRechner.HatErgebnis).
        var erster = Spiel(1, 2, 1, 0);
        erster.Spieltag = 1;
        var zweiterOhneErgebnis = Spiel(2, 1);
        zweiterOhneErgebnis.Spieltag = 2;
        zweiterOhneErgebnis.Status = SpielStatus.Abgeschlossen;

        TabellenRechner.SpieltageMitErgebnis([erster, zweiterOhneErgebnis]).Should().Equal(1);
    }
}
