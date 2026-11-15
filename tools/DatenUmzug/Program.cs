// #49: Kopiert den Bestand mit allen IDs aus der SQL-Server-Datenbank nach PostgreSQL – einmal je Installation, beim
// Umstieg. Ablauf:
//   1. Ziel per Migration auf den Stand der App bringen (legt das Schema an, falls es fehlt).
//   2. Ziel leeren. Hat es schon Daten (etwa den Seed eines ersten App-Starts), nur mit --ersetzen.
//   3. Alle Tabellen per binärem COPY übertragen; die alte Migrationstabelle bleibt außen vor, das Ziel hat seine eigene.
//      Fremdschlüssel-Trigger sind währenddessen aus (session_replication_role = replica), damit Reihenfolge und die
//      Zyklen im Turnierbaum keine Rolle spielen – die Quelle ist referenziell konsistent. Das braucht einen
//      Superuser, deshalb verbindet sich das Werkzeug als „postgres“ mit dem Ziel.
//   4. Identity-Folgen nachziehen, Verbandskürzel großschreiben (wie die App sie seit #49 speichert), Zeilen abgleichen.
//
// Aufruf:   dotnet run --project tools/DatenUmzug -- [--ersetzen] [--quelle <SQL Server>] [--ziel <PostgreSQL>]
// Vorgaben: Quelle Server=.;Database=Fussballturnierverwaltung (Windows-Anmeldung, nur lesend)
//           Ziel   Host=localhost;Database=turnierverwaltung;Username=postgres (Kennwort aus pgpass.conf)

using System.Diagnostics;
using System.Text.Json;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

const string MIGRATIONSTABELLE = "__EFMigrationsHistory";

var ersetzen = args.Contains("--ersetzen");
var quelle = Wert("--quelle")
    ?? "Server=.;Database=Fussballturnierverwaltung;Trusted_Connection=True;TrustServerCertificate=True;ApplicationIntent=ReadOnly;";
var ziel = Wert("--ziel") ?? "Host=localhost;Database=turnierverwaltung;Username=postgres";

var gesamt = Stopwatch.StartNew();
await SchemaAktualisierenAsync(ziel);

await using var pg = new NpgsqlConnection(ziel);
await pg.OpenAsync();
await using var sql = new SqlConnection(quelle);
await sql.OpenAsync();

var tabellen = await TabellenLesenAsync(pg);
var vorhanden = await ZeilenZaehlenAsync(pg, tabellen);
if (vorhanden > 0 && !ersetzen)
{
    Console.WriteLine($"Das Ziel enthält schon {vorhanden} Zeilen. Mit --ersetzen werden sie durch den Bestand der Quelle ersetzt.");
    return 2;
}

await using (var tx = await pg.BeginTransactionAsync())
{
    await AusfuehrenAsync(pg, "TRUNCATE " + string.Join(", ", tabellen.Select(t => Q(t.Name))) + " RESTART IDENTITY");
    await AusfuehrenAsync(pg, "SET LOCAL session_replication_role = replica");
    foreach (var tabelle in tabellen)
        await TabelleKopierenAsync(sql, pg, tabelle);
    await tx.CommitAsync();
}

await FolgenNachziehenAsync(pg, tabellen);
await KurznamenGrossschreibenAsync(pg);
var abweichungen = await ZeilenAbgleichenAsync(sql, pg, tabellen);
Console.WriteLine($"Fertig in {gesamt.ElapsedMilliseconds} ms, {tabellen.Count} Tabellen, {abweichungen} Abweichung(en).");
return abweichungen == 0 ? 0 : 1;

