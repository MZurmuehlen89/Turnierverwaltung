using FluentAssertions;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Wartung;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Fussballturnierverwaltung.Tests.Persistenz;

/// <summary>Was an der Sicherung (#43) ohne PostgreSQL prüfbar ist: Werkzeugsuche, Aufruf mit Kennwort, Dateiname,
/// Liste der Sicherungen und die Meldung bei fehlenden Werkzeugen (AK10–AK12).</summary>
public sealed class SicherungsWerkzeugeTests : IDisposable
{
    private readonly string _ordner = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"tv_werkzeugtest_{Guid.NewGuid():N}")).FullName;

    // ── Werkzeugsuche (AK10) ───────────────────────────

    [Fact]
    public void BinNachVersion_HoechsteVersionZuerst_OrdnerOhneVersionEntfallen()
    {
        var basis = Path.Combine(_ordner, "PostgreSQL");
        string[] ordner = ["9.6", "18", "pgAdmin 4", "17"];

        var bin = PostgresWerkzeuge.BinNachVersion(ordner.Select(o => Path.Combine(basis, o)));

        bin.Should().Equal(
            Path.Combine(basis, "18", "bin"), Path.Combine(basis, "17", "bin"), Path.Combine(basis, "9.6", "bin"));
    }

    [Fact]
    public void Finden_NimmtDasErsteVerzeichnisMitDemProgramm()
    {
        var leer = Directory.CreateDirectory(Path.Combine(_ordner, "leer")).FullName;
        var erstes = Directory.CreateDirectory(Path.Combine(_ordner, "erstes")).FullName;
        var zweites = Directory.CreateDirectory(Path.Combine(_ordner, "zweites")).FullName;
        var datei = OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump";
        File.WriteAllText(Path.Combine(erstes, datei), "");
        File.WriteAllText(Path.Combine(zweites, datei), "");

        var werkzeuge = new PostgresWerkzeuge([leer, erstes, zweites]);

        werkzeuge.Finden("pg_dump").Should().Be(Path.Combine(erstes, datei));
        werkzeuge.Finden("pg_restore").Should().BeNull();
    }

    [Fact]
    public async Task FehlendeWerkzeuge_VerstaendlicheMeldungStattAbsturz()
    {
        await using var context = new TurnierverwaltungDbContext(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseNpgsql("Host=localhost;Database=gibtsnicht;Username=niemand").Options);
        var dienst = new DatenbankSicherungService(context, new PostgresWerkzeuge([]), NullLogger<DatenbankSicherungService>.Instance);
        var datei = Path.Combine(_ordner, "irgendeine.dump");
        File.WriteAllText(datei, "");

        var sichern = () => dienst.SichernAsync(_ordner);
        var wiederherstellen = () => dienst.WiederherstellenAsync(datei);

        (await sichern.Should().ThrowAsync<DatenbankSicherungException>()).Which.Message
            .Should().Contain("pg_dump").And.Contain("installieren");
        (await wiederherstellen.Should().ThrowAsync<DatenbankSicherungException>()).Which.Message
            .Should().Contain("pg_restore");
    }

    // ── Aufruf (AK11) ──────────────────────────────────

    [Fact]
    public void Aufruf_KennwortAusDerApp_GehtPerUmgebungsvariableNichtAufDieBefehlszeile()
    {
        var ziel = new NpgsqlConnectionStringBuilder("Host=server;Port=5433;Username=nutzer;Password=geheim");

        var start = WerkzeugAufruf.Erstellen("pg_dump", ziel, ["-Fc"]);

        start.ArgumentList.Should().Equal("-h", "server", "-p", "5433", "-U", "nutzer", "-w", "-Fc");
        start.Environment[WerkzeugAufruf.KENNWORT_VARIABLE].Should().Be("geheim");
    }

    [Fact]
    public void Aufruf_OhneKennwort_SetztKeinesUndLaesstDieKennwortdateiGelten()
    {
        var ziel = new NpgsqlConnectionStringBuilder("Host=server;Username=nutzer");

        var start = WerkzeugAufruf.Erstellen("pg_dump", ziel, []);

        start.Environment.TryGetValue(WerkzeugAufruf.KENNWORT_VARIABLE, out var wert);
        wert.Should().Be(Environment.GetEnvironmentVariable(WerkzeugAufruf.KENNWORT_VARIABLE), "nur der geerbte Wert, kein eigener");
    }

    // ── Dateiname und Liste (AK1, AK12) ────────────────

    [Fact]
    public void Dateiname_DatenbankUndZeitstempel()
    {
        DatenbankSicherungService.Dateiname("turnierverwaltung", new DateTime(2026, 11, 15, 18, 3, 9, DateTimeKind.Local))
            .Should().Be("turnierverwaltung_2026-11-15_18-03-09.dump");
    }

    [Fact]
    public void Dateiname_ZeichenDieImDateinamenNichtGehen_WerdenErsetzt()
    {
        var ungueltig = Path.GetInvalidFileNameChars()[^1];

        DatenbankSicherungService.Dateiname($"a{ungueltig}b", new DateTime(2026, 11, 15, 18, 3, 9, DateTimeKind.Local))
            .Should().Be("a_b_2026-11-15_18-03-09.dump");
    }

    [Fact]
    public void Auflisten_OrdnerFehlt_KeineSicherung()
    {
        DatenbankSicherungService.Auflisten(Path.Combine(_ordner, "gibtsnicht")).Should().BeEmpty();
    }

    [Fact]
    public void Auflisten_NurSicherungen_NeuesteZuerst()
    {
        var alt = Anlegen("alt.dump", new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Local));
        var neu = Anlegen("neu.dump", new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Local));
        Anlegen("notiz.txt", new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Local));

        var liste = DatenbankSicherungService.Auflisten(_ordner);

        liste.Select(s => s.Name).Should().Equal(neu, alt);
        liste[0].Zeitpunkt.Should().Be(new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Local));
        liste[0].Groesse.Should().Be(3);
    }

    private string Anlegen(string name, DateTime geaendert)
    {
        var pfad = Path.Combine(_ordner, name);
        File.WriteAllText(pfad, "abc");
        File.SetLastWriteTime(pfad, geaendert);
        return name;
    }

    public void Dispose() => Directory.Delete(_ordner, recursive: true);
}
