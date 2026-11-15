using System.Globalization;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Fussballturnierverwaltung.Infrastructure.Wartung;

/// <summary>Eine Sicherungsdatei im Sicherungsordner.</summary>
public record Sicherung(string Pfad, DateTime Zeitpunkt, long Groesse)
{
    public string Name => Path.GetFileName(Pfad);
}

/// <summary>Ein Fehler beim Sichern oder Wiederherstellen, dessen Text für den Nutzer bestimmt ist.</summary>
public class DatenbankSicherungException(string meldung) : Exception(meldung);

/// <summary>Issue #43: Sichern per <c>pg_dump</c> und Wiederherstellen per <c>pg_restore</c> auf Knopfdruck – keine
/// automatische Sicherung (Nutzerentscheidung). Wiederhergestellt wird in eine Hilfsdatenbank, die erst nach
/// erfolgreicher Prüfung den Namen der bisherigen übernimmt: Ein Fehler davor lässt den bisherigen Stand unberührt.</summary>
public class DatenbankSicherungService(
    TurnierverwaltungDbContext context, PostgresWerkzeuge werkzeuge, ILogger<DatenbankSicherungService> logger)
{
    public const string ENDUNG = ".dump";
    private const string PG_DUMP = "pg_dump";
    private const string PG_RESTORE = "pg_restore";
    private const string MIGRATIONSTABELLE = "__EFMigrationsHistory";

    // ── Sichern ────────────────────────────────────────

    public static string Dateiname(string datenbank, DateTime zeitpunkt) =>
        $"{string.Concat(datenbank.Select(z => Path.GetInvalidFileNameChars().Contains(z) ? '_' : z))}_{zeitpunkt.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}{ENDUNG}";

    /// <summary>Die Sicherungen im Ordner, neueste zuerst; ein fehlender Ordner heißt: noch keine.</summary>
    public static IReadOnlyList<Sicherung> Auflisten(string ordner) =>
        Directory.Exists(ordner)
            ? [.. new DirectoryInfo(ordner).EnumerateFiles("*" + ENDUNG)
                .OrderByDescending(d => d.LastWriteTime)
                .Select(d => new Sicherung(d.FullName, d.LastWriteTime, d.Length))]
            : [];

    public async Task<Sicherung> SichernAsync(string ordner, CancellationToken ct = default)
    {
        var programm = WerkzeugFinden(PG_DUMP);
        var ziel = Ziel();
        Directory.CreateDirectory(ordner);
        var datei = Path.Combine(ordner, Dateiname(ziel.Database ?? "", DateTime.Now));

        logger.LogInformation("Sicherung der Datenbank {Datenbank} nach {Datei}", ziel.Database, datei);
        string? fehler;
        try
        {
            fehler = await WerkzeugAufruf.AusfuehrenAsync(
                WerkzeugAufruf.Erstellen(programm, ziel, ["-Fc", "-f", datei, ziel.Database ?? ""]), ct);
        }
        catch
        {
            // Eine halb geschriebene Datei stünde sonst als Sicherung in der Liste.
            File.Delete(datei);
            throw;
        }
        if (fehler is not null)
        {
            File.Delete(datei);
            throw new DatenbankSicherungException($"Die Sicherung ist fehlgeschlagen: {fehler}");
        }

        var info = new FileInfo(datei);
        return new Sicherung(info.FullName, info.LastWriteTime, info.Length);
    }

    // ── Wiederherstellen ───────────────────────────────

    /// <summary>Ersetzt die Datenbank der App durch den Stand der Sicherung. Danach muss die App neu starten: Ihre
    /// Seiten zeigen noch den alten Stand, und eine ältere Sicherung braucht die Migrationen des Startpfads.</summary>
    public async Task WiederherstellenAsync(string datei, CancellationToken ct = default)
    {
        if (!File.Exists(datei))
            throw new DatenbankSicherungException($"Die Sicherung „{datei}“ gibt es nicht.");
        var programm = WerkzeugFinden(PG_RESTORE);
        var ziel = Ziel();
        var name = ziel.Database ?? "";
        var hilfsdatenbank = $"tv_wiederherstellung_{Guid.NewGuid():N}";

        logger.LogInformation("Wiederherstellung der Datenbank {Datenbank} aus {Datei}", name, datei);
        await VerwaltenAsync($"CREATE DATABASE {Bezeichner(hilfsdatenbank)} TEMPLATE template0 ENCODING 'UTF8' "
            + "LOCALE_PROVIDER icu ICU_LOCALE 'de-DE' LOCALE 'C'", ct);
        try
        {
            await EinspielenAsync(programm, ziel, hilfsdatenbank, datei, ct);
            await MigrationsstandPruefenAsync(hilfsdatenbank, ct);
            await TauschenAsync(name, hilfsdatenbank, ct);
        }
        catch
        {
            await HilfsdatenbankVerwerfenAsync(hilfsdatenbank);
            throw;
        }
    }

    private static async Task EinspielenAsync(
        string programm, NpgsqlConnectionStringBuilder ziel, string hilfsdatenbank, string datei, CancellationToken ct)
    {
        // Ohne Besitzer und Rechte aus der Sicherung: Sie kann von einer anderen Anmeldung stammen.
        var fehler = await WerkzeugAufruf.AusfuehrenAsync(WerkzeugAufruf.Erstellen(programm, ziel,
            ["--no-owner", "--no-privileges", "--single-transaction", "--exit-on-error", "-d", hilfsdatenbank, datei]), ct);
        if (fehler is not null)
            throw new DatenbankSicherungException($"Die Sicherung ließ sich nicht einspielen: {fehler}");
    }

    private async Task MigrationsstandPruefenAsync(string hilfsdatenbank, CancellationToken ct)
    {
        await using var verbindung = new NpgsqlConnection(Verbindung(hilfsdatenbank));
        await verbindung.OpenAsync(ct);
        await using var tabelle = new NpgsqlCommand($"SELECT to_regclass('public.\"{MIGRATIONSTABELLE}\"') IS NOT NULL", verbindung);
        if (await tabelle.ExecuteScalarAsync(ct) is not true)
            throw new DatenbankSicherungException("Die Datei ist keine Sicherung der Turnierverwaltung.");

        List<string> eingespielt = [];
        await using var abfrage = new NpgsqlCommand($"SELECT \"MigrationId\" FROM \"{MIGRATIONSTABELLE}\"", verbindung);
        await using (var leser = await abfrage.ExecuteReaderAsync(ct))
            while (await leser.ReadAsync(ct))
                eingespielt.Add(leser.GetString(0));

        var unbekannt = eingespielt.Except(context.Database.GetMigrations()).Order().ToList();
        if (unbekannt.Count > 0)
            throw new DatenbankSicherungException("Die Sicherung stammt aus einer neueren Version der Turnierverwaltung "
                + $"(unbekannte Migration {string.Join(", ", unbekannt)}). Bitte erst die App aktualisieren.");
    }

    /// <summary>Gibt der Hilfsdatenbank den Namen der bisherigen und löscht diese danach. Umbenennen geht nur ohne
    /// offene Verbindungen – die ungenutzten im Pool der App werden dafür geschlossen, fremde nicht getrennt.</summary>
    private async Task TauschenAsync(string name, string hilfsdatenbank, CancellationToken ct)
    {
        NpgsqlConnection.ClearPool((NpgsqlConnection)context.Database.GetDbConnection());
        var bisherige = $"tv_vor_wiederherstellung_{Guid.NewGuid():N}";
        await VerwaltenAsync($"ALTER DATABASE {Bezeichner(name)} RENAME TO {Bezeichner(bisherige)}", ct);
        try
        {
            await VerwaltenAsync($"ALTER DATABASE {Bezeichner(hilfsdatenbank)} RENAME TO {Bezeichner(name)}", ct);
        }
        catch
        {
            await ZurueckbenennenAsync(bisherige, name);
            throw;
        }

        try
        {
            await VerwaltenAsync($"DROP DATABASE {Bezeichner(bisherige)}", CancellationToken.None);
        }
        catch (Exception ex) when (ex is NpgsqlException or DatenbankSicherungException)
        {
            // Die Wiederherstellung selbst ist gelungen – der alte Stand bleibt nur unter seinem Hilfsnamen liegen.
            logger.LogWarning(ex, "Die bisherige Datenbank {Datenbank} ließ sich nicht löschen", bisherige);
        }
    }

    private async Task ZurueckbenennenAsync(string bisherige, string name)
    {
        try
        {
            await VerwaltenAsync($"ALTER DATABASE {Bezeichner(bisherige)} RENAME TO {Bezeichner(name)}", CancellationToken.None);
        }
        catch (Exception ex) when (ex is NpgsqlException or DatenbankSicherungException)
        {
            logger.LogError(ex, "Die bisherige Datenbank ließ sich nicht von {Hilfsname} zurück in {Datenbank} umbenennen",
                bisherige, name);
            throw new DatenbankSicherungException("Die Wiederherstellung ist gescheitert, und die bisherige Datenbank "
                + $"ließ sich nicht zurückbenennen: Sie liegt unverändert unter dem Namen „{bisherige}“. Bitte von Hand "
                + $"(pgAdmin, psql) in „{name}“ umbenennen, bevor das Programm neu startet.");
        }
    }

    private async Task HilfsdatenbankVerwerfenAsync(string hilfsdatenbank)
    {
        try
        {
            await VerwaltenAsync($"DROP DATABASE IF EXISTS {Bezeichner(hilfsdatenbank)}", CancellationToken.None);
        }
        catch (Exception ex) when (ex is NpgsqlException or DatenbankSicherungException)
        {
            logger.LogWarning(ex, "Die Hilfsdatenbank {Datenbank} ließ sich nicht löschen", hilfsdatenbank);
        }
    }

    // ── Hilfen ─────────────────────────────────────────

    private string WerkzeugFinden(string programm) =>
        werkzeuge.Finden(programm) ?? throw new DatenbankSicherungException(
            $"Das Programm {programm} von PostgreSQL wurde nicht gefunden. Es gehört zu den Client-Werkzeugen von "
            + "PostgreSQL – bitte PostgreSQL (oder nur dessen Kommandozeilenwerkzeuge) auf diesem Rechner installieren.");

    private NpgsqlConnectionStringBuilder Ziel() => new(context.Database.GetConnectionString());

    // Ohne Pool: Eine liegengebliebene Verbindung auf die Hilfsdatenbank verhinderte deren Umbenennen und Löschen.
    private string Verbindung(string datenbank) =>
        new NpgsqlConnectionStringBuilder(Ziel().ConnectionString) { Database = datenbank, Pooling = false }.ConnectionString;

    /// <summary>Befehle auf Datenbankebene laufen über die Verwaltungsdatenbank „postgres“.</summary>
    private async Task VerwaltenAsync(string sql, CancellationToken ct)
    {
        try
        {
            await using var verbindung = new NpgsqlConnection(Verbindung("postgres"));
            await verbindung.OpenAsync(ct);
            await using var befehl = new NpgsqlCommand(sql, verbindung);
            await befehl.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ObjectInUse)
        {
            throw new DatenbankSicherungException($"Die Datenbank „{Ziel().Database}“ ist noch in einem anderen "
                + "Programm geöffnet (zweite Turnierverwaltung, pgAdmin, psql …) oder wird gerade von einer anderen Seite "
                + "dieses Programms gelesen. Bitte dort schließen und erneut versuchen.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            throw new DatenbankSicherungException($"„{Ziel().Username}“ fehlt ein Recht für die Wiederherstellung: Datenbanken "
                + $"anlegen (CREATEDB) und die Datenbank „{Ziel().Database}“ besitzen. ({ex.MessageText})");
        }
    }

    private static string Bezeichner(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
}