string? Wert(string schalter)
{
    var i = Array.IndexOf(args, schalter);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

// ── Schema ────────────────────────────────────────────────────────────────

// Als Rolle des Datenbankbesitzers, sonst gehörten neu angelegte Tabellen „postgres“ und die App dürfte nicht schreiben.
static async Task SchemaAktualisierenAsync(string verbindung)
{
    await using var pg = new NpgsqlConnection(verbindung);
    await pg.OpenAsync();
    await using (var besitzer = new NpgsqlCommand(
        "SELECT format('SET ROLE %I', pg_get_userbyid(datdba)) FROM pg_database WHERE datname = current_database()", pg))
        await AusfuehrenAsync(pg, (string)(await besitzer.ExecuteScalarAsync())!);

    var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>().UseNpgsql(pg).Options;
    await using var context = new TurnierverwaltungDbContext(optionen);
    var ausstehend = (await context.Database.GetPendingMigrationsAsync()).ToList();
    await context.Database.MigrateAsync();
    Console.WriteLine(ausstehend.Count == 0 ? "Schema aktuell." : $"Migration(en) eingespielt: {string.Join(", ", ausstehend)}");
}

static async Task<List<Tabelle>> TabellenLesenAsync(NpgsqlConnection pg)
{
    const string abfrage = """
        SELECT c.table_name, c.column_name, c.udt_name, c.is_identity = 'YES'
        FROM information_schema.columns c
        JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name
        WHERE c.table_schema = 'public' AND t.table_type = 'BASE TABLE' AND c.table_name <> @migrationen
        ORDER BY c.table_name, c.ordinal_position
        """;
    var spalten = new List<(string Tabelle, Spalte Spalte)>();
    await using var cmd = new NpgsqlCommand(abfrage, pg);
    cmd.Parameters.AddWithValue("migrationen", MIGRATIONSTABELLE);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        spalten.Add((reader.GetString(0), new Spalte(reader.GetString(1), reader.GetString(2), reader.GetBoolean(3))));
    return [.. spalten.GroupBy(s => s.Tabelle).Select(g => new Tabelle(g.Key, [.. g.Select(s => s.Spalte)]))];
}

// ── Kopie ─────────────────────────────────────────────────────────────────

static async Task TabelleKopierenAsync(SqlConnection sql, NpgsqlConnection pg, Tabelle tabelle)
{
    var uhr = Stopwatch.StartNew();
    var auswahl = string.Join(", ", tabelle.Spalten.Select(s => $"[{s.Name}]"));
    await using var cmd = new SqlCommand($"SELECT {auswahl} FROM [{tabelle.Name}]", sql);
    await using var reader = await cmd.ExecuteReaderAsync();

    var spaltenliste = string.Join(", ", tabelle.Spalten.Select(s => Q(s.Name)));
    await using var import = await pg.BeginBinaryImportAsync($"COPY {Q(tabelle.Name)} ({spaltenliste}) FROM STDIN (FORMAT BINARY)");
    var zeilen = 0;
    while (await reader.ReadAsync())
    {
        await import.StartRowAsync();
        for (var i = 0; i < tabelle.Spalten.Count; i++)
        {
            var wert = reader.GetValue(i);
            if (wert is DBNull)
                await import.WriteNullAsync();
            else
                await import.WriteAsync(Umwandeln(wert, tabelle.Spalten[i].Typ), tabelle.Spalten[i].Typ);
        }
        zeilen++;
    }
    await import.CompleteAsync();
    Console.WriteLine($"  {tabelle.Name,-40} {zeilen,7} Zeilen  {uhr.ElapsedMilliseconds,5} ms");
}

// Typen, die sich zwischen den Datenbanken unterscheiden: tinyint → smallint, datetime2 → timestamptz (UTC, die App
// schreibt DateTime.UtcNow), JSON-Text der PrimitiveCollections → natives PostgreSQL-Array.
static object Umwandeln(object wert, string typ) => typ switch
{
    "int2" => Convert.ToInt16(wert),
    "int4" => Convert.ToInt32(wert),
    "int8" => Convert.ToInt64(wert),
    "numeric" => Convert.ToDecimal(wert),
    "float8" => Convert.ToDouble(wert),
    "timestamptz" => DateTime.SpecifyKind((DateTime)wert, DateTimeKind.Utc),
    "date" => DateOnly.FromDateTime((DateTime)wert),
    "_int4" => JsonSerializer.Deserialize<int[]>((string)wert)!,
    "_text" => JsonSerializer.Deserialize<string[]>((string)wert)!,
    _ => wert,
};

// ── Nacharbeit ────────────────────────────────────────────────────────────

static async Task FolgenNachziehenAsync(NpgsqlConnection pg, List<Tabelle> tabellen)
{
    foreach (var tabelle in tabellen)
    foreach (var spalte in tabelle.Spalten.Where(s => s.Identity))
        await AusfuehrenAsync(pg,
            $"SELECT setval(pg_get_serial_sequence('{Q(tabelle.Name)}', '{spalte.Name}'), " +
            $"COALESCE((SELECT MAX({Q(spalte.Name)}) FROM {Q(tabelle.Name)}), 0) + 1, false)");
}

static async Task KurznamenGrossschreibenAsync(NpgsqlConnection pg)
{
    await using var cmd = new NpgsqlCommand(
        """UPDATE "Verbaende" SET "Kurzname" = upper("Kurzname") WHERE "Kurzname" <> upper("Kurzname") RETURNING "Kurzname" """, pg);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        Console.WriteLine($"  Verbandskürzel großgeschrieben: {reader.GetString(0)}");
}

static async Task<long> ZeilenZaehlenAsync(NpgsqlConnection pg, List<Tabelle> tabellen)
{
    long summe = 0;
    foreach (var tabelle in tabellen)
    {
        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {Q(tabelle.Name)}", pg);
        summe += Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
    return summe;
}

static async Task<int> ZeilenAbgleichenAsync(SqlConnection sql, NpgsqlConnection pg, List<Tabelle> tabellen)
{
    var abweichungen = 0;
    foreach (var tabelle in tabellen)
    {
        await using var sqlCmd = new SqlCommand($"SELECT COUNT_BIG(*) FROM [{tabelle.Name}]", sql);
        await using var pgCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {Q(tabelle.Name)}", pg);
        var a = Convert.ToInt64(await sqlCmd.ExecuteScalarAsync());
        var b = Convert.ToInt64(await pgCmd.ExecuteScalarAsync());
        if (a == b) continue;
        abweichungen++;
        Console.WriteLine($"  ABWEICHUNG {tabelle.Name}: SQL Server {a}, PostgreSQL {b}");
    }
    return abweichungen;
}

static async Task AusfuehrenAsync(NpgsqlConnection pg, string befehl)
{
    await using var cmd = new NpgsqlCommand(befehl, pg);
    await cmd.ExecuteNonQueryAsync();
}

static string Q(string name) => $"\"{name}\"";

record Spalte(string Name, string Typ, bool Identity);
record Tabelle(string Name, List<Spalte> Spalten);
