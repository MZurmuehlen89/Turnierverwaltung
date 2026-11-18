using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Reine Faktorberechnung der Spielsituation (#361): Risikostufe, Platzverweise, Ermüdung.</summary>
public class SpielsituationTests
{
    private static readonly Teamzustand _neutral = Teamzustand.Anpfiff();

    /// <summary>Wirkung auf die Torerwartung des Angreifers: Chancenrate × Quote (seit #431 getrennt).</summary>
    private static double Torwirkung(Teamzustand angreifer, Teamzustand verteidiger)
        => Spielsituation.Angriffsfaktor(angreifer, verteidiger, 1) * Spielsituation.Verwertungsfaktor(angreifer, verteidiger).Gesamt;

    // ── Wirkung der Risikostufe (K3, K4) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Risikostufe_GegnerGewinntMitSteigenderStufeStaerkerAlsDasEigeneTeam()
    {
        Risikostufe[] aufsteigend = [Risikostufe.Neutral, Risikostufe.Offensiv, Risikostufe.AllesNachVorn, Risikostufe.Brechstange];

        foreach (var (niedriger, hoeher) in aufsteigend.Zip(aufsteigend.Skip(1)))
        {
            double eigenerZuwachs = Spielsituation.EigenerAngriff(hoeher) / Spielsituation.EigenerAngriff(niedriger);
            double gegnerZuwachs  = Spielsituation.AngriffDesGegners(hoeher) / Spielsituation.AngriffDesGegners(niedriger);
            eigenerZuwachs.Should().BeGreaterThan(1, $"{hoeher} greift stärker an als {niedriger}");
            gegnerZuwachs.Should().BeGreaterThan(eigenerZuwachs, $"{hoeher} öffnet die Abwehr überproportional");
        }
    }

    /// <summary>#674, K2: Wer mauert, greift weniger an und überlässt dem Gegner das Spiel – bis #674 sank auch der
    /// Angriff des Gegners, das fror ein 1:0 ein.</summary>
    [Fact]
    public void Mauern_SenktDenEigenenAngriffUndLaedtDruckEin()
    {
        var mauernd = new Teamzustand(Risikostufe.Mauern, 0);

        Torwirkung(mauernd, _neutral).Should().BeApproximately(0.90, 1e-9);
        Torwirkung(_neutral, mauernd).Should().BeApproximately(1.10, 1e-9);
    }

    [Fact]
    public void Brechstange_GegnerDeutlichStaerkerAlsDasEigeneTeam()
    {
        var brechstange = new Teamzustand(Risikostufe.Brechstange, 0);

        double eigener = Torwirkung(brechstange, _neutral) / Torwirkung(_neutral, _neutral);
        double gegner  = Torwirkung(_neutral, brechstange) / Torwirkung(_neutral, _neutral);

        eigener.Should().BeGreaterThan(1);
        gegner.Should().BeGreaterThan(eigener * 1.5);
    }

    // ── Menge und Güte (#431, K4) ─────────────────────────────────────────

    [Theory]
    [InlineData(Risikostufe.Offensiv)]
    [InlineData(Risikostufe.AllesNachVorn)]
    [InlineData(Risikostufe.Brechstange)]
    public void Rueckstand_HoehereStufe_MehrChancenAberSchlechtereQuote(Risikostufe stufe)
    {
        var zurueckliegend = new Teamzustand(stufe, 0);

        Spielsituation.Angriffsfaktor(zurueckliegend, _neutral, 1).Should().BeGreaterThan(Spielsituation.Angriffsfaktor(_neutral, _neutral, 1));
        Spielsituation.Verwertungsfaktor(zurueckliegend, _neutral).Gesamt.Should().BeLessThan(Spielsituation.Verwertungsfaktor(_neutral, _neutral).Gesamt);
    }

    [Fact]
    public void GegenBrechstange_SteigtDieVerwertung_StaerkerAlsGegenAllesNachVorn()
    {
        var allesNachVorn = new Teamzustand(Risikostufe.AllesNachVorn, 0);
        var brechstange   = new Teamzustand(Risikostufe.Brechstange, 0);

        Spielsituation.Verwertungsfaktor(_neutral, allesNachVorn).Gesamt.Should().BeGreaterThan(Spielsituation.Verwertungsfaktor(_neutral, _neutral).Gesamt);
        Spielsituation.Verwertungsfaktor(_neutral, brechstange).Gesamt.Should().BeGreaterThan(Spielsituation.Verwertungsfaktor(_neutral, allesNachVorn).Gesamt);
    }

    [Fact]
    public void Mauern_WenigeAberGuteChancen()
    {
        var mauernd = new Teamzustand(Risikostufe.Mauern, 0);

        Spielsituation.Angriffsfaktor(mauernd, _neutral, 1).Should().BeLessThan(Spielsituation.Angriffsfaktor(_neutral, _neutral, 1));
        Spielsituation.Verwertungsfaktor(mauernd, _neutral).Gesamt.Should().BeGreaterThan(1);
    }

    [Fact]
    public void MengeMalGuete_ErgibtDieWirkungAufDieTorerwartung()
    {
        foreach (var eigene in Enum.GetValues<Risikostufe>())
        foreach (var gegner in Enum.GetValues<Risikostufe>())
        {
            Torwirkung(new Teamzustand(eigene, 0), new Teamzustand(gegner, 0)).Should().BeApproximately(
                Spielsituation.EigenerAngriff(eigene) * Spielsituation.AngriffDesGegners(gegner), 1e-9, $"{eigene} gegen {gegner}");
        }
    }

    // ── Risikoachse (#667, K1) ────────────────────────────────────────────

    /// <summary>Die Achse (Haltung) behält ihre Werte; auf den Stufen Defensiv und Mauern kommt seit #674 der Rückzug
    /// hinzu.</summary>
    [Theory]
    [InlineData(Risikostufe.Mauern, -2, 0.75, 0.85, 0.90, 1.10)]
    [InlineData(Risikostufe.Defensiv, -1, 0.875, 0.925, 0.95, 1.05)]
    [InlineData(Risikostufe.Neutral, 0, 1.0, 1.0, 1.0, 1.0)]
    [InlineData(Risikostufe.Offensiv, 1, 1.10, 1.30, 1.10, 1.30)]
    [InlineData(Risikostufe.AllesNachVorn, 2, 1.20, 1.60, 1.20, 1.60)]
    [InlineData(Risikostufe.Brechstange, 3, 1.30, 2.00, 1.30, 2.00)]
#pragma warning disable S107 // je Stufe Position, Werte der Achse und Werte der Stufe – eine Zeile InlineData je Stufe
    public void Risikoachse_GanzePositionen_WerteDerAchse_DieStufeMitRueckzug(
        Risikostufe stufe, double position, double eigener, double gegner, double stufeEigener, double stufeGegner)
#pragma warning restore S107
    {
        Spielsituation.Position(stufe).Should().Be(position);
        Spielsituation.EigenerAngriff(position).Should().BeApproximately(eigener, 1e-9);
        Spielsituation.AngriffDesGegners(position).Should().BeApproximately(gegner, 1e-9);
        Spielsituation.EigenerAngriff(stufe).Should().BeApproximately(stufeEigener, 1e-9);
        Spielsituation.AngriffDesGegners(stufe).Should().BeApproximately(stufeGegner, 1e-9);
    }

    /// <summary>#674, K2: Eine defensive Haltung (Grundausrichtung, Spielbedeutung – Stufe Neutral) bleibt kompakt und
    /// senkt die Tore auf beiden Seiten; nur der Rückzug in Führung lädt Druck ein (#667, #668 unverändert).</summary>
    [Fact]
    public void DefensiveHaltung_OhneRueckzug_WenigerToreAufBeidenSeiten()
    {
        var defensiveHaltung = new Teamzustand(Risikostufe.Neutral, 0, Position: -1);

        Torwirkung(defensiveHaltung, _neutral).Should().BeApproximately(0.875, 1e-9);
        Torwirkung(_neutral, defensiveHaltung).Should().BeApproximately(0.925, 1e-9);
    }

    /// <summary>#674, K2: Der Rückzug wirkt als Faktor zusätzlich zur Achse – auch für ein Team, das wegen seines Profils
    /// nicht genau auf der Stufe steht.</summary>
    [Fact]
    public void Rueckzug_WirktZusaetzlichZurPositionAufDerAchse()
    {
        var mauerndOffensivAusgerichtet = new Teamzustand(Risikostufe.Mauern, 0, Position: -1.5);

        Torwirkung(mauerndOffensivAusgerichtet, _neutral).Should().BeApproximately(0.8125 * 0.90 / 0.75, 1e-9);
        Torwirkung(_neutral, mauerndOffensivAusgerichtet).Should().BeApproximately(0.8875 * 1.10 / 0.85, 1e-9);
    }

    [Theory]
    [InlineData(-1.5, 0.8125, 0.8875)]
    [InlineData(-0.5, 0.9375, 0.9625)]
    [InlineData(0.5, 1.05, 1.15)]
    [InlineData(2.5, 1.25, 1.80)]
    [InlineData(0.25, 1.025, 1.075)]
    public void Risikoachse_ZwischenDenStufen_LinearInterpoliert(double position, double eigener, double gegner)
    {
        Spielsituation.EigenerAngriff(position).Should().BeApproximately(eigener, 1e-9);
        Spielsituation.AngriffDesGegners(position).Should().BeApproximately(gegner, 1e-9);
    }

    [Theory]
    [InlineData(-2.5, -2)]
    [InlineData(3.5, 3)]
    public void Risikoachse_JenseitsDerEnden_BleibtAufDerAeusserstenStufe(double position, double rand)
    {
        Spielsituation.EigenerAngriff(position).Should().Be(Spielsituation.EigenerAngriff(rand));
        Spielsituation.AngriffDesGegners(position).Should().Be(Spielsituation.AngriffDesGegners(rand));
    }

    [Fact]
    public void Teamzustand_WirktUeberSeinePositionAufDerAchse()
    {
        var zwischen = new Teamzustand(Risikostufe.Neutral, 0, Position: 0.5);

        Torwirkung(zwischen, _neutral).Should().BeApproximately(Spielsituation.EigenerAngriff(0.5), 1e-9);
        Torwirkung(_neutral, zwischen).Should().BeApproximately(Spielsituation.AngriffDesGegners(0.5), 1e-9);
        new Teamzustand(Risikostufe.Mauern, 0).Position.Should().Be(-2, "ohne Angabe liegt ein Team auf seiner Stufe");
    }

    [Fact]
    public void Teamzustand_Umstellen_SetztStufeUndZielDesProfils()
    {
        var starrOffensiv = new Teamprofil(Grundausrichtung.Offensiv, Flexibilitaet.Starr);

        Teamzustand.Anpfiff(startpunkt: starrOffensiv.Startpunkt).Position.Should().Be(0.5);
        Teamzustand.Anpfiff(1).Umstellen(Risikostufe.Mauern, starrOffensiv)
            .Should().Be(new Teamzustand(Risikostufe.Mauern, 1, Position: -1.5));
    }

    /// <summary>#674, K2: Defensiv ist der halbe Schritt zum Mauern – eigener Angriff und Druck des Gegners liegen
    /// jeweils zwischen Neutral und Mauern.</summary>
    [Fact]
    public void Defensiv_LiegtZwischenNeutralUndMauern()
    {
        var defensiv = new Teamzustand(Risikostufe.Defensiv, 0);
        var mauernd  = new Teamzustand(Risikostufe.Mauern, 0);
        double neutral = Torwirkung(_neutral, _neutral);

        Torwirkung(defensiv, _neutral).Should().BeLessThan(neutral)
            .And.BeGreaterThan(Torwirkung(mauernd, _neutral));
        Torwirkung(_neutral, defensiv).Should().BeGreaterThan(neutral)
            .And.BeLessThan(Torwirkung(_neutral, mauernd));
    }

    // ── Spielstand (#674, K3) ─────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 1.0)]
    [InlineData(1, 1, 0.80)]
    [InlineData(2, 2, 0.80)]
    [InlineData(1, 0, 0.96)]
    [InlineData(0, 2, 0.96)]
    [InlineData(3, 1, 0.96)]
    public void Spielstandfaktor_NullNullUnveraendert_NachAusgleichRuhiger_SonstLeichtUnterEins(int heim, int gast, double faktor)
    {
        Spielsituation.Spielstandfaktor(heim, gast).Should().BeApproximately(faktor, 1e-9);
    }

    [Theory]
    [InlineData(Risikostufe.Defensiv, 1, "Zieht sich etwas zurück und sichert die Führung")]
    [InlineData(Risikostufe.Defensiv, 0, "Zieht sich etwas zurück und sichert das Unentschieden")]
    [InlineData(Risikostufe.Mauern, 1, "Zieht sich zurück und verteidigt die Führung")]
    [InlineData(Risikostufe.Mauern, 0, "Zieht sich zurück und verteidigt das Unentschieden")]
    [InlineData(Risikostufe.Neutral, -3, "Kehrt zur gewohnten Ordnung zurück")]
    [InlineData(Risikostufe.Offensiv, -1, "Stellt auf Offensive um")]
    [InlineData(Risikostufe.AllesNachVorn, -1, "Wirft alles nach vorn")]
    [InlineData(Risikostufe.Brechstange, -1, "Torwart geht mit nach vorn")]
    public void Taktiktext_JeStufe(Risikostufe stufe, int vorsprung, string erwartet)
    {
        Spielsituation.Taktiktext(stufe, vorsprung).Should().Be(erwartet);
    }

    // ── Platzverweise (K1, K2) ────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0, 0.7)]
    [InlineData(0, 1, 1.2)]
    [InlineData(2, 0, 0.49)]
    [InlineData(1, 1, 0.84)]
    public void Platzverweise_UnterzahlSchwaechtAngriffUndStaerktGegner(int eigene, int gegner, double faktor)
    {
        double ohne = Spielsituation.Angriffsfaktor(_neutral, _neutral, 0.5);

        Spielsituation.Angriffsfaktor(Teamzustand.Anpfiff(eigene), Teamzustand.Anpfiff(gegner), 0.5)
            .Should().BeApproximately(ohne * faktor, 1e-9);
    }

    [Fact]
    public void Platzverweise_AusEreignissen_ZaehltNurRoteKartenJeSeite()
    {
        SimuliertesEreignis[] ereignisse =
        [
            new(10, EreignisArt.RoteKarte, true, "Rote Karte"),
            new(20, EreignisArt.GelbeKarte, false, "Gelbe Karte"),
            new(30, EreignisArt.RoteKarte, false, "Rote Karte"),
            new(90, EreignisArt.RoteKarte, true, "Rote Karte", 3)
        ];

        Platzverweise.AusEreignissen(ereignisse).Should().Be(new Platzverweise(2, 1));
    }

    // ── Ermüdung (K6) ─────────────────────────────────────────────────────

    [Fact]
    public void Ermuedung_SteigtMitDenGespieltenMinutenUndSetztSichInDerVerlaengerungFort()
    {
        Spielsituation.Ermuedung(0).Should().Be(0);
        Spielsituation.Ermuedung(90).Should().Be(1);
        Spielsituation.Ermuedung(120).Should().BeGreaterThan(Spielsituation.Ermuedung(90));
    }

    [Theory]
    [InlineData(-1, 1.6)]
    [InlineData(0, 1.0)]
    [InlineData(1, 0.4)]
    [InlineData(-2, 1.0)]
    [InlineData(3, 1.0)]
    public void Rotfaktor_WerKnappHintenLiegtSiehtHaeufigerRot_BeiKlaremStandUnveraendert(int vorsprung, double erwartet)
    {
        Spielsituation.Rotfaktor(vorsprung).Should().BeApproximately(erwartet, 1e-9);
    }

    [Fact]
    public void Rotfaktor_RueckstandUndFuehrungVerschiebenGleichWeit_HaeufigkeitBleibt()
    {
        (Spielsituation.Rotfaktor(-1) + Spielsituation.Rotfaktor(1)).Should().BeApproximately(2.0, 1e-9,
            "in jeder Minute mit Führung liegt das andere Team hinten – die Summe beider Raten bleibt gleich (#682)");
    }

    [Fact]
    public void Ermuedung_VerlaengerungMitMehrKartenUndLeichtWenigerToren()
    {
        double kartenRegulaer = Enumerable.Range(1, 90).Average(m => Spielsituation.Kartenfaktor(Spielsituation.Ermuedung(m)));
        double kartenVerl     = Enumerable.Range(91, 30).Average(m => Spielsituation.Kartenfaktor(Spielsituation.Ermuedung(m)));
        double toreRegulaer   = MittlererZeitfaktorRegulaer();
        double toreVerl       = Enumerable.Range(97, 30).Average(m => Spielsituation.Zeitfaktor(m - 6, false, Spielsituation.Ermuedung(m)));

        kartenRegulaer.Should().BeApproximately(1.0, 0.02, "im Mittel der regulären Spielzeit bleibt die Kartenrate gleich");
        toreRegulaer.Should().BeApproximately(1.0, 0.02, "die Kalibrierung aus #404 bezieht sich auf die reguläre Spielzeit");
        kartenVerl.Should().BeGreaterThan(kartenRegulaer * 1.3);
        toreVerl.Should().BeInRange(toreRegulaer * 0.8, toreRegulaer * 0.95);
    }

    /// <summary>Mittel über 90 Minuten und die mittlere Nachspielzeit (2 und 4 Minuten).</summary>
    private static double MittlererZeitfaktorRegulaer()
    {
        var faktoren = Enumerable.Range(1, 90).Select(m => Spielsituation.Zeitfaktor(m, false, 0))
            .Concat(Enumerable.Repeat(Spielsituation.Zeitfaktor(45, true, 0), 2))
            .Concat(Enumerable.Repeat(Spielsituation.Zeitfaktor(90, true, 1), 4));
        return faktoren.Average();
    }

    [Fact]
    public void Zeitfaktor_ErsteViertelstundeAmRuhigstenSchlussphaseAmStaerksten()
    {
        double ersteViertelstunde = Spielsituation.Zeitfaktor(1, false, 0);
        double schlussphase       = Spielsituation.Zeitfaktor(80, false, 0.9);
        var uebrige = Enumerable.Range(16, 60).Select(m => Spielsituation.Zeitfaktor(m, false, 0)).ToList();

        uebrige.Should().OnlyContain(f => f > ersteViertelstunde && f < schlussphase);
        Spielsituation.Zeitfaktor(90, true, 1).Should().BeGreaterThan(schlussphase, "in der Nachspielzeit fallen real die meisten Tore je Minute");
    }

    [Fact]
    public void Zeitfaktor_VerlaengerungNieUnterSiebzigProzent()
    {
        Spielsituation.Zeitfaktor(120, false, Spielsituation.Ermuedung(200)).Should().Be(0.7);
    }

    // ── Anzeige der Spielminute (K7) ──────────────────────────────────────

    [Theory]
    [InlineData(45, null, "45'")]
    [InlineData(90, 4, "90+4'")]
    [InlineData(120, 1, "120+1'")]
    public void Spielminute_TextMitNachspielzeit(int minute, int? nachspiel, string erwartet)
    {
        Spielminute.Text(minute, nachspiel).Should().Be(erwartet);
    }

    [Fact]
    public void Spielminute_ChronologischNachMinuteUndNachspielminute_GleicheMinuteInEingabereihenfolge()
    {
        (int Minute, int? Nachspiel, string Name)[] ereignisse =
            [(91, null, "a"), (90, 2, "b"), (46, null, "c"), (90, null, "d"), (90, 2, "e"), (45, 1, "f")];

        ereignisse.Chronologisch(e => e.Minute, e => e.Nachspiel).Select(e => e.Name)
            .Should().Equal("f", "c", "d", "b", "e", "a");
    }
}
