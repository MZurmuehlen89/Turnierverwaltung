using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
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
    [InlineData(VerfahrenArt.SilverGoal, EntscheidungArt.Verlaengerung)]
    [InlineData(VerfahrenArt.VerlaengerungKlassisch, EntscheidungArt.Verlaengerung)]
    public void Art_RichtetSichNachDemVerlaengerungsverfahrenDesRegelwerks(VerfahrenArt verfahren, EntscheidungArt erwartet) =>
        ErgebnisEntscheidung.Art(KoSpiel(verfahren, VerfahrenArt.Elfmeterschiessen), Tore(1, 1, 2, 1))
            .Should().Be(erwartet);

    [Fact]
    public void Art_MehrereVerlaengerungsverfahren_DasErsteImRegelwerkGilt() =>
        ErgebnisEntscheidung.Art(KoSpiel(VerfahrenArt.GoldenGoal, VerfahrenArt.SilverGoal), Tore(1, 1))
            .Should().Be(EntscheidungArt.GoldenGoal);

    [Fact]
    public void Art_NurElfmeterschiessenImRegelwerk_Elfmeterschiessen() =>
        ErgebnisEntscheidung.Art(KoSpiel(VerfahrenArt.Elfmeterschiessen), Tore(1, 1))
            .Should().Be(EntscheidungArt.Elfmeterschiessen);

    [Fact]
    public void Art_RegelwerkOhneVerfahren_RegulaereSpielzeit() =>
        ErgebnisEntscheidung.Art(KoSpiel(), Tore(1, 1)).Should().Be(EntscheidungArt.RegulaereSpielzeit);

    // ── Silver Goal (#391) ────────────────────────────────────────────────────

    private static readonly VerfahrenArt[] _silverGoal = [VerfahrenArt.SilverGoal, VerfahrenArt.Elfmeterschiessen];

    [Theory]
    [InlineData(true, EntscheidungArt.SilverGoal)]
    [InlineData(false, EntscheidungArt.Verlaengerung)]
    public void Art_SilverGoal_HakenEntscheidetZwischenSilverGoalUndVerlaengerung(bool nachErsterHaelfte, EntscheidungArt erwartet) =>
        ErgebnisEntscheidung.Art(KoSpiel(_silverGoal), Tore(1, 1, 2, 1), nachErsterHaelfte).Should().Be(erwartet);

    [Fact]
    public void Art_HakenOhneSilverGoalImRegelwerk_BleibtVerlaengerung() =>
        ErgebnisEntscheidung.Art(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, 2, 1), silverGoalNachErsterHaelfte: true)
            .Should().Be(EntscheidungArt.Verlaengerung);

    [Fact]
    public void Art_SilverGoalMitHakenAberTorgleicheVerlaengerung_Elfmeterschiessen() =>
        ErgebnisEntscheidung.Art(KoSpiel(_silverGoal), Tore(1, 1, 2, 2), silverGoalNachErsterHaelfte: true)
            .Should().Be(EntscheidungArt.Elfmeterschiessen);

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(2, null, false)]
    [InlineData(null, null, false)]
    public void SilverGoalWaehlbar_NurBeiEntschiedenerVerlaengerung(int? heimV, int? auswaertsV, bool erwartet) =>
        ErgebnisEntscheidung.SilverGoalWaehlbar(KoSpiel(_silverGoal), Tore(1, 1, heimV, auswaertsV)).Should().Be(erwartet);

    [Theory]
    [InlineData(VerfahrenArt.VerlaengerungKlassisch)]
    [InlineData(VerfahrenArt.GoldenGoal)]
    public void SilverGoalWaehlbar_AndereVerlaengerung_Nie(VerfahrenArt verfahren) =>
        ErgebnisEntscheidung.SilverGoalWaehlbar(KoSpiel(verfahren, VerfahrenArt.Elfmeterschiessen), Tore(1, 1, 2, 1))
            .Should().BeFalse();

    // ── Live-Prüfung der Verlängerung (#391) ──────────────────────────────────

    private static readonly VerfahrenArt[] _goldenGoal = [VerfahrenArt.GoldenGoal, VerfahrenArt.Elfmeterschiessen];

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 1)]
    [InlineData(null, null)]
    public void VerlaengerungFehler_GoldenGoalHoechstensEinTor_KeinFehler(int? heimV, int? auswaertsV) =>
        ErgebnisEntscheidung.VerlaengerungFehler(KoSpiel(_goldenGoal), Tore(1, 1, heimV, auswaertsV)).Should().BeNull();

    [Theory]
    [InlineData(3, 1)]
    [InlineData(2, 2)]
    public void VerlaengerungFehler_GoldenGoalMehrAlsEinTor_Fehler(int heimV, int auswaertsV) =>
        ErgebnisEntscheidung.VerlaengerungFehler(KoSpiel(_goldenGoal), Tore(1, 1, heimV, auswaertsV))
            .Should().Contain("Golden Goal");

    [Fact]
    public void VerlaengerungFehler_KlassischeVerlaengerungMitMehrerenToren_KeinFehler() =>
        ErgebnisEntscheidung.VerlaengerungFehler(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, 3, 1)).Should().BeNull();

    [Fact]
    public void VerlaengerungFehler_StandUnterDemNach90Minuten_Fehler() =>
        ErgebnisEntscheidung.VerlaengerungFehler(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, 0, 1))
            .Should().Contain("90 Minuten");

    [Fact]
    public void VerlaengerungFehler_KeineVerlaengerungFaellig_KeinFehler() =>
        ErgebnisEntscheidung.VerlaengerungFehler(KoSpiel(_goldenGoal), Tore(2, 1, 5, 1)).Should().BeNull();

    // ── Losentscheid (#391) ───────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1, EntscheidungArt.Losentscheid)]
    [InlineData(2, 1, null)]
    [InlineData(null, null, null)]
    public void LosArt_NachVerlaengerung_NurWennSieTorgleichEndet(int? heimV, int? auswaertsV, EntscheidungArt? erwartet) =>
        ErgebnisEntscheidung.LosArt(KoSpiel(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Losentscheid),
            Tore(1, 1, heimV, auswaertsV)).Should().Be(erwartet);

    [Theory]
    [InlineData(1, 1, EntscheidungArt.Losentscheid)]
    [InlineData(2, 1, null)]
    public void LosArt_OhneVerlaengerung_DirektNachRemis(int heim, int auswaerts, EntscheidungArt? erwartet) =>
        ErgebnisEntscheidung.LosArt(KoSpiel(VerfahrenArt.Losentscheid), Tore(heim, auswaerts)).Should().Be(erwartet);

    [Fact]
    public void LosArt_Muenzwurf_Muenzwurf() =>
        ErgebnisEntscheidung.LosArt(KoSpiel(VerfahrenArt.Muenzwurf), Tore(0, 0)).Should().Be(EntscheidungArt.Muenzwurf);

    [Fact]
    public void LosArt_WiederholungsspielInDerKette_WirdWieInDerSimulationUebersprungen() =>
        ErgebnisEntscheidung.LosArt(
                KoSpiel(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid),
                Tore(1, 1, 1, 1))
            .Should().Be(EntscheidungArt.Losentscheid);

    [Fact]
    public void LosArt_ElfmeterschiessenImRegelwerk_Nie() =>
        ErgebnisEntscheidung.LosArt(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, 1, 1)).Should().BeNull();

    [Fact]
    public void LosArt_KoHinspiel_Nie() =>
        ErgebnisEntscheidung.LosArt(KoSpiel(VerfahrenArt.Losentscheid) with { IstKoHinspiel = true }, Tore(1, 1))
            .Should().BeNull();

    [Theory]
    [InlineData(VerfahrenArt.Losentscheid, EntscheidungArt.Losentscheid)]
    [InlineData(VerfahrenArt.Muenzwurf, EntscheidungArt.Muenzwurf)]
    public void Art_TorgleicheVerlaengerungMitLosImRegelwerk_Los(VerfahrenArt los, EntscheidungArt erwartet) =>
        ErgebnisEntscheidung.Art(KoSpiel(VerfahrenArt.VerlaengerungKlassisch, los), Tore(1, 1, 1, 1)).Should().Be(erwartet);

    [Fact]
    public void Art_RemisOhneVerlaengerungMitLosImRegelwerk_Los() =>
        ErgebnisEntscheidung.Art(KoSpiel(VerfahrenArt.Losentscheid), Tore(1, 1)).Should().Be(EntscheidungArt.Losentscheid);

    // ── Halbzeitstand beim erneuten Speichern (#391) ──────────────────────────

    [Fact]
    public void HalbzeitstandBehalten_NeunzigMinutenUnveraendert_Bleibt() =>
        ErgebnisEntscheidung.HalbzeitstandBehalten(MitHalbzeit(2, 1, 1, 0), 2, 1).Should().Be(((int?)1, (int?)0));

    [Fact]
    public void HalbzeitstandBehalten_NeunzigMinutenGeaendert_Verworfen() =>
        ErgebnisEntscheidung.HalbzeitstandBehalten(MitHalbzeit(2, 1, 1, 0), 3, 1).Should().Be(((int?)null, (int?)null));

    [Fact]
    public void HalbzeitstandBehalten_OhneGespeichertesErgebnis_Leer() =>
        ErgebnisEntscheidung.HalbzeitstandBehalten(null, 2, 1).Should().Be(((int?)null, (int?)null));

    private static SpielErgebnis MitHalbzeit(int heim, int auswaerts, int halbzeitHeim, int halbzeitAuswaerts) => new()
    {
        HeimToreRegulaer = heim, AuswaertsToreRegulaer = auswaerts,
        HalbzeitHeimTore = halbzeitHeim, HalbzeitAuswaertsTore = halbzeitAuswaerts
    };

    [Fact]
    public void VerlaengerungFehler_TorgleichOhneFolgeverfahrenImRegelwerk_Fehler() =>
        ErgebnisEntscheidung.VerlaengerungFehler(KoSpiel(VerfahrenArt.VerlaengerungKlassisch), Tore(1, 1, 2, 2))
            .Should().Contain("keine Entscheidung");

    [Fact]
    public void VerlaengerungFehler_TorgleichMitFolgeverfahren_KeinFehler() =>
        ErgebnisEntscheidung.VerlaengerungFehler(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, 2, 2)).Should().BeNull();

    [Fact]
    public void ElfmeterAktiv_VerlaengerungUnterDemStandNach90Minuten_Nein() =>
        ErgebnisEntscheidung.ElfmeterAktiv(KoSpiel(_verlaengerungUndElfmeter), Tore(1, 1, 0, 0)).Should().BeFalse();
}
