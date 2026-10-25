using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Untertitel unter dem Teamnamen im Spielkopf je nach Kontext der Begegnung (Issues #341, #384).</summary>
public class SpielKopfUntertitelTests
{
    private static readonly Verband _uefa = new() { Id = 1, Kurzname = "UEFA" };
    private static readonly Verband _conmebol = new() { Id = 2, Kurzname = "CONMEBOL" };
    private static readonly Land _deutschland = new() { Id = 10, Name = "Deutschland", VerbandId = 1, Verband = _uefa };
    private static readonly Land _spanien = new() { Id = 11, Name = "Spanien", VerbandId = 1, Verband = _uefa };
    private static readonly Land _brasilien = new() { Id = 12, Name = "Brasilien", VerbandId = 2, Verband = _conmebol };

    private static readonly Mannschaft _bayern = Verein(1, _deutschland);
    private static readonly Mannschaft _schalke = Verein(2, _deutschland);
    private static readonly Mannschaft _ulm = Verein(3, _deutschland);
    private static readonly Mannschaft _realMadrid = Verein(4, _spanien);
    private static readonly Mannschaft _flamengo = Verein(5, _brasilien);

    // Bayern Bundesliga, Schalke 2. Bundesliga; Ulm steht in keiner Pyramide.
    private static readonly UntertitelQuellen _quellen = new(
        m => m.Land?.Verband?.Kurzname,
        Ligazugehoerigkeit.Aus(
        [
            new Rangliste
            {
                Abschnitte = [new RanglistenAbschnitt { Name = "Bundesliga", BisPlatz = 1 }, new RanglistenAbschnitt { Name = "2. Bundesliga", BisPlatz = 2 }],
                Eintraege = [new RanglistenEintrag { MannschaftId = 1, Rang = 1 }, new RanglistenEintrag { MannschaftId = 2, Rang = 2 }],
            },
        ]));

    private static Mannschaft Verein(int id, Land land) =>
        new() { Id = id, Mannschaftsart = Mannschaftsart.Verein, LandId = land.Id, Land = land };

    [Fact]
    public void VereineDesselbenLandes_ZeigenDieLiga()
    {
        SpielKopfUntertitel.Fuer(_bayern, _schalke, _quellen).Should().Be("Bundesliga");
        SpielKopfUntertitel.Fuer(_schalke, _bayern, _quellen).Should().Be("2. Bundesliga");
    }

    [Fact]
    public void VereineDesselbenLandes_OhneLigapyramide_OhneUntertitel() =>
        SpielKopfUntertitel.Fuer(_ulm, _bayern, _quellen).Should().BeNull();

    [Fact]
    public void VereineDesselbenVerbands_ZeigenDasLand() =>
        SpielKopfUntertitel.Fuer(_bayern, _realMadrid, _quellen).Should().Be("Deutschland");

    [Fact]
    public void VereineVerschiedenerVerbaende_ZeigenVerbandUndLand()
    {
        SpielKopfUntertitel.Fuer(_bayern, _flamengo, _quellen).Should().Be("UEFA · Deutschland");
        SpielKopfUntertitel.Fuer(_flamengo, _bayern, _quellen).Should().Be("CONMEBOL · Brasilien");
    }

    [Fact]
    public void VereineVerschiedenerVerbaende_OhneVerbandsnamen_ZeigenDasLand() =>
        SpielKopfUntertitel.Fuer(_bayern, _flamengo, _quellen with { VerbandKurzname = _ => null }).Should().Be("Deutschland");

    [Fact]
    public void Nationalmannschaft_ZeigtDenVerband()
    {
        var deutschland = new Mannschaft { Mannschaftsart = Mannschaftsart.Nationalmannschaft, LandId = 10, Land = _deutschland };
        var brasilien = new Mannschaft { Mannschaftsart = Mannschaftsart.Nationalmannschaft, LandId = 12, Land = _brasilien };

        SpielKopfUntertitel.Fuer(deutschland, brasilien, _quellen).Should().Be("UEFA");
    }

    [Fact]
    public void Verein_OhneGegnerVerein_ZeigtDasLand() =>
        SpielKopfUntertitel.Fuer(_bayern, null, _quellen).Should().Be("Deutschland");

    [Fact]
    public void OhneMannschaft_OhneUntertitel() =>
        SpielKopfUntertitel.Fuer(null, _bayern, _quellen).Should().BeNull();
}
