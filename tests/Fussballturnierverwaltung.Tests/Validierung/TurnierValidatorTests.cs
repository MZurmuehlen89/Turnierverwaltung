using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class TurnierValidatorTests
{
    private readonly TurnierValidator _validator = new();

    private static TurnierRundeEingabe Runde(
        string name, Rundenart art, int eingang, int ausgang,
        bool mitRueckspiel = false, bool auswaerts = false, int? gruppenAnzahl = null) =>
        new(name, art, eingang, ausgang, RegelwerkId: 1,
            MitRueckspiel: mitRueckspiel, AuswaertstorRegelAktiv: auswaerts,
            GruppenAnzahl: gruppenAnzahl);

    [Fact]
    public void GueltigeRundenKette_IstGueltig()
    {
        var runden = new[]
        {
            Runde("Gruppenphase",  Rundenart.Gruppenphase, eingang: 32, ausgang: 16, gruppenAnzahl: 8),
            Runde("Achtelfinale",  Rundenart.KoRunde,      eingang: 16, ausgang: 8),
            Runde("Viertelfinale", Rundenart.KoRunde,      eingang: 8,  ausgang: 4),
            Runde("Halbfinale",    Rundenart.KoRunde,      eingang: 4,  ausgang: 2),
            Runde("Finale",        Rundenart.Finale,        eingang: 2,  ausgang: 1),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void RundenUebergang_FalscheAnzahl_IstUngueltig()
    {
        // Runde 1 liefert 15, Runde 2 erwartet 16
        var runden = new[]
        {
            Runde("Gruppenphase", Rundenart.Gruppenphase, eingang: 32, ausgang: 15, gruppenAnzahl: 8),
            Runde("Achtelfinale", Rundenart.KoRunde,      eingang: 16, ausgang: 8),
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*16*15*");
    }

    [Fact]
    public void AuswaertstorRegel_OhneHinRueckspiel_IstUngueltig()
    {
        var runden = new[]
        {
            // MitRueckspiel=false, aber AuswaertstorRegelAktiv=true
            Runde("Viertelfinale", Rundenart.KoRunde, eingang: 8, ausgang: 4, mitRueckspiel: false, auswaerts: true),
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Auswärtstorregel*keine Hin- und Rückspiele*");
    }

    [Fact]
    public void AuswaertstorRegel_BeiHinRueckspiel_IstGueltig()
    {
        var runden = new[]
        {
            Runde("Achtelfinale H/R", Rundenart.KoRunde, eingang: 16, ausgang: 8, mitRueckspiel: true, auswaerts: true),
            Runde("Finale",           Rundenart.Finale,  eingang: 8,  ausgang: 1),
        };

        _validator.RundenValidieren(runden).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void FinaleOhneEinenSieger_IstUngueltig()
    {
        var runden = new[]
        {
            Runde("Finale", Rundenart.Finale, eingang: 2, ausgang: 2), // 2 Ausgänge statt 1
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Finale*1 Sieger*");
    }

    [Fact]
    public void KeineRunden_IstUngueltig()
    {
        var ergebnis = _validator.RundenValidieren([]);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*mindestens eine Runde*");
    }

    [Fact]
    public void KoRundeMitAusgang_GroesserGleichEingang_IstUngueltig()
    {
        var runden = new[]
        {
            Runde("Halbfinale", Rundenart.KoRunde, eingang: 4, ausgang: 4), // gleich, nicht weniger
        };

        var ergebnis = _validator.RundenValidieren(runden);

        ergebnis.IstUngueltig.Should().BeTrue();
    }
}
