using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class VorlagenPaarungValidatorTests
{
    private readonly VorlagenPaarungValidator _validator = new();

    private static VorlagenRundeEingabe Liga(int eingangsMannschaftsanzahl = 8) =>
        new("Liga", Rundenart.Liga, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl);

    private static VorlagenRundeEingabe KoRunde(int eingangsMannschaftsanzahl = 4, bool paarungFestVorgegeben = true) =>
        new("Halbfinale", Rundenart.KoRunde, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl / 2,
            PaarungFestVorgegeben: paarungFestVorgegeben);

    private static VorlagenRundeEingabe Gruppenphase(int gruppenAnzahl, int eingangsMannschaftsanzahl, bool paarungFestVorgegeben = true) =>
        new("Gruppenphase", Rundenart.Gruppenphase, eingangsMannschaftsanzahl, eingangsMannschaftsanzahl / 2,
            GruppenAnzahl: gruppenAnzahl, PaarungFestVorgegeben: paarungFestVorgegeben);

    [Fact]
    public void KeinePaarungen_IstGueltig()
    {
        var runden = new[] { Liga() };

        _validator.Validieren(runden, [], []).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeKoPaarung_IstGueltig()
    {
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 4), KoRunde(eingangsMannschaftsanzahl: 4) };
        var uebergangsregeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 4) };
        var paarungen = new[]
        {
            new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 1, SlotNr: 1, "Platz 1 (Liga)"),
            new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 1, SlotNr: 2, "Platz 4 (Liga)")
        };

        _validator.Validieren(runden, uebergangsregeln, paarungen).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeGruppenPaarung_IstGueltig()
    {
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 8), Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8) };
        var uebergangsregeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8) };
        var paarungen = new[]
        {
            new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 1, SlotNr: 1, "Platz 1 (Liga)"),
            new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 2, SlotNr: 1, "Platz 2 (Liga)")
        };

        _validator.Validieren(runden, uebergangsregeln, paarungen).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void PaarungOhnePaarungFestVorgegeben_IstUngueltig()
    {
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 4), KoRunde(eingangsMannschaftsanzahl: 4, paarungFestVorgegeben: false) };
        var uebergangsregeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 4) };
        var paarungen = new[] { new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 1, SlotNr: 1, "Platz 1 (Liga)") };

        _validator.Validieren(runden, uebergangsregeln, paarungen).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void PaarungFuerRundeOhneEingehendeUebergangsregel_IstUngueltig()
    {
        // Die erste Runde einer Vorlage hat nie eingehende Übergangsregeln und durchläuft in jedem realen
        // Turnier eine echte Auslosung — feste Paarungen sind dort ausgeschlossen.
        var runden = new[] { KoRunde(eingangsMannschaftsanzahl: 4) };
        var paarungen = new[] { new VorlagenPaarungEingabe(RundenReihenfolge: 1, PaarungsNummer: 1, SlotNr: 1, "Team A") };

        var ergebnis = _validator.Validieren(runden, [], paarungen);

        ergebnis.IstUngueltig.Should().BeTrue();
        ergebnis.Fehler.Should().ContainMatch("*echte Auslosung*");
    }

    [Fact]
    public void DoppelterSlot_IstUngueltig()
    {
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 4), KoRunde(eingangsMannschaftsanzahl: 4) };
        var uebergangsregeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 4) };
        var paarungen = new[]
        {
            new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 1, SlotNr: 1, "Platz 1 (Liga)"),
            new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 1, SlotNr: 1, "Platz 2 (Liga)")
        };

        _validator.Validieren(runden, uebergangsregeln, paarungen).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void LeerePlatzhalterBezeichnung_IstUngueltig()
    {
        var runden = new[] { Liga(eingangsMannschaftsanzahl: 4), KoRunde(eingangsMannschaftsanzahl: 4) };
        var uebergangsregeln = new[] { new VorlagenUebergangsRegelEingabe(1, 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 4) };
        var paarungen = new[] { new VorlagenPaarungEingabe(RundenReihenfolge: 2, PaarungsNummer: 1, SlotNr: 1, "") };

        _validator.Validieren(runden, uebergangsregeln, paarungen).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void ReihenfolgeAusserhalbDerRundenliste_IstUngueltig()
    {
        var runden = new[] { Liga() };
        var paarungen = new[] { new VorlagenPaarungEingabe(RundenReihenfolge: 5, PaarungsNummer: 1, SlotNr: 1, "Team A") };

        _validator.Validieren(runden, [], paarungen).IstUngueltig.Should().BeTrue();
    }
}
