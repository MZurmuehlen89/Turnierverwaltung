using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class VorlagenUebergangsregelValidatorTests
{
    private readonly VorlagenUebergangsregelValidator _validator = new();

    private static VorlagenRundeEingabe Gruppenphase(int? gruppenAnzahl = 2, int eingangsMannschaftsanzahl = 8) =>
        new("Gruppenphase", Rundenart.Gruppenphase, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl / 2, GruppenAnzahl: gruppenAnzahl);

    private static VorlagenRundeEingabe Liga(int eingangsMannschaftsanzahl = 8) =>
        new("Liga", Rundenart.Liga, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl);

    private static VorlagenRundeEingabe KoRunde(int eingangsMannschaftsanzahl = 4) =>
        new("Halbfinale", Rundenart.KoRunde, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl / 2);

    private static VorlagenRundeEingabe Ziel(int eingangsMannschaftsanzahl = 4) =>
        new("Finale", Rundenart.KoRunde, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl / 2);

    [Fact]
    public void GueltigeGruppenphaseRegel_IstGueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppenphaseOhneGruppenIndex_IstUngueltig()
    {
        var runden = new[] { Gruppenphase(), Ziel() };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void LigaMitGruppenIndex_IstUngueltig()
    {
        var runden = new[] { Liga(), Ziel() };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRundeOhneSpielnummer_IstUngueltig()
    {
        var runden = new[] { KoRunde(), Ziel() };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void QuellUndZielGleich_IstUngueltig()
    {
        var runden = new[] { Liga(), Ziel() };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 1, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void ReihenfolgeAusserhalbDerRundenliste_IstUngueltig()
    {
        var runden = new[] { Liga(), Ziel() };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 5, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void DoppelterGruppenindexFuerDieselbeQuellrunde_IstUngueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), Ziel(eingangsMannschaftsanzahl: 4) };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 2, PlatzierungBis: 2)
        };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void SummeUebersteigtEingangsanzahlDerZielrunde_IstUngueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), Ziel(eingangsMannschaftsanzahl: 3) };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2),
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 2)
        };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*4*3*");
    }

    [Fact]
    public void ZyklischerUebergang_IstUngueltig()
    {
        var runden = new[] { Liga(), Ziel() };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1),
            new VorlagenUebergangsRegelEingabe(2, 1, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1)
        };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void ZusammenfuehrungAusZweiQuellrunden_IstGueltig()
    {
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 8), Liga(eingangsMannschaftsanzahl: 8), Ziel(eingangsMannschaftsanzahl: 4) };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2),
            new VorlagenUebergangsRegelEingabe(2, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2)
        };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    // ── Gruppenübergreifende Rangfolge-Regel (Issue #217, Vorlagen-Pendant zu #184) ──────────────────

    [Fact]
    public void GueltigeAnzahlBesteRegel_IstGueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2) };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteBeiNichtGruppenphase_IstUngueltig()
    {
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 8), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteMitGruppenIndex_IstUngueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteGroesserGruppenAnzahl_IstUngueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), Ziel(eingangsMannschaftsanzahl: 8) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 7) };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void DoppelteAnzahlBestePlatzierungFuerDieselbeQuellrunde_IstUngueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), Ziel(eingangsMannschaftsanzahl: 4) };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2),
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 1)
        };

        _validator.Validieren(runden, regeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void AnzahlBesteKapazitaetsSummeWirdBeruecksichtigt()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 3) };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*3*2*");
    }
}
