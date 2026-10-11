using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spiele;

public class ErgebnisEntscheidungTests
{
    private static readonly VerfahrenArt[] _verlaengerungUndElfmeter =
        [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen];

    private static ErgebnisRahmen KoSpiel(params VerfahrenArt[] verfahren) =>
        new(IstKoRunde: true, IstKoHinspiel: false, HinspielTore: null, AuswaertstorRegelAktiv: false, verfahren);

    private static ErgebnisRahmen KoRueckspiel(int hinHeim, int hinAuswaerts, bool auswaertstorRegel = false) =>
        new(IstKoRunde: true, IstKoHinspiel: false, (hinHeim, hinAuswaerts), auswaertstorRegel, _verlaengerungUndElfmeter);

    private static TorEingabe Tore(int? heim, int? auswaerts, int? heimV = null, int? auswaertsV = null) =>
        new(heim, auswaerts, heimV, auswaertsV);

    // ── Verfahren nötig ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    public void VerfahrenNoetig_KoEinzelspiel_NurBeiRemis(int heim, int auswaerts, bool erwartet) =>
        ErgebnisEntscheidung.VerfahrenNoetig(KoSpiel(_verlaengerungUndElfmeter), Tore(heim, auswaerts))
            .Should().Be(erwartet);

    [Fact]
    public void VerfahrenNoetig_KeineKoRunde_NieAuchNichtBeiRemis() =>
        ErgebnisEntscheidung.VerfahrenNoetig(
                KoSpiel(_verlaengerungUndElfmeter) with { IstKoRunde = false }, Tore(1, 1))
            .Should().BeFalse();

    [Fact]
    public void VerfahrenNoetig_KoHinspiel_NieAuchNichtBeiRemis() =>
        ErgebnisEntscheidung.VerfahrenNoetig(
                KoSpiel(_verlaengerungUndElfmeter) with { IstKoHinspiel = true }, Tore(1, 1))
            .Should().BeFalse();

    [Theory]
    [InlineData(null, 1)]
    [InlineData(1, null)]
    public void VerfahrenNoetig_RegulaereSpielzeitUnvollstaendig_Nein(int? heim, int? auswaerts) =>
        ErgebnisEntscheidung.VerfahrenNoetig(KoSpiel(_verlaengerungUndElfmeter), Tore(heim, auswaerts))
            .Should().BeFalse();

    [Fact]
    public void VerfahrenNoetig_KoRueckspiel_BeiAusgeglichenemGesamtergebnisTrotzSiegImRueckspiel() =>
        ErgebnisEntscheidung.VerfahrenNoetig(KoRueckspiel(hinHeim: 2, hinAuswaerts: 1), Tore(2, 1))
            .Should().BeTrue();

    [Fact]
    public void VerfahrenNoetig_KoRueckspiel_NichtBeiRemisMitEntschiedenemGesamtergebnis() =>
        ErgebnisEntscheidung.VerfahrenNoetig(KoRueckspiel(hinHeim: 2, hinAuswaerts: 1), Tore(1, 1))
            .Should().BeFalse();

    [Fact]
    public void VerfahrenNoetig_KoRueckspielMitAuswaertstorRegel_AuswaertstoreEntscheidenVorDemVerfahren() =>
        ErgebnisEntscheidung.VerfahrenNoetig(
                KoRueckspiel(hinHeim: 1, hinAuswaerts: 0, auswaertstorRegel: true), Tore(2, 1))
            .Should().BeFalse();

    // ── Sichtbare Abschnitte ──────────────────────────────────────────────────

