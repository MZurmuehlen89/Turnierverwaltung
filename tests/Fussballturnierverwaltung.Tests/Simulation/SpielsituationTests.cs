using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Reine Faktorberechnung der Spielsituation (#361): Risikostufe, Platzverweise, Ermüdung.</summary>
public class SpielsituationTests
{
    private static readonly Teamzustand _neutral = Teamzustand.Anpfiff();

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

    [Fact]
    public void Mauern_SenktDieTorerwartungBeiderSeiten()
    {
        var mauernd = new Teamzustand(Risikostufe.Mauern, 0);

        Spielsituation.Angriffsfaktor(mauernd, _neutral, 0.5).Should().BeLessThan(Spielsituation.Angriffsfaktor(_neutral, _neutral, 0.5));
        Spielsituation.Angriffsfaktor(_neutral, mauernd, 0.5).Should().BeLessThan(Spielsituation.Angriffsfaktor(_neutral, _neutral, 0.5));
    }

    [Fact]
    public void Brechstange_GegnerDeutlichStaerkerAlsDasEigeneTeam()
    {
        var brechstange = new Teamzustand(Risikostufe.Brechstange, 0);

        double eigener = Spielsituation.Angriffsfaktor(brechstange, _neutral, 1) / Spielsituation.Angriffsfaktor(_neutral, _neutral, 1);
        double gegner  = Spielsituation.Angriffsfaktor(_neutral, brechstange, 1) / Spielsituation.Angriffsfaktor(_neutral, _neutral, 1);

        eigener.Should().BeGreaterThan(1);
        gegner.Should().BeGreaterThan(eigener * 1.5);
    }

    [Theory]
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

    [Fact]
    public void Ermuedung_VerlaengerungMitMehrKartenUndLeichtWenigerToren()
    {
        double kartenRegulaer = Enumerable.Range(1, 90).Average(m => Spielsituation.Kartenfaktor(Spielsituation.Ermuedung(m)));
        double kartenVerl     = Enumerable.Range(91, 30).Average(m => Spielsituation.Kartenfaktor(Spielsituation.Ermuedung(m)));
        double toreRegulaer   = Enumerable.Range(1, 90).Average(m => Spielsituation.Torfaktor(Spielsituation.Ermuedung(m)));
        double toreVerl       = Enumerable.Range(91, 30).Average(m => Spielsituation.Torfaktor(Spielsituation.Ermuedung(m)));

        kartenRegulaer.Should().BeApproximately(1.0, 0.02, "im Mittel der regulären Spielzeit bleibt die Kartenrate gleich");
        toreRegulaer.Should().BeApproximately(1.0, 0.02, "die Kalibrierung aus #404 bezieht sich auf die reguläre Spielzeit");
        kartenVerl.Should().BeGreaterThan(kartenRegulaer * 1.3);
        toreVerl.Should().BeInRange(toreRegulaer * 0.8, toreRegulaer * 0.95);
    }

    [Fact]
    public void Torfaktor_NieUnterSiebzigProzent()
    {
        Spielsituation.Torfaktor(Spielsituation.Ermuedung(200)).Should().Be(0.7);
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
