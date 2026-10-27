using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Import und Export über die Windows-Dateidialoge (#537). Rundlauf: Eine eigene Mannschaft wird exportiert,
/// gelöscht und aus der exportierten Datei wieder importiert – danach steht sie mit denselben Werten in der Liste.
/// Die Datei liegt im Temp-Ordner unter eindeutigem Namen und wird am Ende gelöscht.</summary>
public sealed class ImportExportTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private readonly string _datei = Path.Combine(Path.GetTempPath(), $"GuiTest_{Guid.NewGuid():N}.csv");

    [Fact]
    public void Mannschaften_ExportUndReimport_StelltDieGeloeschteMannschaftWiederHer()
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        var mannschaften = app.Hauptfenster.MannschaftenOeffnen();
        mannschaften.Anlegen(name, "GTI", 70);
        var original = mannschaften.Eintrag(name);

        var importExport = app.Hauptfenster.ImportExportOeffnen();
        importExport.MannschaftenExportieren(_datei);
        importExport.Fehlermeldung.Should().BeEmpty();
        File.ReadAllText(_datei).Should().Contain(name, "die Datei enthält die neue Mannschaft");

        mannschaften = app.Hauptfenster.MannschaftenOeffnen();
        mannschaften.Auswaehlen(name);
        mannschaften.Loeschen(bestaetigen: true);
        mannschaften.NichtMehrGelistetAbwarten(name).Should().BeTrue();

        importExport = app.Hauptfenster.ImportExportOeffnen();
        importExport.MannschaftenImportieren(_datei).Should().Contain("1 neu", "nur die gelöschte Mannschaft fehlte");
        importExport.Fehlermeldung.Should().BeEmpty();

        app.Hauptfenster.MannschaftenOeffnen().Eintrag(name).Should().Be(original);
    }

    public void Dispose()
    {
        app.BeiFehlschlagBelegeSichern();
        File.Delete(_datei);
    }
}
