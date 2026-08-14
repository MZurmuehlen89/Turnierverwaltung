using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class VerbandsQuoteValidatorTests
{
    private readonly VerbandsQuoteValidator _validator = new();

    private static VerbandsQuote Bestehende(int id, int verbandId) =>
        new() { Id = id, TurnierInstanzId = 1, VerbandId = verbandId, MaxAnzahl = 5 };

    [Fact]
    public void GueltigeEingabe_IstGueltig()
    {
        var eingabe = new VerbandsQuoteEingabe(TurnierInstanzId: 1, VerbandId: 10, MaxAnzahl: 8, MinAnzahl: 2);

        _validator.Validieren(eingabe, []).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void MaxAnzahlKleinerAls1_WirdAbgelehnt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, 10, MaxAnzahl: 0, MinAnzahl: null);

        var ergebnis = _validator.Validieren(eingabe, []);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 1*");
    }

    [Fact]
    public void MinAnzahlGroesserAlsMaxAnzahl_WirdAbgelehnt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, 10, MaxAnzahl: 3, MinAnzahl: 5);

        var ergebnis = _validator.Validieren(eingabe, []);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*nicht überschreiten*");
    }

    [Fact]
    public void MinAnzahlNegativ_WirdAbgelehnt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, 10, MaxAnzahl: 5, MinAnzahl: -1);

        var ergebnis = _validator.Validieren(eingabe, []);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*negativ*");
    }

    [Fact]
    public void DuplikatVerbandImSelbenTurnier_WirdAbgelehnt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, VerbandId: 10, MaxAnzahl: 5, MinAnzahl: null);
        var bestehende = new[] { Bestehende(1, verbandId: 10) };

        var ergebnis = _validator.Validieren(eingabe, bestehende);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*existiert*bereits*");
    }

    [Fact]
    public void BearbeitenDerEigenenQuote_WirdNichtAlsDuplikatErkannt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, VerbandId: 10, MaxAnzahl: 6, MinAnzahl: null);
        var bestehende = new[] { Bestehende(1, verbandId: 10) };

        _validator.Validieren(eingabe, bestehende, bearbeiteteId: 1).IstGueltig.Should().BeTrue();
    }

    // ── Listen-Überladung (WPF-Turnierkopf-Grid, Issue #161) ──────────────────────────────────

    [Fact]
    public void ZeilenListe_LeereListe_IstGueltig()
    {
        _validator.Validieren(Array.Empty<VerbandsQuoteZeileEingabe>()).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZeilenListe_ZweiUnterschiedlicheVerbaende_SindGueltig()
    {
        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(VerbandId: 10, MaxAnzahl: 8, MinAnzahl: 2),
            new VerbandsQuoteZeileEingabe(VerbandId: 20, MaxAnzahl: 4, MinAnzahl: null)
        };

        _validator.Validieren(zeilen).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZeilenListe_MaxAnzahlKleinerAls1_WirdAbgelehnt()
    {
        var zeilen = new[] { new VerbandsQuoteZeileEingabe(VerbandId: 10, MaxAnzahl: 0, MinAnzahl: null) };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 1*");
    }

    [Fact]
    public void ZeilenListe_MinAnzahlGroesserAlsMaxAnzahl_WirdAbgelehnt()
    {
        var zeilen = new[] { new VerbandsQuoteZeileEingabe(VerbandId: 10, MaxAnzahl: 3, MinAnzahl: 5) };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*nicht überschreiten*");
    }

    [Fact]
    public void ZeilenListe_MinAnzahlNegativ_WirdAbgelehnt()
    {
        var zeilen = new[] { new VerbandsQuoteZeileEingabe(VerbandId: 10, MaxAnzahl: 5, MinAnzahl: -1) };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*negativ*");
    }

    [Fact]
    public void ZeilenListe_DuplikatVerbandInDerselbenListe_WirdAbgelehnt()
    {
        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(VerbandId: 10, MaxAnzahl: 8, MinAnzahl: null),
            new VerbandsQuoteZeileEingabe(VerbandId: 10, MaxAnzahl: 4, MinAnzahl: null)
        };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Mehrere Quoten*");
    }
}
