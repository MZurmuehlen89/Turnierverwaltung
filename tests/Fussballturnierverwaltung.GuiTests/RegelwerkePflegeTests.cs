using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Stammdatenpflege Regelwerke (#530): anlegen, bearbeiten, Prüfung des Punktesystems beim Speichern und
/// Löschen mit Rückfrage. Jeder Test legt sein Regelwerk unter eindeutigem Namen selbst an; der Seed bleibt
/// unverändert.</summary>
public sealed class RegelwerkePflegeTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private readonly RegelwerkeSeite _seite = app.Hauptfenster.RegelwerkeOeffnen();

    [Fact]
    public void Anlegen_SpeichertDasPunktesystem()
    {
        string name = EindeutigerName();

        _seite.Anlegen(name, new Punkte(2, 1, 0));

        _seite.Gelistet(name).Should().Be(name);
        _seite.Editor.Auswaehlen(name);
        _seite.Punkte.Should().Be(new Punkte(2, 1, 0));
    }

    [Fact]
    public void Bearbeiten_UebernimmtDieAenderungen()
    {
        string name = EindeutigerName();
        _seite.Anlegen(name, new Punkte(2, 1, 0));

        _seite.Editor.Auswaehlen(name);
        _seite.Editor.WertSetzen("GueltigVon", "1994");
        _seite.PunkteSetzen(new Punkte(3, 1, 0));
        _seite.Editor.Speichern();

        // Nach dem Speichern lädt die Seite neu; das Formular zeigt dann die Werte aus der Datenbank
        _seite.Editor.Auswaehlen(name);
        _seite.Editor.Wert("GueltigVon").Should().Be("1994");
        _seite.Punkte.Should().Be(new Punkte(3, 1, 0));
    }

    /// <summary>Ein Sieg muss mehr Punkte bringen als ein Unentschieden.</summary>
    [Fact]
    public void SiegNichtMehrWertAlsUnentschieden_WirdNichtGespeichert()
    {
        const string MELDUNG = "müssen größer als für Unentschieden";
        _seite.Editor.Neu();
        _seite.Editor.WertSetzen("Name", EindeutigerName());
        _seite.PunkteSetzen(new Punkte(1, 1, 0));

        _seite.Editor.SpeichernAbgelehnt(MELDUNG).Should().Contain(MELDUNG);
        _seite.Editor.SpeichernFreigegeben.Should().BeTrue("die Eingabe ist noch nicht gespeichert");

        _seite.Editor.Abbrechen();
    }

    [Fact]
    public void Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string name = EindeutigerName();
        _seite.Anlegen(name, new Punkte(3, 1, 0));

        _seite.Editor.Auswaehlen(name);
        _seite.Editor.Loeschen(bestaetigen: false);
        _seite.Gelistet(name).Should().Be(name, "Abbrechen lässt das Regelwerk stehen");

        _seite.Editor.Auswaehlen(name);
        _seite.Editor.Loeschen(bestaetigen: true);
        _seite.Editor.NichtMehrGelistetAbwarten(name).Should().BeTrue("das Regelwerk ist gelöscht");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static string EindeutigerName() => $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
}
