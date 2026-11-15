using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Wartung;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Fussballturnierverwaltung.Tests.Persistenz;

/// <summary>Sichern und Wiederherstellen gegen ein echtes PostgreSQL mit pg_dump/pg_restore (#43, AK1–AK9). Jeder Test
/// legt seine eigenen Verbände an und stellt nur seine eigene Sicherung wieder her – die Reihenfolge ist also egal.</summary>
public sealed class DatenbankSicherungTests(PostgresDatenbank datenbank) : IClassFixture<PostgresDatenbank>, IDisposable
{
    private static readonly string[] _werkzeugnamen = ["pg_dump", "pg_restore"];

    private readonly string _ordner = Path.Combine(Path.GetTempPath(), $"tv_sicherungstest_{Guid.NewGuid():N}");

    // ── AK1–AK3 ────────────────────────────────────────

    [Fact]
    public async Task SichernUndWiederherstellen_StelltGenauDenGesichertenStandHer()
    {
        BereitOderUeberspringen();
        var (a, b) = (Kuerzel(), Kuerzel());
        await VerbaendeAnlegenAsync(a, b);
        var hilfsdatenbankenVorher = await HilfsdatenbankenAsync();

        var sicherung = await NeuerDienst().SichernAsync(_ordner);

        File.Exists(sicherung.Pfad).Should().BeTrue();
        sicherung.Name.Should().MatchRegex($"^{datenbank.Name}_\\d{{4}}-\\d{{2}}-\\d{{2}}_\\d{{2}}-\\d{{2}}-\\d{{2}}\\.dump$");
        DatenbankSicherungService.Auflisten(_ordner).Select(s => s.Pfad).Should().Equal(sicherung.Pfad);

        // Ändern, löschen, neu anlegen – und dabei Verbindungen im Pool der App hinterlassen (AK3)
        var c = Kuerzel();
        await using (var context = datenbank.NeuerContext())
        {
            (await context.Verbaende.SingleAsync(v => v.Kurzname == a)).Name = "geändert";
            context.Verbaende.Remove(await context.Verbaende.SingleAsync(v => v.Kurzname == b));
            context.Verbaende.Add(new Verband { Name = "danach", Kurzname = c });
            await context.SaveChangesAsync();
        }

        await NeuerDienst().WiederherstellenAsync(sicherung.Pfad);

        (await VerbandsnamenAsync(a, b, c)).Should().Equal($"Verband {a}", $"Verband {b}");
        (await HilfsdatenbankenAsync()).Should().Be(hilfsdatenbankenVorher, "die Hilfsdatenbank heißt jetzt wie die alte");
    }

    // ── AK5–AK9: abgelehnt, Datenbank unverändert ──────

    [Fact]
    public async Task Wiederherstellen_DefekteDatei_LaesstDatenbankUnveraendert()
    {
        BereitOderUeberspringen();
        var datei = Path.Combine(Directory.CreateDirectory(_ordner).FullName, "defekt.dump");
        await File.WriteAllTextAsync(datei, "keine Sicherung");

        var meldung = await AbgelehntOhneAenderungAsync(datei, "ließ sich nicht einspielen");

        meldung.Should().NotContain("�", "die Meldung von pg_restore kommt in der Kodierung des Werkzeugs an");
    }

    [Fact]
    public async Task Wiederherstellen_SicherungEinerFremdenDatenbank_WirdAbgelehnt()
    {
        BereitOderUeberspringen();
        var fremd = $"tv_fremd_{Guid.NewGuid():N}";
        await VerwaltenAsync($"CREATE DATABASE \"{fremd}\"");
        string datei;
        try
        {
            await using (var verbindung = new NpgsqlConnection(Verbindung(fremd)))
            {
                await verbindung.OpenAsync();
                await new NpgsqlCommand("CREATE TABLE notizen (text varchar(20)); INSERT INTO notizen VALUES ('fremd')", verbindung)
                    .ExecuteNonQueryAsync();
            }
            datei = await FremdeSicherungAsync(fremd);
        }
        finally
        {
            await VerwaltenAsync($"DROP DATABASE IF EXISTS \"{fremd}\" WITH (FORCE)");
        }

        await AbgelehntOhneAenderungAsync(datei, "keine Sicherung der Turnierverwaltung");
    }

    [Fact]
    public async Task Wiederherstellen_SicherungAusNeuererVersion_WirdAbgelehnt()
    {
        BereitOderUeberspringen();
        const string ZUKUNFT = "29991231000000_AusDerZukunft";
        await AusfuehrenAsync($"INSERT INTO \"__EFMigrationsHistory\" VALUES ('{ZUKUNFT}', '99.0')");
        Sicherung sicherung;
        try
        {
            sicherung = await NeuerDienst().SichernAsync(_ordner);
        }
        finally
        {
            await AusfuehrenAsync($"DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{ZUKUNFT}'");
        }

        await AbgelehntOhneAenderungAsync(sicherung.Pfad, $"neueren Version der Turnierverwaltung (unbekannte Migration {ZUKUNFT})");
    }

    [Fact]
    public async Task Wiederherstellen_BeiOffenerFremderVerbindung_WirdAbgelehnt()
    {
        BereitOderUeberspringen();
        var sicherung = await NeuerDienst().SichernAsync(_ordner);
        await using var fremd = new NpgsqlConnection(Verbindung(datenbank.Name));
        await fremd.OpenAsync();

        await AbgelehntOhneAenderungAsync(sicherung.Pfad, "noch in einem anderen Programm geöffnet");
    }

