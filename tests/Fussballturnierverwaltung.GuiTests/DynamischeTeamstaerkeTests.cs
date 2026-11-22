using FluentAssertions;
using GuiTest.Framework;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Schalter „Dynamische Teamstärke“ im Turnierkopf (#359, K8): ein neues Turnier startet mit „aus“, die Änderung
/// macht das Formular ungespeichert, der Wert bleibt nach dem Speichern und erneuten Öffnen der Seite erhalten.</summary>
public sealed class DynamischeTeamstaerkeTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void Schalter_StartetAus_WirdGespeichertUndNachDemNeuOeffnenAngezeigt()
    {
        string name = EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", TEAMS, REGELWERK);
        var kopf = turniere.Kopf();

        kopf.Schalter("DynamischeTeamstaerke").Should().BeFalse("ein neues Turnier startet ohne dynamische Teamstärke");
        kopf.SpeichernFreigabeAbwarten(false).Should().BeFalse("nichts ist geändert");

        kopf.SchalterSetzen("DynamischeTeamstaerke", true);
        kopf.SpeichernFreigabeAbwarten(true).Should().BeTrue("die Änderung macht das Formular ungespeichert");
        kopf.Speichern();

        NeuOeffnen(name).Schalter("DynamischeTeamstaerke").Should().BeTrue("der Wert bleibt gespeichert");

        kopf.SchalterSetzen("DynamischeTeamstaerke", false);
        kopf.SpeichernFreigabeAbwarten(true).Should().BeTrue();
        kopf.Speichern();

        NeuOeffnen(name).Schalter("DynamischeTeamstaerke").Should().BeFalse();
    }

    private Seiten.StammdatenEditor NeuOeffnen(string name)
    {
        app.Hauptfenster.MannschaftenOeffnen();
        var kopf = app.Hauptfenster.TurniereOeffnen().Kopf();
        kopf.Auswaehlen(name);
        return kopf;
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
