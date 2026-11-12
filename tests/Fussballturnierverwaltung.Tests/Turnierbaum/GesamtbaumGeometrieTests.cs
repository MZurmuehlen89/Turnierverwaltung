using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

/// <summary>Streckung und Verbindungslinien der Gesamtübersicht (#506 aus der Ansicht herausgelöst).</summary>
public class GesamtbaumGeometrieTests
{
    // ── Streckung ─────────────────────────────────────────────────────────

    [Fact]
    public void Strecken_LaesstDenKopfStehenUndStrecktDarunter()
    {
        var streckung = new GesamtbaumStreckung(Kopf: 32, Faktor: 2);

        streckung.Strecken(32).Should().Be(32);
        streckung.Strecken(82).Should().Be(132);
    }

    [Fact]
    public void KartenOben_StrecktDieKartenmitteUndBehaeltDieKartenhoehe()
    {
        var streckung = new GesamtbaumStreckung(Kopf: 32, Faktor: 2);

        // Mitte 58 + 26 = 84 → 32 + 52·2 = 136, Oberkante 136 − 26 = 110.
        streckung.KartenOben(kartenY: 58, kartenHoehe: 52).Should().Be(110);
    }

    [Fact]
    public void OhneStreckung_BleibtAllesWieEs()
    {
        var streckung = new GesamtbaumStreckung(Kopf: 32, Faktor: 1);

        streckung.KartenOben(58, 52).Should().Be(58);
        streckung.FlaechenHoehe(baumHoehe: 500, rand: 16).Should().Be(500);
    }

    [Fact]
    public void FlaechenHoehe_StrecktNurDenInhaltZwischenKopfUndRand()
    {
        var streckung = new GesamtbaumStreckung(Kopf: 32, Faktor: 1.5);

        // Inhalt 500 − 32 − 16 = 452 → 678; dazu Kopf und Rand.
        streckung.FlaechenHoehe(baumHoehe: 500, rand: 16).Should().Be(32 + 678 + 16);
    }

    [Theory]
    [InlineData(952, 2.0)]   // Inhalt 452 soll 952 − 48 = 904 füllen
    [InlineData(400, 1.0)]   // weniger Platz als Inhalt → nie gestaucht
    [InlineData(5000, 3.0)]  // viel Platz → höchstens der Höchstwert
    public void FaktorZumEinpassen_FuelltDieVerfuegbareHoeheZwischenEinsUndHoechstwert(double verfuegbar, double erwartet)
    {
        GesamtbaumStreckung.FaktorZumEinpassen(verfuegbar, baumHoehe: 500, kopf: 32, rand: 16, maxFaktor: 3)
            .Should().Be(erwartet);
    }

    [Fact]
    public void FaktorZumEinpassen_OhneInhalt_IstEins()
    {
        GesamtbaumStreckung.FaktorZumEinpassen(1000, baumHoehe: 48, kopf: 32, rand: 16, maxFaktor: 3).Should().Be(1);
    }

    // ── Linien zwischen Karten ───────────────────────────────────────────

    [Fact]
    public void ZwischenKarten_KindLinks_VonRechterKindkanteZurLinkenElternkante()
    {
        var punkte = Verbindungslinien.ZwischenKarten(kindX: 16, kindMitteY: 100, elternX: 250, elternMitteY: 140,
            kartenBreite: 190);

        // x1 = 16 + 190 = 206, x2 = 250, Mitte 228.
        punkte.Should().Equal(new Punkt(206, 100), new Punkt(228, 100), new Punkt(228, 140), new Punkt(250, 140));
    }

    [Fact]
    public void ZwischenKarten_KindRechts_Gespiegelt()
    {
        var punkte = Verbindungslinien.ZwischenKarten(kindX: 500, kindMitteY: 100, elternX: 266, elternMitteY: 140,
            kartenBreite: 190);

        // x1 = 500 (linke Kindkante), x2 = 266 + 190 = 456 (rechte Elternkante), Mitte 478.
        punkte.Should().Equal(new Punkt(500, 100), new Punkt(478, 100), new Punkt(478, 140), new Punkt(456, 140));
    }
}
