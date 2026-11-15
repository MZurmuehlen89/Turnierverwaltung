using System.Text.Json;
using FluentAssertions;
using GuiTest.Framework;
using Npgsql;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Startfenster bei einem Verbindungsfehler (#49, AK7). Die App startet hier ohne die Vorgabe der übrigen
/// GUI-Tests und mit einer Einstellungsdatei, deren Port nicht stimmt. Das Kennwort steht nicht darin: Wie bei einer
/// Einrichtung per build/PostgreSQL-Einrichten.ps1 findet PostgreSQL es in pgpass.conf (lokal und in CI).</summary>
public sealed class StartfensterTests(TestDatenbank datenbank) : IDisposable
{
    private const string STARTFENSTER = "Turnierverwaltung – Datenbankverbindung";
    private const string HAUPTFENSTER = "Turnierverwaltung";
    private const int FALSCHER_PORT = 1;

    // Nach „Verbinden“ legt die App die Testdatenbank an, falls dieser Test der erste des Laufs ist (Migration, Seed)
    private static readonly TimeSpan _startZeit = TimeSpan.FromMinutes(2);

    private readonly NpgsqlConnectionStringBuilder _ziel = new(GuiTestUmgebung.Grundverbindung) { Database = datenbank.Name };
    private readonly string _datenpfad = Path.Combine(datenbank.LaufOrdner, $"startfenster-{Guid.NewGuid():N}");
    private AppSession? _sitzung;

    [Fact]
    public void FalscherPort_ZeigtUrsache_VerbindenMitRichtigemPortOeffnetHauptfenster()
    {
        var fenster = Starten();

        fenster.TextOf("Verbindung.Fehlermeldung").Should().Be(
            $"Keine Verbindung zu {_ziel.Host}:{FALSCHER_PORT} – läuft der PostgreSQL-Dienst?");

        fenster.FindById("Verbindung.Port").Patterns.Value.Pattern.SetValue(_ziel.Port.ToString());
        fenster.ActivateById("Startfenster.Verbinden");

        _sitzung!.WaitForWindow(HAUPTFENSTER, _startZeit).Close();
        GespeicherterPort().Should().Be(_ziel.Port, "Verbinden speichert die funktionierende Verbindung");
    }

    [Fact]
    public void Beenden_BeendetDieApp()
    {
        Starten().ActivateById("Startfenster.Beenden");

        _sitzung!.WaitForExit(TimeSpan.FromSeconds(15)).Should().BeTrue("ohne Verbindung gibt es kein Hauptfenster");
    }

    public void Dispose() => _sitzung?.Dispose();

    private FlaUI.Core.AutomationElements.Window Starten()
    {
        _ziel.Password.Should().BeNull("das Kennwort kommt aus pgpass.conf, eine Einstellungsdatei hätte es verschlüsselt");
        Directory.CreateDirectory(_datenpfad);
        File.WriteAllText(Path.Combine(_datenpfad, "einstellungen.json"), JsonSerializer.Serialize(new
        {
            Datenbank = new { Server = _ziel.Host, Port = FALSCHER_PORT, Datenbank = _ziel.Database, Benutzer = _ziel.Username }
        }));

        _sitzung = AppSession.Launch(new AppLaunchOptions(GuiTestUmgebung.AppPfad)
        {
            StartupTimeout = _startZeit,
            Environment = new Dictionary<string, string>
            {
                ["TURNIERVERWALTUNG_DATENPFAD"] = _datenpfad,
                ["Zufall__Seed"] = GuiTestUmgebung.ZUFALLS_SEED,
            },
        });
        _sitzung.MainWindow.Title.Should().Be(STARTFENSTER);
        return _sitzung.MainWindow;
    }

    private int GespeicherterPort()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_datenpfad, "einstellungen.json")));
        return json.RootElement.GetProperty("Datenbank").GetProperty("Port").GetInt32();
    }
}
