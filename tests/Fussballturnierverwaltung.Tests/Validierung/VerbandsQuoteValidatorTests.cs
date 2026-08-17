using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class VerbandsQuoteValidatorTests
{
    private readonly VerbandsQuoteValidator _validator = new();

    private static VerbandsQuote Bestehende(int id, int verbandId) =>
        new() { Id = id, TurnierInstanzId = 1, VerbandId = verbandId, Anzahl = 5 };

    [Fact]
    public void GueltigeEingabe_IstGueltig()
    {
        var eingabe = new VerbandsQuoteEingabe(TurnierInstanzId: 1, VerbandId: 10, Anzahl: 8);

        _validator.Validieren(eingabe, []).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlKleinerAls1_WirdAbgelehnt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, 10, Anzahl: 0);

        var ergebnis = _validator.Validieren(eingabe, []);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 1*");
    }

    [Fact]
    public void DuplikatVerbandImSelbenTurnier_WirdAbgelehnt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, VerbandId: 10, Anzahl: 5);
        var bestehende = new[] { Bestehende(1, verbandId: 10) };

        var ergebnis = _validator.Validieren(eingabe, bestehende);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*existiert*bereits*");
    }

    [Fact]
    public void BearbeitenDerEigenenQuote_WirdNichtAlsDuplikatErkannt()
    {
        var eingabe = new VerbandsQuoteEingabe(1, VerbandId: 10, Anzahl: 6);
        var bestehende = new[] { Bestehende(1, verbandId: 10) };

        _validator.Validieren(eingabe, bestehende, bearbeiteteId: 1).IstGueltig.Should().BeTrue();
    }

    // ── Listen-Überladung (WPF-Turnierkopf-Grid, Issue #161/#154) ─────────────────────────────

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
            new VerbandsQuoteZeileEingabe(VerbandId: 10, Anzahl: 8),
            new VerbandsQuoteZeileEingabe(VerbandId: 20, Anzahl: 4)
        };

        _validator.Validieren(zeilen).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZeilenListe_AnzahlKleinerAls1_WirdAbgelehnt()
    {
        var zeilen = new[] { new VerbandsQuoteZeileEingabe(VerbandId: 10, Anzahl: 0) };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens 1*");
    }

    [Fact]
    public void ZeilenListe_DuplikatVerbandInDerselbenListe_WirdAbgelehnt()
    {
        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(VerbandId: 10, Anzahl: 8),
            new VerbandsQuoteZeileEingabe(VerbandId: 10, Anzahl: 4)
        };

        var ergebnis = _validator.Validieren(zeilen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Mehrere Quoten*");
    }

    // ── Kapazitäts-Summen-Check (Issue #154) ───────────────────────────────────────────────────

    [Fact]
    public void ZeilenListe_SummeInnerhalbKapazitaet_IstGueltig()
    {
        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(VerbandId: 10, Anzahl: 8),
            new VerbandsQuoteZeileEingabe(VerbandId: 20, Anzahl: 4)
        };

        _validator.Validieren(zeilen, eingangsKapazitaet: 16).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZeilenListe_SummeUeberKapazitaet_WirdAbgelehnt()
    {
        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(VerbandId: 10, Anzahl: 8),
            new VerbandsQuoteZeileEingabe(VerbandId: 20, Anzahl: 10)
        };

        var ergebnis = _validator.Validieren(zeilen, eingangsKapazitaet: 16);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*übersteigt die Eingangskapazität*");
    }

    [Fact]
    public void ZeilenListe_OhneKapazitaetsangabe_SummeWirdNichtGeprueft()
    {
        var zeilen = new[] { new VerbandsQuoteZeileEingabe(VerbandId: 10, Anzahl: 1000) };

        _validator.Validieren(zeilen, eingangsKapazitaet: null).IstGueltig.Should().BeTrue();
    }
}
