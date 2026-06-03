using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class RegelwerkValidatorTests
{
    private readonly RegelwerkValidator _validator = new();

    private static RegelwerkErstellenEingabe GueltigeEingabe(
        int sieg = 3, int unentschieden = 1, int niederlage = 0,
        VerfahrenArt[]? verfahren = null) =>
        new("FIFA Modern", "Test-Regelwerk", null, null, null,
            sieg, unentschieden, niederlage,
            verfahren ?? [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen]);

    [Fact]
    public void GueltigeEingabe_IstGueltig()
    {
        _validator.Validieren(GueltigeEingabe()).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void SiegPunkteKleinerGleichUnentschieden_IstUngueltig()
    {
        // Sieg = 1 = Unentschieden → kein Anreiz zu gewinnen
        var ergebnis = _validator.Validieren(GueltigeEingabe(sieg: 1, unentschieden: 1));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Sieg*größer*Unentschieden*");
    }

    [Fact]
    public void NiederlagePunkteHoeherAlsUnentschieden_IstUngueltig()
    {
        var ergebnis = _validator.Validieren(GueltigeEingabe(unentschieden: 1, niederlage: 2));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Niederlage*nicht größer*Unentschieden*");
    }

    [Fact]
    public void DoppeltesVerfahren_IstUngueltig()
    {
        // Elfmeter zweimal in der Liste
        var ergebnis = _validator.Validieren(GueltigeEingabe(
            verfahren: [VerfahrenArt.Elfmeterschiessen, VerfahrenArt.Elfmeterschiessen]));

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Duplikat*");
    }

    [Fact]
    public void ZweiPunkteRegel_IstGueltig()
    {
        // Historisch: 2 Punkte für Sieg (vor 1994)
        _validator.Validieren(GueltigeEingabe(sieg: 2, unentschieden: 1, niederlage: 0))
            .IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void LeererName_IstUngueltig()
    {
        var eingabe = new RegelwerkErstellenEingabe("", "Beschreibung", null, null, null, 3, 1, 0, []);
        _validator.Validieren(eingabe).IstUngueltig.Should().BeTrue();
    }
}
