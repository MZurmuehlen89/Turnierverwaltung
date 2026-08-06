using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Validierung;

public class UebergangsregelValidatorTests
{
    private readonly UebergangsregelValidator _validator = new();

    private static readonly IReadOnlyList<Uebergangsregel> _keineRegeln = [];

    private static TurnierRunde Gruppenphase(int? gruppenAnzahl = 2, int eingangsMannschaftsanzahl = 8) => new()
    {
        Id = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase,
        EingangsMannschaftsanzahl = eingangsMannschaftsanzahl, GruppenAnzahl = gruppenAnzahl
    };

    private static TurnierRunde Liga(int eingangsMannschaftsanzahl = 36) => new()
    {
        Id = 1, Name = "Liga-Phase", Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = eingangsMannschaftsanzahl
    };

    private static TurnierRunde KoRunde(int eingangsMannschaftsanzahl = 16) => new()
    {
        Id = 1, Name = "Sechzehntelfinale", Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = eingangsMannschaftsanzahl
    };

    private static TurnierRunde Ziel(int id = 2) => new() { Id = id, Name = $"Zielrunde {id}", Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 2 };

    [Fact]
    public void GueltigeGruppenphaseRegel_IstGueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2);
        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), Ziel(), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeLigaRegel_IstGueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 8);
        _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void GueltigeKoRundeRegel_IstGueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, KoRunde(eingangsMannschaftsanzahl: 16), Ziel(), _keineRegeln).IstGueltig.Should().BeTrue();
    }

    [Fact]
    public void QuellUndZielrundeIdentisch_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 1, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(id: 1), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void PlatzierungVonKleinerEins_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 0, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void PlatzierungBisKleinerVon_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 3, PlatzierungBis: 2);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppenphaseOhneGruppenIndex_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppenphaseGruppenIndexAusserhalbBereich_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 5, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 2), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void GruppenphasePlatzierungGroesserGruppengroesse_IstUngueltig()
    {
        // 8 Teams / 2 Gruppen = 4 pro Gruppe, Platzierung 5 gibt es nicht.
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 5);
        _validator.Validieren(eingabe, Gruppenphase(gruppenAnzahl: 2, eingangsMannschaftsanzahl: 8), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void LigaMitGruppenIndex_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, Liga(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void LigaPlatzierungGroesserTeilnehmerzahl_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 40);
        _validator.Validieren(eingabe, Liga(eingangsMannschaftsanzahl: 36), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRundeOhneSpielnummer_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, KoRunde(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void KoRundePlatzierungGroesserZwei_IstUngueltig()
    {
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 3);
        _validator.Validieren(eingabe, KoRunde(), Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void SchweizerSystemAlsQuelle_IstUngueltig()
    {
        var quelle = new TurnierRunde { Id = 1, Name = "Schweiz", Rundenart = Rundenart.SchweizerSystem, EingangsMannschaftsanzahl = 8 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1);
        _validator.Validieren(eingabe, quelle, Ziel(), _keineRegeln).IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void NeueRegelWuerdeZyklusSchliessen_IstUngueltig()
    {
        // Bestehende Regel A(1) -> B(2). Neue Regel B(2) -> A(1) würde einen Zyklus schließen.
        var bestehende = new Uebergangsregel { Id = 100, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 1 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 2, ZielRundeId: 1, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1);

        var ergebnis = _validator.Validieren(eingabe, KoRunde(), Ziel(id: 1), [bestehende]);

        ergebnis.IstUngueltig.Should().BeTrue();
    }

    [Fact]
    public void BearbeitenDerselbenRegel_LoestKeinenSelbstZyklusAus()
    {
        // Bestehende Regel A(1) -> B(2) wird bearbeitet (gleiche Werte) - darf sich nicht selbst als Zyklus melden.
        var bestehende = new Uebergangsregel { Id = 100, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 1 };
        var eingabe = new UebergangsregelEingabe(QuellRundeId: 1, ZielRundeId: 2, GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 2);

        var ergebnis = _validator.Validieren(eingabe, KoRunde(), Ziel(), [bestehende], bearbeiteteRegelId: 100);

        ergebnis.IstGueltig.Should().BeTrue();
    }
}
