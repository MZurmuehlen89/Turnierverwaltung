using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class VorlagenUebergangsregelValidatorTests
{
    private readonly VorlagenUebergangsregelValidator _validator = new();

    private static VorlagenRundeEingabe Gruppenphase(int? gruppenAnzahl = 2, int eingangsMannschaftsanzahl = 8, int? ausgangsMannschaftsanzahl = null) =>
        new("Gruppenphase", Rundenart.Gruppenphase, eingangsMannschaftsanzahl, ausgangsMannschaftsanzahl ?? eingangsMannschaftsanzahl / 2, GruppenAnzahl: gruppenAnzahl);

    private static VorlagenRundeEingabe Liga(int eingangsMannschaftsanzahl = 8, int? ausgangsMannschaftsanzahl = null) =>
        new("Liga", Rundenart.Liga, eingangsMannschaftsanzahl, ausgangsMannschaftsanzahl ?? eingangsMannschaftsanzahl);

    private static VorlagenRundeEingabe SchweizerSystem(int eingangsMannschaftsanzahl = 8, int? ausgangsMannschaftsanzahl = null) =>
        new("Ligaphase", Rundenart.SchweizerSystem, eingangsMannschaftsanzahl, ausgangsMannschaftsanzahl ?? eingangsMannschaftsanzahl);

    private static VorlagenRundeEingabe KoRunde(int eingangsMannschaftsanzahl = 4) =>
        new("Halbfinale", Rundenart.KoRunde, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl / 2);

    private static VorlagenRundeEingabe Ziel(int eingangsMannschaftsanzahl = 4) =>
        new("Finale", Rundenart.KoRunde, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl / 2);

    [Fact]
    public void GueltigeGruppenphaseRegel_IstGueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1)
        };

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
    public void GueltigeSchweizerSystemRegel_IstGueltig()
    {
        var runden = new[] { SchweizerSystem(eingangsMannschaftsanzahl: 36, ausgangsMannschaftsanzahl: 8), Ziel(eingangsMannschaftsanzahl: 8) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8) };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void SchweizerSystemMitGruppenIndex_IstUngueltig()
    {
        var runden = new[] { SchweizerSystem(), Ziel() };
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
    public void SummeUnterschreitetEingangsanzahlDerZielrunde_IstUngueltig()
    {
        // Vor der #284-Nachbesserung wurde nur "> erwartet" geprüft — eine zu geringe Summe (hier: nur eine
        // von zwei Gruppen liefert eine Übergangsregel) rutschte unbemerkt als "gültig" durch.
        var runden = new[] { Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1) };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*1*2*");
    }

    [Fact]
    public void AusgehendeRegelnDeckenNichtDieGesamteAusgangsanzahlAb_IstUngueltig()
    {
        // Gruppenphase (Ausgang 4: Platz 1+2 aus 2 Gruppen) bekommt nur eine Übergangsregel für Platz 1 je
        // Gruppe — die Plätze 2 "verschwinden" dadurch spurlos, die Quellrunde bleibt inkonsistent.
        var runden = new[] { Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 4), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1)
        };

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*Gruppenphase*");
    }

    [Fact]
    public void TerminaleRundeOhneAusgehendeRegeln_IstGueltig()
    {
        // Eine Runde ohne jede ausgehende Übergangsregel (z.B. das Finale) ist ein legitimes Ende der
        // Vorlage und darf nicht als fehlerhaft "unvollständig ausgehend" gemeldet werden.
        var runden = new[] { Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2), Ziel(eingangsMannschaftsanzahl: 2) };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1),
            new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1)
        };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
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
        var runden = new[]
        {
            Liga(eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2),
            Liga(eingangsMannschaftsanzahl: 8, ausgangsMannschaftsanzahl: 2),
            Ziel(eingangsMannschaftsanzahl: 4)
        };
        var regeln = new[]
        {
            new VorlagenUebergangsRegelEingabe(1, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2),
            new VorlagenUebergangsRegelEingabe(2, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2)
        };

        _validator.Validieren(runden, regeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void ZusammenfuehrungAusLigaUndKoRundeUnterschiedlicherArt_IstGueltig()
    {
        // Nutzer-Fund: Liga liefert Platz 1-8 (8 Mannschaften) UND eine K.o.-Runde liefert 8 Siegerplätze
        // (je 1 Mannschaft) an dieselbe Zielrunde — Summe muss 16 ergeben, unabhängig von der
        // unterschiedlichen Quellrundenart (PlatzierungVon/Bis kodiert bei K.o. "Sieger"/"Verlierer" als
        // Marker 1/1 bzw. 2/2, nicht als echten Platzierungsbereich — Bis-Von+1 liefert trotzdem korrekt 1).
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 8), KoRunde(eingangsMannschaftsanzahl: 16), Ziel(eingangsMannschaftsanzahl: 16) };
        var regeln = new List<VorlagenUebergangsRegelEingabe>
        {
            new(1, 3, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8)
        };
        for (var spiel = 1; spiel <= 8; spiel++)
            regeln.Add(new VorlagenUebergangsRegelEingabe(2, 3, GruppeIndex: spiel, PlatzierungVon: 1, PlatzierungBis: 1));

        var ergebnis = _validator.Validieren(runden, regeln);

        ergebnis.IstGueltig.Should().BeTrue(because: string.Join(" | ", ergebnis.Fehler));
    }

    // ── Gruppenübergreifende Rangfolge-Regel (Issue #217, Vorlagen-Pendant zu #184) ──────────────────

    [Fact]
    public void GueltigeAnzahlBesteRegel_IstGueltig()
    {
        var runden = new[] { Gruppenphase(gruppenAnzahl: 4, eingangsMannschaftsanzahl: 16, ausgangsMannschaftsanzahl: 2), Ziel(eingangsMannschaftsanzahl: 2) };
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
