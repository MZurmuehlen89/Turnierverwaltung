using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fussballturnierverwaltung.Tests.Persistenz;

/// <summary>Eine frische PostgreSQL-Datenbank für Tests, die das echte Verhalten brauchen (#49): Sortierung, Umlaute,
/// Fehler beim Verbindungsaufbau. Die Grundverbindung kommt aus <see cref="VARIABLE"/> (CI: Dienst-Container) – ist sie
/// gesetzt, muss sie funktionieren. Ohne die Variable wird das lokale PostgreSQL mit der Anmeldung aus
/// build/PostgreSQL-Einrichten.ps1 versucht; fehlt es, werden die Tests übersprungen statt rot.</summary>
public sealed class PostgresDatenbank : IAsyncLifetime
{
    public const string VARIABLE = "TURNIERVERWALTUNG_TEST_POSTGRES";
    private const string LOKAL = "Host=localhost;Port=5432;Username=turnierverwaltung";

    private readonly string? _vorgabe = Environment.GetEnvironmentVariable(VARIABLE);

    public string Name { get; } = $"turnierverwaltung_unittest_{Guid.NewGuid():N}";
    public bool Verfuegbar { get; private set; }
    public string Grundverbindung => string.IsNullOrWhiteSpace(_vorgabe) ? LOKAL : _vorgabe;
    public string Verbindung => new NpgsqlConnectionStringBuilder(Grundverbindung) { Database = Name }.ConnectionString;

    public TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>().UseNpgsql(Verbindung).Options);

    public async ValueTask InitializeAsync()
    {
        try
        {
            await using var context = NeuerContext();
            await context.Database.MigrateAsync();
            Verfuegbar = true;
        }
        catch (NpgsqlException) when (string.IsNullOrWhiteSpace(_vorgabe))
        {
            // Kein lokales PostgreSQL – die Tests melden sich als übersprungen.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!Verfuegbar) return;
        NpgsqlConnection.ClearAllPools();
        await using var verbindung = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(Grundverbindung) { Database = "postgres" }.ConnectionString);
        await verbindung.OpenAsync();
        await using var befehl = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", verbindung);
        await befehl.ExecuteNonQueryAsync();
    }

    public void VerfuegbarOderUeberspringen() =>
        Assert.SkipUnless(Verfuegbar, $"Kein PostgreSQL erreichbar ({VARIABLE} nicht gesetzt, lokal keins eingerichtet).");
}
