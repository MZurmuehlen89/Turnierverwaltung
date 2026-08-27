using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class TurnierVorlageValidatorTests
{
    private readonly TurnierVorlageValidator _validator = new();

    // ── BasisdatenValidieren ──────────────────────────────────────────────────

    [Fact]
    public void GueltigeBasisdaten_IstGueltig()
    {
        _validator.BasisdatenValidieren("WM", 32, new DateOnly(2022, 1, 1), new DateOnly(2022, 1, 1))
            .IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeBasisdatenOhneGueltigkeitszeitraum_IstGueltig()
    {
        _validator.BasisdatenValidieren("WM", 32, null, null).IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LeererName_IstUngueltig(string name)
    {
        _validator.BasisdatenValidieren(name, 32, null, null).IstGueltig.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnzahlMannschaftenUnter2_IstUngueltig(int anzahl)
    {
        _validator.BasisdatenValidieren("WM", anzahl, null, null).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void GueltigVonNachGueltigBis_IstUngueltig()
    {
        _validator.BasisdatenValidieren("WM", 32, new DateOnly(2023, 1, 1), new DateOnly(2022, 1, 1))
            .IstGueltig.Should().BeFalse();
    }

    // ── RundenValidieren ──────────────────────────────────────────────────────

    [Fact]
    public void GueltigeRunden_IstGueltig()
    {
        var runden = new[]
        {
            new VorlagenRundeEingabe("Vorrunde", Rundenart.Gruppenphase, 8, 4, GruppenAnzahl: 2),
            new VorlagenRundeEingabe("Finale", Rundenart.KoRunde, 4, 2)
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RundeOhneName_IstUngueltig(string name)
    {
        var runden = new[] { new VorlagenRundeEingabe(name, Rundenart.Liga, 8, 8) };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void RundeMitEingangsMannschaftsanzahlUnter1_IstUngueltig()
    {
        var runden = new[] { new VorlagenRundeEingabe("Liga", Rundenart.Liga, 0, 0) };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void RundeMitAusgangsMannschaftsanzahlUnter1_IstUngueltig()
    {
        var runden = new[] { new VorlagenRundeEingabe("Liga", Rundenart.Liga, 8, 0) };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void GruppenphaseOhneGruppenAnzahl_IstUngueltig()
    {
        var runden = new[] { new VorlagenRundeEingabe("Vorrunde", Rundenart.Gruppenphase, 8, 4) };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void GruppenphaseMitEinerGruppe_IstUngueltig()
    {
        var runden = new[] { new VorlagenRundeEingabe("Vorrunde", Rundenart.Gruppenphase, 8, 4, GruppenAnzahl: 1) };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeFalse();
    }

    [Fact]
    public void KeineRunden_IstGueltig()
    {
        _validator.RundenValidieren([]).IstGueltig.Should().BeTrue();
    }
}
