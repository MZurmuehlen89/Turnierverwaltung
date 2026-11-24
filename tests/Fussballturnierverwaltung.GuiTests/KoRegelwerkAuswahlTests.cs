using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>#624: Das Rundenformular bietet bei K.-o.-Rundenarten nur Regelwerke mit Entscheidungsverfahren an (K1) und
/// setzt beim Wechsel auf K.-o. ein ungeeignetes Regelwerk zurück (K2). Eigene Klasse und damit eigene App-Sitzung.</summary>
public sealed class KoRegelwerkAuswahlTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string TURNIERSTANDARD = "— Turnierstandard —";

    [Fact]
    public void KoRunde_BietetNurRegelwerkeMitVerfahrenAn_RundenartwechselSetztZurueck()
    {
        // Ein neu angelegtes Regelwerk hat kein Entscheidungsverfahren.
        string ohneVerfahren = EindeutigerName();
        app.Hauptfenster.RegelwerkeOeffnen().Anlegen(ohneVerfahren, new Punkte(3, 1, 0));

        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(EindeutigerName(), "2027", TEAMS, REGELWERK);
        var runden = turniere.Runden();

        // Zustand davor: eine Liga-Runde nimmt jedes Regelwerk, auch eines ohne Verfahren.
        runden.Anlegen("Ligarunde", "Liga", 4, 2);
        runden.RegelwerkAngebot().Should().Contain(ohneVerfahren);
        runden.RegelwerkWaehlen(ohneVerfahren);
        runden.RegelwerkAbwarten(ohneVerfahren).Should().Be(ohneVerfahren);

        // K2: der Wechsel auf K.-o. setzt die Auswahl zurück (der Turnierstandard hat ein Verfahren).
        runden.RundenartWaehlen("K.o.-Runde");
        runden.RegelwerkAbwarten(TURNIERSTANDARD).Should().Be(TURNIERSTANDARD);

        // K1: bei K.-o. steht das Regelwerk ohne Verfahren nicht mehr zur Wahl, der Turnierstandard schon.
        var angebot = runden.RegelwerkAngebot();
        angebot.Should().NotContain(ohneVerfahren).And.Contain(TURNIERSTANDARD).And.Contain(REGELWERK);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
