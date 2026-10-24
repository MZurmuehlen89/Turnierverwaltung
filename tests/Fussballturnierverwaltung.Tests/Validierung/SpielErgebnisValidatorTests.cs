using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class SpielErgebnisValidatorTests
{
    private readonly SpielErgebnisValidator _validator = new();

    private static SpielErgebnisEingabe GueltigeEingabe(
        int heim = 2, int auswaerts = 1,
        int? halbzeitHeim = null, int? halbzeitAuswaerts = null) =>
        new(1, 2, heim, auswaerts, null, null, null, null,
            EntscheidungArt.RegulaereSpielzeit, 1, halbzeitHeim, halbzeitAuswaerts);

    [Fact]
    public void OhneHalbzeitstand_IstGueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe());
        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void MitPassendemHalbzeitstand_IstGueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(halbzeitHeim: 1, halbzeitAuswaerts: 0));
        ergebnis.IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, 0)]
    public void NurEineSeiteAngegeben_IstUngueltig(int? halbzeitHeim, int? halbzeitAuswaerts)
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(halbzeitHeim: halbzeitHeim, halbzeitAuswaerts: halbzeitAuswaerts));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*für beide Mannschaften*");
    }

    [Fact]
    public void NegativerHalbzeitstand_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(halbzeitHeim: -1, halbzeitAuswaerts: 0));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*nicht negativ*");
    }

    [Fact]
    public void HalbzeitstandUeberschreitetEndstand_IstUngueltig()
    {
        // 2 Tore zur Halbzeit, aber nur 1 Tor im Endstand — physikalisch nicht möglich.
        var ergebnis = _validator.Validieren(GueltigeEingabe(heim: 1, auswaerts: 1, halbzeitHeim: 2, halbzeitAuswaerts: 0));
        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*darf den Stand der regulären Spielzeit nicht überschreiten*");
    }

    [Fact]
    public void HalbzeitstandGleichEndstand_IstGueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(heim: 1, auswaerts: 0, halbzeitHeim: 1, halbzeitAuswaerts: 0));
        ergebnis.IstGueltig.Should().BeTrue();
    }

    // ── Lücken vor der Zerlegung (#499) — exakte Meldungen ─────────────────

    private static SpielErgebnisEingabe NachVerlaengerung(EntscheidungArt art, int? heimVerl, int? auswaertsVerl) =>
        GueltigeEingabe(heim: 1, auswaerts: 1) with
        {
            EntscheidungArt = art, HeimToreVerlaengerung = heimVerl, AuswaertsToreVerlaengerung = auswaertsVerl
        };

    private static SpielErgebnisEingabe NachElfmeterschiessen(int? heimElf, int? auswaertsElf) =>
        GueltigeEingabe(heim: 1, auswaerts: 1) with
        {
            EntscheidungArt = EntscheidungArt.Elfmeterschiessen, HeimElfmetertore = heimElf, AuswaertsElfmetertore = auswaertsElf
        };

    [Fact]
    public void NegativeRegulaereTore_IstUngueltig()
    {
        var eingabe = GueltigeEingabe(heim: -1, auswaerts: 0) with { SiegerMannschaftId = null };

        _validator.Validieren(eingabe).Fehler.Should().Equal("Tore der regulären Spielzeit dürfen nicht negativ sein.");
    }

    [Theory]
    [InlineData(EntscheidungArt.Verlaengerung)]
    [InlineData(EntscheidungArt.GoldenGoal)]
    [InlineData(EntscheidungArt.SilverGoal)]
    public void VerlaengerungOhneTore_IstUngueltig(EntscheidungArt art)
    {
        _validator.Validieren(NachVerlaengerung(art, 1, null)).Fehler.Should().Equal(
            "Bei Verlängerung müssen Verlängerungstore für beide Mannschaften angegeben werden.");
    }

    [Fact]
    public void NegativeVerlaengerungstore_IstUngueltig()
    {
        _validator.Validieren(NachVerlaengerung(EntscheidungArt.Verlaengerung, -1, 0)).Fehler.Should().Equal(
            "Verlängerungstore dürfen nicht negativ sein.");
    }

    [Fact]
    public void VerlaengerungMitToren_IstGueltig()
    {
        _validator.Validieren(NachVerlaengerung(EntscheidungArt.Verlaengerung, 1, 0)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ElfmeterschiessenOhneTore_IstUngueltig()
    {
        _validator.Validieren(NachElfmeterschiessen(4, null)).Fehler.Should().Equal(
            "Bei Elfmeterschießen müssen die Elfmetertore für beide Mannschaften angegeben werden.");
    }

    [Fact]
    public void ElfmeterschiessenNegativUndGleichstand_MeldetBeides()
    {
        _validator.Validieren(NachElfmeterschiessen(-1, -1)).Fehler.Should().Equal(
            "Elfmetertore dürfen nicht negativ sein.",
            "Beim Elfmeterschießen muss es einen klaren Sieger geben – Gleichstand ist nicht möglich.");
    }

    [Fact]
    public void ElfmeterschiessenMitSieger_IstGueltig()
    {
        _validator.Validieren(NachElfmeterschiessen(5, 4)).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void FremderSieger_MeldetAuchTorverhaeltnis()
    {
        var eingabe = GueltigeEingabe() with { SiegerMannschaftId = 99 };

        _validator.Validieren(eingabe).Fehler.Should().Equal(
            "Der Sieger muss eine der beiden beteiligten Mannschaften sein.",
            "Der Sieger entspricht nicht dem Torverhältnis der regulären Spielzeit.");
    }

    [Fact]
    public void KoHinspielMitVerlaengerung_IstUngueltig()
    {
        var eingabe = NachVerlaengerung(EntscheidungArt.Verlaengerung, 1, 0) with { IstKoHinspiel = true };

        _validator.Validieren(eingabe).Fehler.Should().Equal(
            "Im Hinspiel gibt es keine Verlängerung und kein Elfmeterschießen – die Entscheidung fällt erst im Rückspiel.");
    }

    [Fact]
    public void KoHinspielInRegulaererSpielzeit_IstGueltig()
    {
        _validator.Validieren(GueltigeEingabe() with { IstKoHinspiel = true }).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void UnentschiedenMitSieger_IstUngueltig()
    {
        _validator.Validieren(GueltigeEingabe(heim: 1, auswaerts: 1)).Fehler.Should().Equal(
            "Bei Unentschieden in regulärer Spielzeit kann es keinen Sieger geben.");
    }

    [Fact]
    public void UnentschiedenOhneSieger_IstGueltig()
    {
        var eingabe = GueltigeEingabe(heim: 1, auswaerts: 1) with { SiegerMannschaftId = null };

        _validator.Validieren(eingabe).IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData(2, 1, 2)]
    [InlineData(1, 2, 1)]
    public void SiegerPasstNichtZumTorverhaeltnis_IstUngueltig(int heim, int auswaerts, int sieger)
    {
        var eingabe = GueltigeEingabe(heim, auswaerts) with { SiegerMannschaftId = sieger };

        _validator.Validieren(eingabe).Fehler.Should().Equal(
            "Der Sieger entspricht nicht dem Torverhältnis der regulären Spielzeit.");
    }

    [Fact]
    public void AuswaertssiegerPasstZumTorverhaeltnis_IstGueltig()
    {
        _validator.Validieren(GueltigeEingabe(1, 2) with { SiegerMannschaftId = 2 }).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void Gesamtwertung_SiegerDarfVomTorverhaeltnisAbweichen()
    {
        // Issue #346: im K.-o.-Rückspiel entscheidet das Gesamtergebnis beider Spiele.
        var eingabe = GueltigeEingabe(0, 1) with { SiegerMannschaftId = 1, IstGesamtwertung = true };

        _validator.Validieren(eingabe).IstGueltig.Should().BeTrue();
    }
}