    // ── AK8: ältere Sicherung ──────────────────────────

    [Fact]
    public async Task Wiederherstellen_SicherungAusAeltererVersion_WirdBeimStartMigriert()
    {
        BereitOderUeberspringen();
        Sicherung sicherung;
        await using (var context = datenbank.NeuerContext())
        {
            // Mit nur einer Migration ist „älter“ der Stand vor ihr; der Startpfad muss sie einspielen. Ihr Down lässt
            // die Sortierfolge stehen, die eine Datenbank vor der ersten Migration noch nicht hatte.
            await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
            await context.Database.ExecuteSqlRawAsync("DROP COLLATION IF EXISTS deutsch");
            sicherung = await new DatenbankSicherungService(context, PostgresWerkzeuge.Standard(),
                NullLogger<DatenbankSicherungService>.Instance).SichernAsync(_ordner);
            await context.Database.MigrateAsync();
        }

        await NeuerDienst().WiederherstellenAsync(sicherung.Pfad);

        await using var danach = datenbank.NeuerContext();
        (await danach.Database.GetPendingMigrationsAsync()).Should().NotBeEmpty("die Sicherung ist älter als die App");
        await danach.Database.MigrateAsync();
        (await danach.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await danach.Verbaende.CountAsync()).Should().Be(0);
    }

    // ── Hilfen ─────────────────────────────────────────

    private async Task<string> AbgelehntOhneAenderungAsync(string datei, string meldungsteil)
    {
        var a = Kuerzel();
        await VerbaendeAnlegenAsync(a);
        var hilfsdatenbankenVorher = await HilfsdatenbankenAsync();

        var wiederherstellen = () => NeuerDienst().WiederherstellenAsync(datei);

        var meldung = (await wiederherstellen.Should().ThrowAsync<DatenbankSicherungException>()).Which.Message;
        meldung.Should().Contain(meldungsteil);
        (await VerbandsnamenAsync(a)).Should().Equal($"Verband {a}");
        (await HilfsdatenbankenAsync()).Should().Be(hilfsdatenbankenVorher, "die Hilfsdatenbank wird verworfen");
        return meldung;
    }

    private void BereitOderUeberspringen()
    {
        datenbank.VerfuegbarOderUeberspringen();
        var fehlt = _werkzeugnamen.Where(p => PostgresWerkzeuge.Standard().Finden(p) is null).ToList();
        if (fehlt.Count == 0) return;
        // In der CI müssen die Werkzeuge da sein, lokal fehlen sie nur ohne PostgreSQL-Installation.
        Assert.True(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresDatenbank.VARIABLE)),
            $"{string.Join(", ", fehlt)} nicht gefunden, obwohl {PostgresDatenbank.VARIABLE} gesetzt ist.");
        Assert.Skip($"{string.Join(", ", fehlt)} nicht gefunden.");
    }

    private DatenbankSicherungService NeuerDienst() =>
        new(datenbank.NeuerContext(), PostgresWerkzeuge.Standard(), NullLogger<DatenbankSicherungService>.Instance);

    private static string Kuerzel() => "S" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task VerbaendeAnlegenAsync(params string[] kuerzel)
    {
        await using var context = datenbank.NeuerContext();
        context.Verbaende.AddRange(kuerzel.Select(k => new Verband { Name = $"Verband {k}", Kurzname = k }));
        await context.SaveChangesAsync();
    }

    private async Task<List<string>> VerbandsnamenAsync(params string[] kuerzel)
    {
        await using var context = datenbank.NeuerContext();
        return await context.Verbaende.Where(v => kuerzel.Contains(v.Kurzname)).OrderBy(v => v.Kurzname == kuerzel[0] ? 0 : 1)
            .Select(v => v.Name).ToListAsync();
    }

    private async Task<long> HilfsdatenbankenAsync()
    {
        await using var verbindung = new NpgsqlConnection(Verbindung("postgres"));
        await verbindung.OpenAsync();
        await using var befehl = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_database WHERE datname LIKE 'tv\\_wiederherstellung\\_%' OR datname LIKE 'tv\\_vor\\_wiederherstellung\\_%'",
            verbindung);
        return (long)(await befehl.ExecuteScalarAsync())!;
    }

    private async Task<string> FremdeSicherungAsync(string fremd)
    {
        var ziel = new NpgsqlConnectionStringBuilder(Verbindung(fremd));
        var datei = Path.Combine(Directory.CreateDirectory(_ordner).FullName, "fremd.dump");
        var fehler = await WerkzeugAufruf.AusfuehrenAsync(WerkzeugAufruf.Erstellen(
            PostgresWerkzeuge.Standard().Finden("pg_dump")!, ziel, ["-Fc", "-f", datei, fremd]), CancellationToken.None);
        fehler.Should().BeNull();
        return datei;
    }

    private string Verbindung(string name) =>
        new NpgsqlConnectionStringBuilder(datenbank.Grundverbindung) { Database = name, Pooling = false }.ConnectionString;

    private async Task VerwaltenAsync(string sql)
    {
        await using var verbindung = new NpgsqlConnection(Verbindung("postgres"));
        await verbindung.OpenAsync();
        await new NpgsqlCommand(sql, verbindung).ExecuteNonQueryAsync();
    }

    private async Task AusfuehrenAsync(string sql)
    {
        await using var verbindung = new NpgsqlConnection(Verbindung(datenbank.Name));
        await verbindung.OpenAsync();
        await new NpgsqlCommand(sql, verbindung).ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_ordner))
            Directory.Delete(_ordner, recursive: true);
    }
}
