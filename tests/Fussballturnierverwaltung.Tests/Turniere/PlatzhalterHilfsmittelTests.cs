using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class PlatzhalterHilfsmittelTests
{
    [Fact]
    public void Gruppenphase_ErzeugtPlatzhalterJePlatzierungUndGruppe()
    {
        var regeln = new[]
        {
            new VorlagenPlatzhalterQuelle(1, Rundenart.Gruppenphase, "Gruppenphase", GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1, AnzahlBeste: null),
            new VorlagenPlatzhalterQuelle(1, Rundenart.Gruppenphase, "Gruppenphase", GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1, AnzahlBeste: null)
        };

        PlatzhalterHilfsmittel.PlatzhalterGenerieren(regeln)
            .Should().Equal("1. Gruppe A", "1. Gruppe B");
    }

    [Fact]
    public void GruppenuebergreifendeRangfolge_ErzeugtAnzahlBestePlatzhalter()
    {
        var regeln = new[]
        {
            new VorlagenPlatzhalterQuelle(1, Rundenart.Gruppenphase, "Gruppenphase", GruppeIndex: null, PlatzierungVon: 3, PlatzierungBis: 3, AnzahlBeste: 2)
        };

        PlatzhalterHilfsmittel.PlatzhalterGenerieren(regeln)
            .Should().Equal("1. bester 3. Platz", "2. bester 3. Platz");
    }

    [Fact]
    public void KoRunde_UnterscheidetSiegerUndVerlierer()
    {
        var regeln = new[]
        {
            new VorlagenPlatzhalterQuelle(1, Rundenart.KoRunde, "Halbfinale", GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1, AnzahlBeste: null),
            new VorlagenPlatzhalterQuelle(1, Rundenart.KoRunde, "Halbfinale", GruppeIndex: 1, PlatzierungVon: 2, PlatzierungBis: 2, AnzahlBeste: null)
        };

        PlatzhalterHilfsmittel.PlatzhalterGenerieren(regeln)
            .Should().Equal("Sieger Halbfinale 1", "Verlierer Halbfinale 1");
    }

    [Fact]
    public void Liga_ErzeugtPlatzhalterJePlatzierung()
    {
        var regeln = new[]
        {
            new VorlagenPlatzhalterQuelle(1, Rundenart.Liga, "Liga", GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 2, AnzahlBeste: null)
        };

        PlatzhalterHilfsmittel.PlatzhalterGenerieren(regeln)
            .Should().Equal("Platz 1 (Liga)", "Platz 2 (Liga)");
    }

    [Fact]
    public void MehrereQuellen_SortiertNachReihenfolgeDannGruppeDannPlatzierung()
    {
        var regeln = new[]
        {
            new VorlagenPlatzhalterQuelle(2, Rundenart.Liga, "Playoff", GruppeIndex: null, PlatzierungVon: 1, PlatzierungBis: 1, AnzahlBeste: null),
            new VorlagenPlatzhalterQuelle(1, Rundenart.Gruppenphase, "Gruppenphase", GruppeIndex: 2, PlatzierungVon: 1, PlatzierungBis: 1, AnzahlBeste: null),
            new VorlagenPlatzhalterQuelle(1, Rundenart.Gruppenphase, "Gruppenphase", GruppeIndex: 1, PlatzierungVon: 1, PlatzierungBis: 1, AnzahlBeste: null)
        };

        PlatzhalterHilfsmittel.PlatzhalterGenerieren(regeln)
            .Should().Equal("1. Gruppe A", "1. Gruppe B", "Platz 1 (Playoff)");
    }
}
