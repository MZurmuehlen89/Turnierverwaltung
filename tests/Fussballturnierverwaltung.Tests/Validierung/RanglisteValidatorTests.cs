using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class RanglisteValidatorTests
{
    private readonly RanglisteValidator _validator = new();

    private static RanglisteErstellenEingabe GueltigeEingabe(
        IReadOnlyList<RanglistenEintragEingabe>? eintraege = null) =>
        new("FIFA-Weltrangliste", new DateOnly(2026, 1, 1), "Manuell",
            eintraege ??
            [
                new RanglistenEintragEingabe(1, 1, 100),
                new RanglistenEintragEingabe(2, 2, 90)
            ]);

    [Fact]
    public void GueltigeEingabe_IstGueltig()
    {
        _validator.Validieren(GueltigeEingabe()).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void LeererName_IstUngueltig()
    {
        var eingabe = new RanglisteErstellenEingabe("", new DateOnly(2026, 1, 1), "Manuell", []);
        _validator.Validieren(eingabe).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void OhneEintraege_IstGueltig()
    {
        // Eine leere Rangliste ist ein gültiger Zwischenstand (z.B. direkt nach dem Anlegen)
        _validator.Validieren(GueltigeEingabe(eintraege: [])).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void DoppelteMannschaft_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(eintraege:
        [
            new RanglistenEintragEingabe(1, 1, 100),
            new RanglistenEintragEingabe(1, 2, 90)
        ]));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mehrfach*");
    }

    [Fact]
    public void DoppelterRang_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(eintraege:
        [
            new RanglistenEintragEingabe(1, 1, 100),
            new RanglistenEintragEingabe(2, 1, 90)
        ]));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Rang*mehrfach*");
    }

    [Fact]
    public void RangKleinerEins_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(eintraege:
        [
            new RanglistenEintragEingabe(1, 0, 100)
        ]));

        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void NegativePunkte_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(eintraege:
        [
            new RanglistenEintragEingabe(1, 1, -5)
        ]));

        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void PunkteOhneWert_IstGueltig()
    {
        // Punkte sind optional (z.B. reine Rang-Rangliste ohne Punktesystem)
        _validator.Validieren(GueltigeEingabe(eintraege:
        [
            new RanglistenEintragEingabe(1, 1, null),
            new RanglistenEintragEingabe(2, 2, null)
        ])).IstGueltig.Should().BeTrue();
    }

    // Profigrenze der Ligapyramide (#393): leer erlaubt, sonst mindestens Spielklasse 1.
    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Profigrenze_LeerOderMindestensEins(int? profigrenze, bool gueltig) =>
        _validator.Validieren(GueltigeEingabe() with { ProfigrenzeSpielklasse = profigrenze }).IstGueltig.Should().Be(gueltig);
}
