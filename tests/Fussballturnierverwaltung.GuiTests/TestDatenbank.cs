using Microsoft.Data.SqlClient;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Eine eigene Datenbank je Testlauf (Assembly-Fixture). Angelegt wird sie von der App selbst beim ersten
/// Start, samt Migrationen und Seed (#370, rund 9 s); hier werden nur Name und Lauf-Ordner vergeben und am Ende
/// wird die Datenbank wieder gelöscht. Die echte Datenbank des Nutzers wird nie berührt.</summary>
public sealed class TestDatenbank : IAsyncDisposable
{
    public TestDatenbank()
    {
        Name = $"Fussballturnierverwaltung_GuiTest_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        LaufOrdner = Path.Combine(GuiTestUmgebung.ArtefaktOrdner, "laeufe", Name);
        Directory.CreateDirectory(LaufOrdner);
    }

    public string Name { get; }

    /// <summary>Ablage dieses Laufs: Datenpfad der App (Einstellungen, Logs) und Belege fehlgeschlagener Tests.</summary>
    public string LaufOrdner { get; }

    public string Verbindung => Verbindungstext(Name);

    /// <summary>Liest einen Einzelwert direkt aus der Test-Datenbank – für Zustände, die die Oberfläche nicht zeigt
    /// (#538). Werte nur über Parameter, nie in den SQL-Text.</summary>
    public async Task<T> WertLesenAsync<T>(string sql, params (string Name, object Wert)[] parameter)
    {
        await using var verbindung = new SqlConnection(Verbindung);
        await verbindung.OpenAsync();
        await using var befehl = Befehl(verbindung, sql, parameter);
        return (T)Convert.ChangeType((await befehl.ExecuteScalarAsync())!, typeof(T));
    }

    /// <summary>Ändert die Test-Datenbank an der Oberfläche vorbei, etwa um einen fehlenden Seed-Eintrag
    /// herzustellen (#538).</summary>
    public async Task<int> AusfuehrenAsync(string sql, params (string Name, object Wert)[] parameter)
    {
        await using var verbindung = new SqlConnection(Verbindung);
        await verbindung.OpenAsync();
        await using var befehl = Befehl(verbindung, sql, parameter);
        return await befehl.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using var verbindung = new SqlConnection(Verbindungstext("master"));
        await verbindung.OpenAsync();
        await using var befehl = verbindung.CreateCommand();
        // Name wird oben selbst erzeugt (nur Buchstaben, Ziffern, Unterstriche) – keine Fremdeingabe im SQL
        befehl.CommandText = $"""
            IF DB_ID(N'{Name}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{Name}];
            END
            """;
        await befehl.ExecuteNonQueryAsync();
    }

    private static SqlCommand Befehl(SqlConnection verbindung, string sql, (string Name, object Wert)[] parameter)
    {
        var befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        foreach (var (name, wert) in parameter)
            befehl.Parameters.AddWithValue(name, wert);
        return befehl;
    }

    private static string Verbindungstext(string datenbank) =>
        $"Server={GuiTestUmgebung.SqlServer};Database={datenbank};Trusted_Connection=True;TrustServerCertificate=True;";
}
