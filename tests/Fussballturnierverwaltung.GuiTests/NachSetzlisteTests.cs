using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Auslosungsart „Nach Setzliste“ (#563, aus #532 nachgeholt), an der Vorlage „UEFA Nations League Liga A“:
/// Topf 1 ist die Setzliste, seine Reihenfolge (beim automatischen Befüllen der Rang) die Setzreihenfolge. In der
/// Gruppenphase führen die Gesetzten der Reihe nach Gruppe A–D an, im Viertelfinale trifft kein Gesetzter (Topf 1 =
/// die Gruppensieger) auf einen anderen.</summary>
public sealed class NachSetzlisteTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string VORLAGE = "UEFA Nations League Liga A";
    private const string VARIANTE = "16 Teams · ab 2024/25";
    private const string GRUPPENPHASE = "Gruppenphase";
    private const string VIERTELFINALE = "Viertelfinale";
    private const string NACH_SETZLISTE = "Nach Setzliste";

    /// <summary>16 UEFA-Nationalmannschaften in Ranglistenreihenfolge; die ersten vier kommen in Topf 1.</summary>
    private static readonly string[] _nationen =
    [
        "Spanien", "Frankreich", "England", "Portugal", "Belgien", "Niederlande", "Italien", "Deutschland",
        "Kroatien", "Schweiz", "Dänemark", "Österreich", "Ungarn", "Serbien", "Polen", "Schottland",
    ];

    [Fact]
    public async Task Gesetzte_FuehrenDieGruppenInReihenfolgeAn_UndTreffenImViertelfinaleNichtAufeinander()
    {
        string rangliste = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var turnier = TurnierAnlegen();

        var gruppen = GruppenAuslosen(turnier, rangliste);
        turnier.RundeSimulieren(GRUPPENPHASE, 48);
        var tabellen = turnier.Spielplan().GruppenTabellen();

        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(VIERTELFINALE, NACH_SETZLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();
        var viertelfinale = turnier.SpieleDerRunde(VIERTELFINALE);

        gruppen.Should().HaveCount(4);
        gruppen.Values.Select((teams, i) => (teams, gesetzt: _nationen[i])).Should().AllSatisfy(g =>
            g.teams.Should().Contain(g.gesetzt, "Topf 1 belegt die Gruppen A–D in Setzreihenfolge"));
        GesetzteGegenUngesetzte(Spielstand.HinUndRueckspiele(viertelfinale), tabellen);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private TurnierdurchfuehrungSeite TurnierAnlegen()
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        app.Hauptfenster.TurniereOeffnen().AusVorlageErstellen(VORLAGE, VARIANTE, "2027", name);
        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name);
        return turnier;
    }

    /// <summary>Lost die Gruppenphase über die TV-Ziehung aus — sie zeigt die Gruppen in der Reihenfolge A–D.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> GruppenAuslosen(
        TurnierdurchfuehrungSeite turnier, string rangliste)
    {
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(GRUPPENPHASE, NACH_SETZLISTE);
        auslosung.RanglisteWaehlen(rangliste);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.MitTvModusManuellDurchfuehren();
        auslosung.RestSofortAnzeigen();
        return auslosung.ZiehungsErgebnis();
    }

    /// <summary>Jede Paarung: genau ein Gruppensieger (Topf 1 = gesetzt) gegen einen Zweiten.</summary>
    private static void GesetzteGegenUngesetzte(
        IReadOnlyList<(SpielZeile Hin, SpielZeile Rueck)> paare, IReadOnlyDictionary<string, IReadOnlyList<string>> tabellen)
    {
        var sieger = tabellen.Values.Select(t => t[0]).ToHashSet();
        paare.Should().HaveCount(4, "vier Paarungen mit Hin- und Rückspiel");
        paare.Should().AllSatisfy(p =>
            new[] { p.Hin.Heim, p.Hin.Auswaerts }.Count(sieger.Contains)
                .Should().Be(1, "ein Gesetzter trifft nie auf einen anderen ({0} – {1})", p.Hin.Heim, p.Hin.Auswaerts));
    }
}
