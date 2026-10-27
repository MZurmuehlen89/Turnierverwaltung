using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Turnierkopf ohne Vorlage (#531): anlegen und bearbeiten mit Live-Prüfung, Löschen mit Rückfrage. Jeder
/// Test legt sein Turnier unter eindeutigem Namen selbst an; der Seed bleibt unverändert. Warum die Szenarien aus
/// #531 auf vier Klassen verteilt sind, steht bei <see cref="HandTurnier"/>.</summary>
public sealed class TurnierkopfVonHandTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string NAME_LEER = "Turniername darf nicht leer sein.";
    private const string SAISON_LEER = "Saison/Jahr darf nicht leer sein.";
    private const string ANZAHL_KEINE_ZAHL = "Mannschaftsanzahl muss eine Zahl ≥ 2 sein.";

    /// <summary>Ein neues, leeres Formular meldet sofort beide Pflichtfelder; eine ungültige Mannschaftsanzahl kommt
    /// dazu. Speichern ist genau dann gesperrt, wenn Meldungen dastehen. Gespeicherte Änderungen stehen nach dem
    /// erneuten Öffnen der Seite noch da.</summary>
    [Fact]
    public void Turnierkopf_PrueftLiveUndSpeichertAenderungen()
    {
        var turniere = app.Hauptfenster.TurniereOeffnen();
        var kopf = turniere.Kopf();
        kopf.Neu();
        turniere.KopfFehlerAbwarten(NAME_LEER, SAISON_LEER).Should().BeEquivalentTo(NAME_LEER, SAISON_LEER);
        SpeichernFreigabeAbwarten(kopf, false).Should().BeFalse();

        string name = EindeutigerName();
        kopf.WertSetzen("Name", name);
        kopf.WertSetzen("Saison", "2027");
        kopf.WertSetzen("Mannschaftsanzahl", "acht");
        turniere.KopfFehlerAbwarten(ANZAHL_KEINE_ZAHL).Should().Equal(ANZAHL_KEINE_ZAHL);
        SpeichernFreigabeAbwarten(kopf, false).Should().BeFalse();

        kopf.WertSetzen("Mannschaftsanzahl", "8");
        turniere.KopfFehlerAbwarten().Should().BeEmpty();
        kopf.Speichern();

        kopf.WertSetzen("Variante", "von Hand");
        kopf.Speichern();

        app.Hauptfenster.MannschaftenOeffnen();
        kopf = app.Hauptfenster.TurniereOeffnen().Kopf();
        kopf.Auswaehlen(name);
        (kopf.Wert("Saison"), kopf.Wert("Mannschaftsanzahl"), kopf.Wert("Variante"))
            .Should().Be(("2027", "8", "von Hand"));
    }

    [Fact]
    public void Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string name = EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", TEAMS, REGELWERK);
        var kopf = turniere.Kopf();

        kopf.Loeschen(bestaetigen: false);
        kopf.Eintrag(name, e => e).Should().NotBeNull("Abbrechen lässt das Turnier stehen");

        kopf.Auswaehlen(name);
        kopf.Loeschen(bestaetigen: true);
        kopf.NichtMehrGelistetAbwarten(name).Should().BeTrue("das Turnier ist gelöscht");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static bool SpeichernFreigabeAbwarten(StammdatenEditor kopf, bool erwartet)
    {
        Wait.Within(() => kopf.SpeichernFreigegeben == erwartet, Wait.DefaultTimeout);
        return kopf.SpeichernFreigegeben;
    }
}
