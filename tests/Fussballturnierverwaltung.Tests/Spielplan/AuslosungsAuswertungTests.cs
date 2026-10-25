using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class AuslosungsAuswertungTests
{
    private static AuslosungsErgebnis Paarung(int nr, int? heim, int? gast) =>
        new() { PaarungsNummer = nr, HeimMannschaftId = heim, AuswaertsMannschaftId = gast };

    private static AuslosungsErgebnis Platzhalter(int nr, string? heim, string? gast, bool freilos = false) =>
        new() { PaarungsNummer = nr, HeimPlatzhalterName = heim, AuswaertsPlatzhalterName = gast, IstFreilos = freilos };

    [Fact]
    public void TopfLigaPaarungen_NachPaarungsnummer_OhneUnvollstaendige()
    {
        AuslosungsErgebnis[] ergebnisse = [Paarung(3, 5, 6), Paarung(1, 1, 2), Paarung(2, 3, null)];

        AuslosungsAuswertung.TopfLigaPaarungen(ergebnisse).Should().Equal((1, 2), (5, 6));
    }

    [Fact]
    public void MannschaftenInPaarungsreihenfolge_HeimVorGast_JedesTeamEinmal()
    {
        // Gruppen-Auslosungen speichern ein Team je Eintrag (nur Heim), Paarungs-Auslosungen beide Seiten
        AuslosungsErgebnis[] ergebnisse = [Paarung(2, 7, 3), Paarung(1, 4, 8), Paarung(3, 8, null), Paarung(4, null, 9)];

        AuslosungsAuswertung.MannschaftenInPaarungsreihenfolge(ergebnisse).Should().Equal(4, 8, 7, 3, 9);
    }

    [Fact]
    public void NurPlatzhalter_LeerOderMitFesterMannschaft_Nicht()
    {
        AuslosungsAuswertung.NurPlatzhalter([]).Should().BeFalse();
        AuslosungsAuswertung.NurPlatzhalter([Platzhalter(1, "Sieger A", "2. B"), Paarung(2, null, 5)]).Should().BeFalse();
        AuslosungsAuswertung.NurPlatzhalter([Platzhalter(1, "Sieger A", "2. B"), Platzhalter(2, null, null)]).Should().BeTrue();
    }

    [Fact]
    public void PlatzhalterPaarungen_NachPaarungsnummer_OhneFreilos_FehlenderNameWirdFragezeichen()
    {
        AuslosungsErgebnis[] ergebnisse =
            [Platzhalter(2, "Sieger B", null), Platzhalter(3, "Sieger C", null, freilos: true), Platzhalter(1, "Sieger A", "2. B")];

        AuslosungsAuswertung.PlatzhalterPaarungen(ergebnisse).Should().Equal(("Sieger A", "2. B"), ("Sieger B", "?"));
    }
}

public class PlatzhalterKoSpieleTests
{
    private static TurnierRunde Runde(bool mitRueckspiel) =>
        new() { Id = 7, TurnierInstanzId = 3, Name = "Halbfinale", MitRueckspiel = mitRueckspiel };

    private static readonly (string, string)[] _paarungen = [("Sieger A", "2. B"), ("Sieger B", "2. A")];

    [Fact]
    public void OhneRueckspiel_EinSpielJePaarung_OhneSpieltag()
    {
        var (hinspiele, rueckspiele) = PlatzhalterKoSpiele.Erzeugen(Runde(mitRueckspiel: false), _paarungen, startNr: 11);

        rueckspiele.Should().BeEmpty();
        hinspiele.Select(s => (s.Spielnummer, s.Spieltag, s.HeimPlatzhalterName, s.AuswaertsPlatzhalterName, s.IstRueckspiel))
            .Should().Equal((11, (int?)null, "Sieger A", "2. B", false), (12, null, "Sieger B", "2. A", false));
        hinspiele.Should().OnlyContain(s => s.TurnierRundeId == 7 && s.TurnierInstanzId == 3 && s.HeimMannschaftId == null);
    }

    [Fact]
    public void MitRueckspiel_ErstHinspieleDannGespiegelteRueckspiele()
    {
        var (hinspiele, rueckspiele) = PlatzhalterKoSpiele.Erzeugen(Runde(mitRueckspiel: true), _paarungen, startNr: 11);

        hinspiele.Select(s => (s.Spielnummer, s.Spieltag)).Should().Equal((11, (int?)1), (12, 1));
        rueckspiele.Select(r => (r.Rueckspiel.Spielnummer, r.Rueckspiel.Spieltag, r.Rueckspiel.HeimPlatzhalterName,
                r.Rueckspiel.AuswaertsPlatzhalterName, r.Rueckspiel.IstRueckspiel))
            .Should().Equal((13, (int?)2, "2. B", "Sieger A", true), (14, 2, "2. A", "Sieger B", true));
        rueckspiele.Select(r => r.Hinspiel).Should().Equal(hinspiele);
    }
}
