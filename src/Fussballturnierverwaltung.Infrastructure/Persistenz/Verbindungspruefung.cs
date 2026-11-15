using System.Net.Sockets;
using Npgsql;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

/// <summary>Prüft eine Datenbankverbindung, bevor sie gespeichert wird oder die App damit startet (#49), und
/// übersetzt die typischen Fehler in eine verständliche Ursache. Erfolg wird nicht gemeldet, nur Fehler.</summary>
public static class Verbindungspruefung
{
    private const int WARTEZEIT_SEKUNDEN = 5;

    /// <returns>Die Ursache als Text, oder <c>null</c>, wenn die Verbindung steht.</returns>
    public static async Task<string?> PruefenAsync(string zeichenfolge, CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(zeichenfolge) { Timeout = WARTEZEIT_SEKUNDEN };
        try
        {
            await OeffnenAsync(builder.ConnectionString, ct);
            return null;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            // Die Datenbank fehlt – die App legt sie beim Start an, wenn die Anmeldung selbst funktioniert.
            return await AnmeldungOhneDatenbankPruefenAsync(builder, ct);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            return Ursache(ex, builder);
        }
    }

    private static async Task<string?> AnmeldungOhneDatenbankPruefenAsync(
        NpgsqlConnectionStringBuilder builder, CancellationToken ct)
    {
        var ohneDatenbank = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = "postgres" };
        try
        {
            await OeffnenAsync(ohneDatenbank.ConnectionString, ct);
            return null;
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            return Ursache(ex, builder);
        }
    }

    private static async Task OeffnenAsync(string zeichenfolge, CancellationToken ct)
    {
        await using var verbindung = new NpgsqlConnection(zeichenfolge);
        await verbindung.OpenAsync(ct);
    }

    /// <summary>Der Verbindungsfehler in der Kette der inneren Ausnahmen – <c>null</c>, wenn die Ausnahme etwas
    /// anderes ist (etwa eine fehlerhafte Migration), bei dem andere Zugangsdaten nicht helfen.</summary>
    public static Exception? VerbindungsfehlerFinden(Exception ex)
    {
        for (var aktuell = ex; aktuell is not null; aktuell = aktuell.InnerException)
        {
            if (aktuell is PostgresException postgres)
                return postgres.SqlState is PostgresErrorCodes.InvalidPassword
                    or PostgresErrorCodes.InvalidAuthorizationSpecification
                    or PostgresErrorCodes.InvalidCatalogName ? postgres : null;
            if (aktuell is NpgsqlException or TimeoutException) return aktuell;
        }
        return null;
    }

    public static string Ursache(Exception ex, NpgsqlConnectionStringBuilder ziel) => ex switch
    {
        PostgresException { SqlState: PostgresErrorCodes.InvalidPassword or PostgresErrorCodes.InvalidAuthorizationSpecification }
            => $"Anmeldung als „{ziel.Username}“ abgelehnt: Kennwort falsch oder fehlt.",
        PostgresException { SqlState: PostgresErrorCodes.InvalidCatalogName }
            => $"Die Datenbank „{ziel.Database}“ gibt es nicht, und „{ziel.Username}“ darf keine anlegen.",
        PostgresException postgres => postgres.MessageText,
        _ when ex is TimeoutException || ex.InnerException is SocketException or TimeoutException
            => $"Keine Verbindung zu {ziel.Host}:{ziel.Port} – läuft der PostgreSQL-Dienst?",
        _ => ex.Message
    };
}
