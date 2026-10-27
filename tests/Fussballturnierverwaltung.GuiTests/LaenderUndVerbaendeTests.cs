using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Stammdatenpflege Länder und Verbände (#530): anlegen, bearbeiten, Prüfung beim Speichern und Löschen mit
/// Rückfrage, je Reiter. Jeder Test legt seine Daten unter eindeutigen Namen selbst an; der Seed bleibt unverändert.
/// FIFA-Code und Verbandskürzel sind in der Datenbank eindeutig, darum werden auch sie zufällig gebildet.</summary>
public sealed class LaenderUndVerbaendeTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private readonly LaenderUndVerbaendeSeite _seite = app.Hauptfenster.LaenderUndVerbaendeOeffnen();

    // ── Verbände ────────────────────────────────────────────────────────────

    [Fact]
    public void Verband_Anlegen_ErscheintMitSeinemKuerzel()
    {
        string name = EindeutigerName();
        string kuerzel = Kuerzel();

        _seite.VerbandAnlegen(name, kuerzel);

        _seite.Verband(name).Should().Be(new VerbandEintrag(name, kuerzel));
    }

    [Fact]
    public void Verband_Bearbeiten_UebernimmtDieAenderungen()
    {
        string name = EindeutigerName();
        _seite.VerbandAnlegen(name, Kuerzel());
        string neuesKuerzel = Kuerzel();

        var verbaende = _seite.VerbaendeReiter();
        verbaende.Auswaehlen(name);
        verbaende.WertSetzen("Kurzname", neuesKuerzel);
        verbaende.Speichern();

        _seite.Verband(name).Should().Be(new VerbandEintrag(name, neuesKuerzel));
    }

    [Fact]
    public void Verband_OhneNamen_WirdNichtGespeichert()
    {
        var verbaende = _seite.VerbaendeReiter();
        verbaende.Neu();
        verbaende.WertSetzen("Kurzname", Kuerzel());

        verbaende.SpeichernAbgelehnt("Verbandsname darf nicht leer sein.")
            .Should().Contain("Verbandsname darf nicht leer sein.");
        verbaende.SpeichernFreigegeben.Should().BeTrue("die Eingabe ist noch nicht gespeichert");

        verbaende.Abbrechen();
    }

    [Fact]
    public void Verband_Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string name = EindeutigerName();
        _seite.VerbandAnlegen(name, Kuerzel());
        LoeschenMitRueckfrage(_seite.Verbaende, name);
    }

    // ── Länder ──────────────────────────────────────────────────────────────

    [Fact]
    public void Land_Anlegen_GehoertZumGewaehltenVerband()
    {
        string verband = EindeutigerName();
        string kuerzel = Kuerzel();
        _seite.VerbandAnlegen(verband, kuerzel);
        string name = EindeutigerName();
        string fifaCode = FifaCode();

        _seite.LandAnlegen(name, fifaCode, verband);

        _seite.Land(name).Should().Be(new LandEintrag(name, fifaCode, kuerzel));
    }

    [Fact]
    public void Land_Bearbeiten_UebernimmtDieAenderungen()
    {
        string verband = EindeutigerName();
        _seite.VerbandAnlegen(verband, Kuerzel());
        string name = EindeutigerName();
        _seite.LandAnlegen(name, FifaCode(), verband);
        string neuerCode = FifaCode();

        var laender = _seite.LaenderReiter();
        laender.Auswaehlen(name);
        laender.WertSetzen("FifaCode", neuerCode);
        laender.Speichern();

        _seite.Land(name).FifaCode.Should().Be(neuerCode);
    }

    [Fact]
    public void Land_OhneFifaCode_WirdNichtGespeichert()
    {
        var laender = _seite.LaenderReiter();
        laender.Neu();
        laender.WertSetzen("Name", EindeutigerName());
        laender.WertSetzen("Kurzname", "GT");

        laender.SpeichernAbgelehnt("FIFA-Code darf nicht leer sein.")
            .Should().Contain("FIFA-Code darf nicht leer sein.");
        laender.SpeichernFreigegeben.Should().BeTrue("die Eingabe ist noch nicht gespeichert");

        laender.Abbrechen();
    }

    [Fact]
    public void Land_Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string verband = EindeutigerName();
        _seite.VerbandAnlegen(verband, Kuerzel());
        string name = EindeutigerName();
        _seite.LandAnlegen(name, FifaCode(), verband);

        LoeschenMitRueckfrage(_seite.LaenderReiter(), name);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static void LoeschenMitRueckfrage(StammdatenEditor editor, string name)
    {
        editor.Auswaehlen(name);
        editor.Loeschen(bestaetigen: false);
        editor.Eintrag(name, e => e).Should().NotBeNull("Abbrechen lässt den Eintrag stehen");

        editor.Auswaehlen(name);
        editor.Loeschen(bestaetigen: true);
        editor.NichtMehrGelistetAbwarten(name).Should().BeTrue("der Eintrag ist gelöscht");
    }

    private static string EindeutigerName() => $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";

    private static string Kuerzel() => $"GT{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    /// <summary>Drei Zeichen, mit Ziffer vorn, damit kein echter FIFA-Code des Seeds getroffen wird.</summary>
    private static string FifaCode() => $"9{Guid.NewGuid().ToString("N")[..2].ToUpperInvariant()}";
}
