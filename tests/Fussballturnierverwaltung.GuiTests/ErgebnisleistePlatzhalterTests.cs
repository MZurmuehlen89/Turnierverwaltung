using FluentAssertions;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Ergebnis-Leiste bei einem Spiel, dessen Paarung noch nicht feststeht (#391, AK7b): das vorbelegte Halbfinale
/// des <see cref="HandTurnier"/> besteht vor der Gruppenphase aus Platzhaltern. Eigene Klasse und damit eigene
/// App-Sitzung, siehe <see cref="HandTurnier"/>.</summary>
public sealed class ErgebnisleistePlatzhalterTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void SpielMitPlatzhaltern_ZeigtHinweisStattEingabefeldern()
    {
        string name = EindeutigerName();
        Aufbauen(app.Hauptfenster, name);

        var spielplan = app.Hauptfenster.TurnierdurchfuehrungOeffnen().Spielplan();
        spielplan.RundeWaehlen(HALBFINALE);
        spielplan.SpielWaehlen(KreuzPaarungen[0], KreuzPaarungen[1], platzhalter: true);

        spielplan.Leiste().ZeigtEingabe.Should().BeFalse("ein Spiel mit Platzhaltern zeigt keine Eingabefelder (AK7b)");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
