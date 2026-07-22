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
}
