using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class LandesQuoteValidatorTests
{
    private readonly LandesQuoteValidator _validator = new();

    // Land 10 mit Pyramide (Bundesliga 18 Plätze, 2. Bundesliga 18 Plätze), Land 20 ohne.
    private static readonly Land[] _laender =
    [
        new()
        {
            Id = 10, Name = "Deutschland",
            Rangliste = new Rangliste
            {
                Abschnitte =
                [
                    new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 18 },
                    new RanglistenAbschnitt { Name = "2. Bundesliga", Spielklasse = 2, BisPlatz = 36 },
                ],
            },
        },
        new() { Id = 20, Name = "Malta" },
    ];

    private IReadOnlyList<string> Fehler(params LandesQuoteZeileEingabe[] zeilen) =>
        _validator.Validieren(zeilen, _laender).Fehler;

    [Fact]
    public void LeereListe_IstGueltig() => Fehler().Should().BeEmpty();

    [Fact]
    public void ZweiLaenderOhneSpielklasse_SindGueltig() =>
        Fehler(new(LandId: 10, Anzahl: 8), new(LandId: 20, Anzahl: 4)).Should().BeEmpty();

    [Fact]
    public void AnzahlKleinerAls1_WirdAbgelehnt() =>
        Fehler(new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 0)).Should().ContainMatch("*mindestens 1*");

    [Fact]
    public void ZweiZeilenOhneSpielklasse_WerdenAbgelehnt() =>
        Fehler(new(LandId: 10, Anzahl: 8), new(LandId: 10, Anzahl: 4))
            .Should().ContainSingle().Which.Should().Match("Deutschland: Eine Quote ohne Spielklasse gilt für das ganze Land*");

    [Fact]
    public void ZeileOhneNebenZeileMitSpielklasse_WirdAbgelehnt() =>
        Fehler(new(LandId: 10, Anzahl: 8), new(LandId: 10, Anzahl: 4, Spielklasse: 1))
            .Should().ContainMatch("*ohne Spielklasse*");

    // ── Kontingente (#572) ──────────────────────────────────────────────────────────────

    [Fact]
    public void KontingenteInVerschiedenenBereichen_SindGueltig() =>
        Fehler(
            new(LandId: 10, Anzahl: 4, Spielklasse: 1, AbPlatz: 1),
            new(LandId: 10, Anzahl: 2, Spielklasse: 1, AbPlatz: 5),
            new(LandId: 10, Anzahl: 18, Spielklasse: 2))
            .Should().BeEmpty();

    [Fact]
    public void UeberschneidendeKontingente_WerdenAbgelehnt() =>
        Fehler(
            new(LandId: 10, Anzahl: 4, Spielklasse: 1, AbPlatz: 1),
            new(LandId: 10, Anzahl: 2, Spielklasse: 1, AbPlatz: 4))
            .Should().ContainSingle().Which.Should().Be("Deutschland: Die Quoten in Spielklasse 1 überschneiden sich.");

    [Fact]
    public void AbPlatzFehlt_ZaehltAbPlatz1() =>
        Fehler(
            new(LandId: 10, Anzahl: 4, Spielklasse: 1),
            new(LandId: 10, Anzahl: 2, Spielklasse: 1, AbPlatz: 1))
            .Should().ContainMatch("*überschneiden sich*");

    [Fact]
    public void KontingentUeberDasEndeDerSpielklasse_WirdAbgelehnt() =>
        Fehler(new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 4, Spielklasse: 1, AbPlatz: 16))
            .Should().ContainSingle().Which.Should().Be("Deutschland: 4 Team(s) ab Platz 16 passen nicht in Spielklasse 1.");

    [Fact]
    public void SpielklasseOhneLigapyramide_WirdAbgelehnt() =>
        Fehler(new LandesQuoteZeileEingabe(LandId: 20, Anzahl: 2, Spielklasse: 1))
            .Should().ContainSingle().Which.Should().Be("Malta hat keine Ligapyramide, eine Spielklasse lässt sich nicht wählen.");

    [Fact]
    public void AbPlatzOhneSpielklasse_WirdIgnoriert() =>
        new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 2, AbPlatz: 5).GespeicherterAbPlatz.Should().BeNull();

    // ── Kapazitäts-Summen-Check (Issue #189) ───────────────────────────────────────────────

    [Fact]
    public void SummeInnerhalbKapazitaet_IstGueltig() =>
        _validator.Validieren([new(10, 8), new(20, 4)], _laender, eingangsKapazitaet: 16).IstGueltig.Should().BeTrue();

    [Fact]
    public void SummeUeberKapazitaet_WirdAbgelehnt() =>
        _validator.Validieren([new(10, 8), new(20, 10)], _laender, eingangsKapazitaet: 16)
            .Fehler.Should().ContainMatch("*übersteigt die Eingangskapazität*");

    [Fact]
    public void OhneKapazitaetsangabe_SummeWirdNichtGeprueft() =>
        _validator.Validieren([new(10, 1000)], _laender).IstGueltig.Should().BeTrue();

    // ── Pokalsieger (#583) ──────────────────────────────────────────────────────────────

    [Fact]
    public void Pokalsieger_NebenKontingent_IstGueltig() =>
        Fehler(new(LandId: 10, Anzahl: 1, Spielklasse: 1, AbPlatz: 5), new(LandId: 10, Anzahl: 1, AbPlatz: 6, Pokalsieger: true))
            .Should().BeEmpty();

    [Fact]
    public void Pokalsieger_NachrueckenAbAufKontingentplatz_UeberschneidetSichNicht() =>
        Fehler(new(LandId: 10, Anzahl: 1, Spielklasse: 1, AbPlatz: 5), new(LandId: 10, Anzahl: 1, AbPlatz: 5, Pokalsieger: true))
            .Should().BeEmpty();

    [Fact]
    public void Pokalsieger_MehrAlsEinPlatz_WirdAbgelehnt() =>
        Fehler(new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 2, Pokalsieger: true))
            .Should().Equal("Deutschland: Das Pokalsieger-Kontingent hat genau einen Platz.");

    [Fact]
    public void Pokalsieger_Zweimal_WirdAbgelehnt() =>
        Fehler(new(LandId: 10, Anzahl: 1, Pokalsieger: true), new(LandId: 10, Anzahl: 1, AbPlatz: 3, Pokalsieger: true))
            .Should().Equal("Deutschland: Ein Land hat höchstens ein Pokalsieger-Kontingent.");

    [Fact]
    public void Pokalsieger_NebenGanzemLand_WirdAbgelehnt() =>
        Fehler(new(LandId: 10, Anzahl: 4), new(LandId: 10, Anzahl: 1, Pokalsieger: true))
            .Should().ContainMatch("*ohne Spielklasse*");

    [Fact]
    public void Pokalsieger_NachrueckenAbHinterDerObersten_Spielklasse_WirdAbgelehnt() =>
        Fehler(new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 1, AbPlatz: 19, Pokalsieger: true))
            .Should().Equal("Deutschland: Nachrücken ab Platz 19 liegt nicht in der Bundesliga.");

    [Fact]
    public void Pokalsieger_OhneLigapyramide_WirdAbgelehnt() =>
        Fehler(new LandesQuoteZeileEingabe(LandId: 20, Anzahl: 1, Pokalsieger: true))
            .Should().ContainMatch("Malta hat keine Ligapyramide*");

    [Fact]
    public void Pokalsieger_OhneNachrueckenAb_ZaehltAbPlatz1() =>
        new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 1, Pokalsieger: true).GespeicherterAbPlatz.Should().Be(1);
}
