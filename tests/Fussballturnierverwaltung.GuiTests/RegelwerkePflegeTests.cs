using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Stammdatenpflege Regelwerke (#530): anlegen, bearbeiten, Live-Prüfung des Punktesystems (#602) und
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

    /// <summary>Ein Sieg muss mehr Punkte bringen als ein Unentschieden: die Live-Prüfung markiert den Sieg-Regler und
    /// sperrt Speichern; die Leiste springt in den Regler, ein gültiger Wert gibt Speichern wieder frei (#602).</summary>
    [Fact]
    public void SiegNichtMehrWertAlsUnentschieden_SperrtSpeichernUndMarkiertDenSieg()
    {
        const string MELDUNG = "Punkte für Sieg (1) müssen größer als für Unentschieden (1) sein – sonst gibt es keinen Anreiz zu gewinnen.";
        _seite.Editor.Neu();
        _seite.Editor.WertSetzen("Name", EindeutigerName());
        _seite.PunkteSetzen(new Punkte(1, 1, 0));

        Feldmarkierung.FehlerAbwarten(_seite.Editor.Feld("PunkteSieg"), MELDUNG).Should().Be(MELDUNG);
        Feldmarkierung.FehlerAbwarten(_seite.Editor.Feld("PunkteUnentschieden"), "").Should().BeEmpty();
        _seite.Editor.SpeichernFreigabeAbwarten(false).Should().BeFalse("die Prüfung meldet einen Fehler");
        _seite.Editor.Meldungen.Anspringen(MELDUNG);
        Feldmarkierung.FokusAbwarten(_seite.Editor.Feld("PunkteSieg")).Should().BeTrue("der Klick springt in den Regler");

        _seite.PunkteSetzen(new Punkte(3, 1, 0));
        Feldmarkierung.FehlerAbwarten(_seite.Editor.Feld("PunkteSieg"), "").Should().BeEmpty();
        _seite.Editor.SpeichernFreigabeAbwarten(true).Should().BeTrue();

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
