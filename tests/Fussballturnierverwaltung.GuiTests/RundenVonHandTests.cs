using FluentAssertions;
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

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