    [Fact]
    public void VerlaengerungAktiv_RemisMitVerlaengerungImRegelwerk_Ja() =>
        ErgebnisEntscheidung.VerlaengerungAktiv(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1)).Should().BeTrue();

    [Fact]
    public void VerlaengerungAktiv_RegelwerkOhneVerlaengerung_Nein() =>
        ErgebnisEntscheidung.VerlaengerungAktiv(KoSpiel(VerfahrenArt.Elfmeterschiessen), Tore(1, 1)).Should().BeFalse();

    [Fact]
    public void ElfmeterAktiv_OhneVerlaengerungImRegelwerk_DirektNachRemis() =>
        ErgebnisEntscheidung.ElfmeterAktiv(KoSpiel(VerfahrenArt.Elfmeterschiessen), Tore(1, 1)).Should().BeTrue();

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(2, null, false)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    public void ElfmeterAktiv_MitVerlaengerung_ErstWennSieVollstaendigUndTorgleichIst(
        int? heimV, int? auswaertsV, bool erwartet) =>
        ErgebnisEntscheidung.ElfmeterAktiv(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, heimV, auswaertsV))
            .Should().Be(erwartet);

    [Fact]
    public void ElfmeterAktiv_RegelwerkOhneElfmeterschiessen_Nein() =>
        ErgebnisEntscheidung.ElfmeterAktiv(KoSpiel(VerfahrenArt.VerlaengerungKlassisch), Tore(1, 1, 2, 2))
            .Should().BeFalse();

    [Fact]
    public void ElfmeterAktiv_KoRueckspiel_ZaehltNurDieToreInDerVerlaengerung()
    {
        // Reguläre Spielzeit 2:1 gleicht das Hinspiel (2:1) aus; in der Verlängerung fällt je ein Tor.
        var rahmen = KoRueckspiel(hinHeim: 2, hinAuswaerts: 1);

        ErgebnisEntscheidung.ElfmeterAktiv(rahmen, Tore(2, 1, 3, 2)).Should().BeTrue();
        ErgebnisEntscheidung.ElfmeterAktiv(rahmen, Tore(2, 1, 3, 1)).Should().BeFalse();
    }

    // ── Entscheidungsart ──────────────────────────────────────────────────────

    [Fact]
    public void Art_KeinVerfahrenNoetig_RegulaereSpielzeit() =>
        ErgebnisEntscheidung.Art(KoSpiel(_verlaengerungUndElfmeter), Tore(2, 1))
            .Should().Be(EntscheidungArt.RegulaereSpielzeit);

    [Theory]
    [InlineData(null, null)]
    [InlineData(2, null)]
    [InlineData(2, 1)]
    public void Art_VerlaengerungOffenOderEntschieden_Verlaengerung(int? heimV, int? auswaertsV) =>
        ErgebnisEntscheidung.Art(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, heimV, auswaertsV))
            .Should().Be(EntscheidungArt.Verlaengerung);

    [Fact]
    public void Art_VerlaengerungTorgleich_Elfmeterschiessen() =>
        ErgebnisEntscheidung.Art(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, 2, 2))
            .Should().Be(EntscheidungArt.Elfmeterschiessen);

    [Fact]
    public void Art_VerlaengerungTorgleichOhneElfmeterschiessenImRegelwerk_BleibtVerlaengerung() =>
        ErgebnisEntscheidung.Art(KoSpiel(VerfahrenArt.VerlaengerungKlassisch), Tore(1, 1, 2, 2))
            .Should().Be(EntscheidungArt.Verlaengerung);

    [Theory]
    [InlineData(VerfahrenArt.GoldenGoal, EntscheidungArt.GoldenGoal)]
    [InlineData(VerfahrenArt.SilverGoal, EntscheidungArt.SilverGoal)]
    [InlineData(VerfahrenArt.VerlaengerungKlassisch, EntscheidungArt.Verlaengerung)]
    public void Art_RichtetSichNachDemVerlaengerungsverfahrenDesRegelwerks(VerfahrenArt verfahren, EntscheidungArt erwartet) =>
        ErgebnisEntscheidung.Art(KoSpiel(verfahren, VerfahrenArt.Elfmeterschiessen), Tore(1, 1, 2, 1))
            .Should().Be(erwartet);

    [Fact]
    public void Art_MehrereVerlaengerungsverfahren_DasErsteImRegelwerkGilt() =>
        ErgebnisEntscheidung.Art(KoSpiel(VerfahrenArt.SilverGoal, VerfahrenArt.GoldenGoal), Tore(1, 1))
            .Should().Be(EntscheidungArt.SilverGoal);

    [Fact]
    public void Art_NurElfmeterschiessenImRegelwerk_Elfmeterschiessen() =>
        ErgebnisEntscheidung.Art(KoSpiel(VerfahrenArt.Elfmeterschiessen), Tore(1, 1))
            .Should().Be(EntscheidungArt.Elfmeterschiessen);

    [Fact]
    public void Art_RegelwerkOhneVerfahren_RegulaereSpielzeit() =>
        ErgebnisEntscheidung.Art(KoSpiel(), Tore(1, 1)).Should().Be(EntscheidungArt.RegulaereSpielzeit);
}
