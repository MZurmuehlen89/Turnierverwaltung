using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Runden eines Turniers ohne Vorlage pflegen (#531). Eigene Klasse und damit eigene App-Sitzung, siehe
/// <see cref="HandTurnier"/>.</summary>
public sealed class RundenVonHandTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    /// <summary>Runden anlegen, umbenennen und entfernen; die Live-Prüfung sperrt Speichern bei unstimmigen Zahlen.
    /// Nach dem Speichern und erneuten Öffnen stehen genau die gespeicherten Runden da.</summary>
    [Fact]
    public void Runden_AnlegenUmbenennenEntfernenUndSpeichern()
    {
        string name = EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", TEAMS, REGELWERK);

        var runden = turniere.Runden();
        runden.Anlegen(GRUPPENPHASE, "Gruppenphase", TEAMS, 4, gruppen: 2);
        runden.Anlegen("Zwischenrunde", "Liga", 4, 2);
        runden.Anlegen(FINALE, "Finale", 2, 1);

        runden.Auswaehlen(GRUPPENPHASE);
        runden.AusgangSetzen(TEAMS);
        runden.FehlerAbwarten("müssen kleiner als Eingangsteams")
            .Should().Contain(f => f.Contains("müssen kleiner als Eingangsteams", StringComparison.Ordinal));
        runden.SpeichernFreigabeAbwarten(false).Should().BeFalse();
        runden.AusgangSetzen(4);
        runden.KeineFehlerAbwarten().Should().BeEmpty();

        runden.Auswaehlen("Zwischenrunde");
        runden.Ausfuellen(HALBFINALE, "K.o.-Runde", 4, 2);
        runden.Anlegen("Überzählig", "Liga", 2, 1);
        runden.Entfernen("Überzählig");
        runden.KeineFehlerAbwarten().Should().BeEmpty();
        runden.Speichern();

        app.Hauptfenster.MannschaftenOeffnen();
        var wieder = app.Hauptfenster.TurniereOeffnen();
        wieder.Kopf().Auswaehlen(name);
        wieder.Runden().Rundennamen.Should().Equal(GRUPPENPHASE, HALBFINALE, FINALE);
    }

    /// <summary>Fehler einer Runde (#601): das Feld ist markiert, die Rundenliste zählt die Fehler, und ein Klick in der
    /// Meldungsleiste wählt die Runde – beim Feldfehler fokussiert er das Feld, bei der Rundenkette nur die Runde.</summary>
    [Fact]
    public void Rundenfehler_MarkiertFeldUndListe_DieLeisteWaehltDieRunde()
    {
        const string UNGERADE = "Runde 'Halbfinale': K.-o.-Rundenarten benötigen eine gerade Eingangsmannschaftsanzahl (war: 5).";
        const string KETTE = "Runde 2 ('Halbfinale') erwartet 5 Mannschaften, Runde 1 ('Gruppenphase') liefert aber 4.";
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(EindeutigerName(), "2027", TEAMS, REGELWERK);
        var runden = turniere.Runden();
        runden.Anlegen(GRUPPENPHASE, "Gruppenphase", TEAMS, 4, gruppen: 2);
        runden.Anlegen(HALBFINALE, "K.o.-Runde", 4, 2);
        runden.Anlegen(FINALE, "Finale", 2, 1);

        runden.Auswaehlen(HALBFINALE);
        runden.Ausfuellen(HALBFINALE, "K.o.-Runde", 5, 2);
        Feldmarkierung.FehlerAbwarten(runden.Feld("Eingang"), UNGERADE).Should().Be(UNGERADE);
        Feldmarkierung.FehlerAbwarten(runden.Feld("Ausgang"), "").Should().BeEmpty();
        runden.FehlerAnzahlAbwarten(HALBFINALE, "2").Should().Be("2", "ungerader Eingang und die Rundenkette");
        runden.FehlerAnzahlAbwarten(FINALE, "").Should().BeEmpty();

        runden.Auswaehlen(FINALE);
        runden.Meldungen.Anspringen(UNGERADE);
        runden.GezeigteRundeAbwarten(HALBFINALE).Should().Be(HALBFINALE);
        Feldmarkierung.FokusAbwarten(runden.Feld("Eingang")).Should().BeTrue("der Klick springt ins Feld");

        runden.Auswaehlen(FINALE);
        runden.Meldungen.Anspringen(KETTE);
        runden.GezeigteRundeAbwarten(HALBFINALE).Should().Be(HALBFINALE, "die Rundenkette zeigt die Runde");

        runden.Ausfuellen(HALBFINALE, "K.o.-Runde", 4, 2);
        Feldmarkierung.FehlerAbwarten(runden.Feld("Eingang"), "").Should().BeEmpty();
        runden.FehlerAnzahlAbwarten(HALBFINALE, "").Should().BeEmpty();
    }

    /// <summary>Fehler in einer Übergangsregel (#601): die Zeile ist markiert, die Quellrunde zählt ihn; ein Klick in der
    /// Leiste wählt die Quellrunde, öffnet die Regel und springt in die Zeile.</summary>
    [Fact]
    public void Regelfehler_DieLeisteOeffnetDieRegelUndSpringtInDieZeile()
    {
        const string ZU_GROSS = "Platzierung 5 übersteigt die Gruppengröße (4) der Quellrunde 'Gruppenphase'.";
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(EindeutigerName(), "2027", TEAMS, REGELWERK);
        var runden = turniere.Runden();
        runden.Anlegen(GRUPPENPHASE, "Gruppenphase", TEAMS, 4, gruppen: 2);
        runden.Anlegen(HALBFINALE, "K.o.-Runde", 4, 2);

        runden.Auswaehlen(GRUPPENPHASE);
        runden.AusgehendeRegelOeffnen(HALBFINALE);
        runden.RegelPlatzBisSetzen(0, 5);
        Feldmarkierung.FehlerAbwarten(runden.RegelFeld("PlatzBis", 0), ZU_GROSS).Should().Be(ZU_GROSS);
        Feldmarkierung.FehlerAbwarten(runden.RegelFeld("PlatzBis", 1), "").Should().BeEmpty();
        runden.FehlerAnzahlAbwarten(GRUPPENPHASE, "2").Should().Be("2", "die Zeile und die ausgehende Summe");

        runden.RegelSchliessen();
        runden.Auswaehlen(HALBFINALE);
        runden.Meldungen.Anspringen(ZU_GROSS);
        runden.GezeigteRundeAbwarten(GRUPPENPHASE).Should().Be(GRUPPENPHASE);
        runden.RegelformularOffenAbwarten().Should().BeTrue("der Klick öffnet die Regel");
        Feldmarkierung.FokusAbwarten(runden.RegelFeld("PlatzBis", 0)).Should().BeTrue("und springt in die Zeile");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
