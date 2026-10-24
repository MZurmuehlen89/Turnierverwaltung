using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Stammdatenpflege am Beispiel Mannschaft (#523): anlegen, bearbeiten, Live-Prüfung des Formulars und
/// Löschen mit Rückfrage. Jeder Test legt seine Mannschaft unter eindeutigem Namen selbst an; der Seed bleibt
/// unverändert.</summary>
public sealed class MannschaftsPflegeTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string NAME_LEER = "Mannschaftsname darf nicht leer sein.";
    private const string KUERZEL_LEER = "Kürzel darf nicht leer sein.";

    private readonly MannschaftenSeite _seite = app.Hauptfenster.MannschaftenOeffnen();

    [Fact]
    public void Anlegen_ErscheintMitSeinenWertenInDerListe()
    {
        string name = EindeutigerName();

        _seite.Anlegen(name, "GTA", 80);

        _seite.Eintrag(name).Should().Be(new MannschaftsEintrag(name, "GTA", 80));
        _seite.FehlerAbwarten().Should().BeEmpty();
    }

    [Fact]
    public void Bearbeiten_UebernimmtDieAenderungen()
    {
        string name = EindeutigerName();
        _seite.Anlegen(name, "GTB", 80);

        _seite.Auswaehlen(name);
        _seite.KurznameEingeben("GTX");
        _seite.OffensivSetzen(55);
        _seite.Speichern();

        _seite.Eintrag(name).Should().Be(new MannschaftsEintrag(name, "GTX", 55));
        _seite.Auswaehlen(name);
        _seite.Kurzname.Should().Be("GTX");
    }

    /// <summary>Die Prüfung läuft bei jeder Eingabe: Ein neues, leeres Formular meldet sofort beide Pflichtfelder, ein
    /// vollständiges gar nichts, und Speichern ist genau dann gesperrt, wenn Meldungen dastehen.</summary>
    [Fact]
    public void LiveValidierung_SperrtSpeichernSolangeFehlerBestehen()
    {
        _seite.Neu();
        _seite.FehlerAbwarten(NAME_LEER, KUERZEL_LEER).Should().BeEquivalentTo(NAME_LEER, KUERZEL_LEER);
        _seite.SpeichernFreigabeAbwarten(false).Should().BeFalse();

        _seite.NameEingeben(EindeutigerName());
        _seite.KurznameEingeben("GTV");
        _seite.FehlerAbwarten().Should().BeEmpty();
        _seite.SpeichernFreigabeAbwarten(true).Should().BeTrue();

        _seite.NameEingeben("");
        _seite.FehlerAbwarten(NAME_LEER).Should().Equal(NAME_LEER);
        _seite.SpeichernFreigabeAbwarten(false).Should().BeFalse();

        _seite.Abbrechen();
    }

    [Fact]
    public void Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string name = EindeutigerName();
        _seite.Anlegen(name, "GTL", 70);

        _seite.Auswaehlen(name);
        _seite.Loeschen(bestaetigen: false);
        _seite.Eintrag(name).Name.Should().Be(name, "Abbrechen lässt die Mannschaft stehen");

        _seite.Auswaehlen(name);
        _seite.Loeschen(bestaetigen: true);
        _seite.NichtMehrGelistetAbwarten(name).Should().BeTrue("die Mannschaft ist gelöscht");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static string EindeutigerName() => $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
}
