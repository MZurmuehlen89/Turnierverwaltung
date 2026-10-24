using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Die laufende App für eine Testklasse (Klassen-Fixture): startet gegen die Test-Datenbank des Laufs,
/// mit eigenem Datenpfad und festem Zufalls-Seed, und wird nach der Klasse wieder geschlossen.</summary>
public sealed class TurnierverwaltungApp : IDisposable
{
    // Der erste Start eines Laufs legt die Datenbank samt Seed an
    private static readonly TimeSpan _startZeit = TimeSpan.FromMinutes(2);
    private const int LOG_ZEILEN_BEI_FEHLSCHLAG = 200;

    private readonly AppSession _sitzung;
    private readonly string _datenpfad;
    private readonly string _belegOrdner;

    public TurnierverwaltungApp(TestDatenbank datenbank)
    {
        _datenpfad = Path.Combine(datenbank.LaufOrdner, "app");
        _belegOrdner = Path.Combine(datenbank.LaufOrdner, "fehlschlaege");

        _sitzung = AppSession.Launch(new AppLaunchOptions(GuiTestUmgebung.AppPfad)
        {
            StartupTimeout = _startZeit,
            Environment = new Dictionary<string, string>
            {
                ["ConnectionStrings__Standard"] = datenbank.Verbindung,
                ["TURNIERVERWALTUNG_DATENPFAD"] = _datenpfad,
                ["Zufall__Seed"] = GuiTestUmgebung.ZUFALLS_SEED,
            },
        });
        Hauptfenster = new Hauptfenster(_sitzung.MainWindow);
    }

    public Hauptfenster Hauptfenster { get; }

    /// <summary>Aus dem Dispose der Testklasse aufzurufen: Ist der Test fehlgeschlagen, legt es Bildschirmfoto und
    /// Log-Ende im Lauf-Ordner ab und hängt beides an das Testergebnis (Anhänge erscheinen nur in Ergebnisdateien).</summary>
    public void BeiFehlschlagBelegeSichern()
    {
        var kontext = TestContext.Current;
        if (kontext.TestState?.Result != TestResult.Failed || _sitzung.HasExited) return;

        Directory.CreateDirectory(_belegOrdner);
        var basis = Path.Combine(_belegOrdner, DateiName(kontext.Test?.TestDisplayName));
        _sitzung.CaptureScreenshot($"{basis}.png");
        var log = LogTail.Read(NeuestesLog(), LOG_ZEILEN_BEI_FEHLSCHLAG);
        File.WriteAllText($"{basis}.log.txt", log);
        kontext.AddAttachment("Bildschirmfoto", File.ReadAllBytes($"{basis}.png"), "image/png");
        kontext.AddAttachment("App-Log (Ende)", log);
    }

    public void Dispose() => _sitzung.Dispose();

    private string NeuestesLog() =>
        new DirectoryInfo(Path.Combine(_datenpfad, "logs")) is { Exists: true } logs
            && logs.GetFiles("turnierverwaltung-*.log").MaxBy(d => d.LastWriteTimeUtc) is { } datei
                ? datei.FullName
                : Path.Combine(_datenpfad, "logs");

    private static string DateiName(string? text) =>
        string.Concat((text ?? "Test").Select(z => Path.GetInvalidFileNameChars().Contains(z) ? '_' : z));
}
