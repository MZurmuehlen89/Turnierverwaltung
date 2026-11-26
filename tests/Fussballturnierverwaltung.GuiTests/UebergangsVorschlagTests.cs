using FluentAssertions;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Vorschläge für eingehende Übergangsregeln im Runden-Reiter (#167). Eigene Klasse und damit eigene
/// App-Sitzung, siehe <see cref="HandTurnier"/>.</summary>
public sealed class UebergangsVorschlagTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string ACHTELFINALE = "Achtelfinale";

    /// <summary>24 Teams in 6 Gruppen, 16 kommen weiter: das Achtelfinale bekommt „Platz 1–2 je Gruppe + 4 beste
    /// Dritte“ vorgeschlagen (K2, K6). Davor (erste Runde, vor der Übernahme) gibt es keinen Vorschlag bzw. eine Marke;
    /// „Übernehmen“ legt die Regeln im Editor an, gespeichert werden sie erst mit „Speichern“ (K7), danach steht „Regel“
    /// da und der Vorschlag ist weg.</summary>
    [Fact]
    public void Gruppenphase_UngleichmaessigVerteilt_VorschlagUebernehmenSpeichernNeuLaden()
    {
        string name = EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", 24, REGELWERK);
        var runden = turniere.Runden();
        runden.Anlegen(GRUPPENPHASE, "Gruppenphase", 24, 16, gruppen: 6);
        runden.Anlegen(ACHTELFINALE, "K.o.-Runde", 16, 8);

        runden.Auswaehlen(ACHTELFINALE);
        runden.VorschlagTextAbwarten().Should().Be("Gruppenphase: Platz 1–2 je Gruppe, 4 beste 3. Plätze");
        runden.VorschlagHinweis.Should().NotBeEmpty("24 Teams verteilen sich nicht gleichmäßig auf 6 Gruppen");
        runden.MarkeAbwarten(ACHTELFINALE, "Vorschlag").Should().Be("Vorschlag");
        runden.MarkeAbwarten(GRUPPENPHASE, "").Should().BeEmpty("die erste Runde hat keinen Vorschlag");

        runden.VorschlagUebernehmen();
        runden.VorschlagVerschwindetAbwarten().Should().BeTrue("die Lücke ist geschlossen");
        runden.MarkeAbwarten(ACHTELFINALE, "Regel").Should().Be("Regel");
        runden.SpeichernFreigabeAbwarten(true).Should().BeTrue("die Übernahme ist eine ungespeicherte Änderung");
        runden.KeineFehlerAbwarten().Should().BeEmpty();
        runden.Speichern();

        app.Hauptfenster.MannschaftenOeffnen();
        var wieder = app.Hauptfenster.TurniereOeffnen();
        wieder.Kopf().Auswaehlen(name);
        var gespeichert = wieder.Runden();
        gespeichert.Auswaehlen(ACHTELFINALE);
        gespeichert.MarkeAbwarten(ACHTELFINALE, "Regel").Should().Be("Regel", "die Regeln sind gespeichert");
        gespeichert.VorschlagVerschwindetAbwarten().Should().BeTrue();
    }

    /// <summary>Der Ribbon-Knopf „Vorschläge übernehmen“ legt die Regeln aller Runden auf einmal an und ist danach
    /// deaktiviert, nicht ausgeblendet (K8).</summary>
    [Fact]
    public void Ribbon_VorschlaegeUebernehmen_LegtAlleAnUndSperrtSichDanach()
    {
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(EindeutigerName(), "2027", TEAMS, REGELWERK);
        var runden = turniere.Runden();
        runden.Anlegen(GRUPPENPHASE, "Gruppenphase", TEAMS, 4, gruppen: 2);
        runden.Anlegen(HALBFINALE, "K.o.-Runde", 4, 2);
        runden.Anlegen(FINALE, "Finale", 2, 1);

        runden.VorschlaegeUebernehmenFreigabeAbwarten(true).Should().BeTrue("Halbfinale und Finale haben einen Vorschlag");
        runden.MarkeAbwarten(HALBFINALE, "Vorschlag").Should().Be("Vorschlag");
        runden.MarkeAbwarten(FINALE, "Vorschlag").Should().Be("Vorschlag");

        runden.AlleVorschlaegeUebernehmen();
        runden.VorschlaegeUebernehmenFreigabeAbwarten(false).Should().BeFalse("kein Vorschlag mehr offen");
        runden.MarkeAbwarten(HALBFINALE, "Regel").Should().Be("Regel");
        runden.MarkeAbwarten(FINALE, "Regel").Should().Be("Regel");
        runden.KeineFehlerAbwarten().Should().BeEmpty();
        runden.Speichern();
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
