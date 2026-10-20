using FluentAssertions;
using Fussballturnierverwaltung.Application.Diagnose;

namespace Fussballturnierverwaltung.Tests.Diagnose;

public class LadeanzeigeStandTests
{
    private static OffenerVorgang V(int ms, string? anzeige = null) => new(TimeSpan.FromMilliseconds(ms), anzeige);

    [Fact]
    public void OhneOffeneVorgaenge_KeineAnzeige() =>
        LadeanzeigeStand.Ermitteln([]).Should().Be(LadeanzeigeStand.Keine);

    [Fact]
    public void KurzerVorgang_KeinBalken() =>
        LadeanzeigeStand.Ermitteln([V(299, "Lädt")]).Should().Be(LadeanzeigeStand.Keine);

    [Fact]
    public void AbBalkenschwelle_BalkenOhneText() =>
        LadeanzeigeStand.Ermitteln([V(300, "Lädt")]).Should().Be(new LadeanzeigeStand(true, null));

    [Fact]
    public void AbTextschwelle_TextDesAeltestenVorgangsMitAnzeige()
    {
        // Äußerer Vorgang ohne Text (z.B. Reiter laden ohne eigene Anzeige), darin zwei mit Text — der erste zählt.
        var stand = LadeanzeigeStand.Ermitteln([V(2500), V(2400, "Runde wird simuliert …"), V(100, "Turnierbaum wird aktualisiert …")]);

        stand.Should().Be(new LadeanzeigeStand(true, "Runde wird simuliert …"));
    }

    [Fact]
    public void AbTextschwelle_OhneAnzeigetext_Standardtext() =>
        LadeanzeigeStand.Ermitteln([V(2000)]).Should().Be(new LadeanzeigeStand(true, LadeanzeigeStand.STANDARD_TEXT));

    [Fact]
    public void Massgeblich_IstDerLaengsteVorgang_NichtDerErste() =>
        LadeanzeigeStand.Ermitteln([V(50), V(400)]).BalkenSichtbar.Should().BeTrue();
}
