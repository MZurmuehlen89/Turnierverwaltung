using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class LandesQuoteValidatorTests
{
    private readonly LandesQuoteValidator _validator = new();

    private static LandesQuote Bestehende(int id, int landId) =>
        new() { Id = id, TurnierInstanzId = 1, LandId = landId, Anzahl = 5 };

    [Fact]
    public void GueltigeEingabe_IstGueltig()
    {
        var eingabe = new LandesQuoteEingabe(TurnierInstanzId: 1, LandId: 10, Anzahl: 8);

        _validator.Validieren(eingabe, []).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlKleinerAls1_WirdAbgelehnt()
    {
        var eingabe = new LandesQuoteEingabe(1, 10, Anzahl: 0);

        var ergebnis = _validator.Validieren(eingabe, []);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 1*");
    }

    [Fact]
    public void DuplikatLandImSelbenTurnier_WirdAbgelehnt()
    {
        var eingabe = new LandesQuoteEingabe(1, LandId: 10, Anzahl: 5);
        var bestehende = new[] { Bestehende(1, landId: 10) };

        var ergebnis = _validator.Validieren(eingabe, bestehende);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*existiert*bereits*");
    }

    [Fact]
    public void BearbeitenDerEigenenQuote_WirdNichtAlsDuplikatErkannt()
    {
        var eingabe = new LandesQuoteEingabe(1, LandId: 10, Anzahl: 6);
        var bestehende = new[] { Bestehende(1, landId: 10) };

        _validator.Validieren(eingabe, bestehende, bearbeiteteId: 1).IstGueltig.Should().BeTrue();
    }

    // ── Listen-Überladung (WinUi-Turnierkopf-Grid, Issue #189) ─────────────────────────────

    [Fact]
    public void ZeilenListe_LeereListe_IstGueltig()
    {
        _validator.Validieren(Array.Empty<LandesQuoteZeileEingabe>()).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZeilenListe_ZweiUnterschiedlicheLaender_SindGueltig()
    {
        var zeilen = new[]
        {
            new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 8),
            new LandesQuoteZeileEingabe(LandId: 20, Anzahl: 4)
        };

        _validator.Validieren(zeilen).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZeilenListe_AnzahlKleinerAls1_WirdAbgelehnt()
    {
        var zeilen = new[] { new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 0) };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 1*");
    }

    [Fact]
    public void ZeilenListe_DuplikatLandInDerselbenListe_WirdAbgelehnt()
    {
        var zeilen = new[]
        {
            new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 8),
            new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 4)
        };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Mehrere Quoten*");
    }

    // ── Kapazitäts-Summen-Check (Issue #189) ───────────────────────────────────────────────────

    [Fact]
    public void ZeilenListe_SummeInnerhalbKapazitaet_IstGueltig()
    {
        var zeilen = new[]
        {
            new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 8),
            new LandesQuoteZeileEingabe(LandId: 20, Anzahl: 4)
        };

        _validator.Validieren(zeilen, eingangsKapazitaet: 16).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZeilenListe_SummeUeberKapazitaet_WirdAbgelehnt()
    {
        var zeilen = new[]
        {
            new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 8),
            new LandesQuoteZeileEingabe(LandId: 20, Anzahl: 10)
        };

        var ergebnis = _validator.Validieren(zeilen, eingangsKapazitaet: 16);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*übersteigt die Eingangskapazität*");
    }

    [Fact]
    public void ZeilenListe_OhneKapazitaetsangabe_SummeWirdNichtGeprueft()
    {
        var zeilen = new[] { new LandesQuoteZeileEingabe(LandId: 10, Anzahl: 1000) };

        _validator.Validieren(zeilen, eingangsKapazitaet: null).IstGueltig.Should().BeTrue();
    }
}
