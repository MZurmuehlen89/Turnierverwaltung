using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fussballturnierverwaltung.Tests.Persistenz;

/// <summary>Verhalten, das nur ein echtes PostgreSQL zeigt (#49): deutsche Sortierung und Umlaut-Suche wie unter
/// SQL Server, Verbindungsprüfung mit ihren Ursachen (AK5, AK8).</summary>
public sealed class PostgresVerhaltenTests(PostgresDatenbank datenbank) : IClassFixture<PostgresDatenbank>
{
    [Fact]
    public async Task Sortierung_UmlauteWieImDeutschen()
    {
        datenbank.VerfuegbarOderUeberspringen();
        await using var context = datenbank.NeuerContext();
        var verband = new Verband { Name = "Sortierprobe", Kurzname = "SORT" };
        context.Verbaende.Add(verband);
        string[] namen = ["Polen", "Oman", "Österreich", "Nordmazedonien", "Nördliche Marianen", "Nordkorea"];
        for (var i = 0; i < namen.Length; i++)
            context.Laender.Add(new Land { Name = namen[i], Kurzname = namen[i][..3], FifaCode = $"S{i}", Verband = verband });
        await context.SaveChangesAsync();

        var reihenfolge = await context.Laender.Where(l => l.Verband.Kurzname == "SORT").OrderBy(l => l.Name)
            .Select(l => l.Name).ToListAsync();

        reihenfolge.Should().Equal("Nordkorea", "Nördliche Marianen", "Nordmazedonien", "Oman", "Österreich", "Polen");
    }

    [Theory]
    [InlineData("öster")]
    [InlineData("ÖSTER")]
    public async Task Mannschaftssuche_FindetUmlauteUnabhaengigVonGrossKlein(string suchbegriff)
    {
        datenbank.VerfuegbarOderUeberspringen();
        await using (var context = datenbank.NeuerContext())
        {
            if (!await context.Mannschaften.AnyAsync(m => m.Name == "Österreich U21"))
            {
                var verband = new Verband { Name = "Suchprobe", Kurzname = "SUCH" };
                var land = new Land { Name = "Österreich (Suche)", Kurzname = "AUT", FifaCode = "AUS", Verband = verband };
                context.Mannschaften.Add(new Mannschaft { Name = "Österreich U21", Kurzname = "AUT21", Land = land });
                await context.SaveChangesAsync();
            }
        }

        await using var lesen = datenbank.NeuerContext();
        var treffer = await new MannschaftRepository(lesen).SuchenAsync(suchbegriff);

        treffer.Select(m => m.Name).Should().Contain("Österreich U21");
    }

    // PostgreSQL (ICU) schreibt „İ“ klein als „i“ + kombinierender Punkt (U+0307), .NET als schlichtes „i“ – der
    // Namensabgleich beim Import fand „İstanbul Başakşehir“ deshalb nicht mehr (GUI-Test ImportExportTests, #49).
    [Theory]
    [InlineData("İstanbul Başakşehir")]
    [InlineData("istanbul başakşehir")]
    public async Task FindenNachName_TuerkischesI_WirdGefunden(string name)
    {
        datenbank.VerfuegbarOderUeberspringen();
        await TuerkischeMannschaftAnlegenAsync();
        await using var lesen = datenbank.NeuerContext();

        var gefunden = await new MannschaftRepository(lesen).FindenNachNameAsync(name);

        gefunden.Should().NotBeNull();
        gefunden!.Name.Should().Be("İstanbul Başakşehir");
    }

    [Theory]
    [InlineData("istanbul")]
    [InlineData("İstanbul")]
    public async Task Mannschaftssuche_TuerkischesI_WirdGefunden(string suchbegriff)
    {
        datenbank.VerfuegbarOderUeberspringen();
        await TuerkischeMannschaftAnlegenAsync();
        await using var lesen = datenbank.NeuerContext();

        (await new MannschaftRepository(lesen).SuchenAsync(suchbegriff)).Select(m => m.Name)
            .Should().Contain("İstanbul Başakşehir");
    }

    private async Task TuerkischeMannschaftAnlegenAsync()
    {
        await using var context = datenbank.NeuerContext();
        if (await context.Mannschaften.AnyAsync(m => m.Kurzname == "IBFK")) return;
        var verband = new Verband { Name = "Türkeiprobe", Kurzname = "TUERK" };
        var land = new Land { Name = "Türkei (Probe)", Kurzname = "TUR", FifaCode = "TRP", Verband = verband };
        context.Mannschaften.Add(new Mannschaft { Name = "İstanbul Başakşehir", Kurzname = "IBFK", Land = land });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Verbindungspruefung_FunktionierendeVerbindung_KeineUrsache()
    {
        datenbank.VerfuegbarOderUeberspringen();

        (await Verbindungspruefung.PruefenAsync(datenbank.Verbindung)).Should().BeNull();
    }

    // AK8: Die App legt eine fehlende Datenbank selbst an – eine funktionierende Anmeldung reicht.
    [Fact]
    public async Task Verbindungspruefung_DatenbankFehlt_AnmeldungKlappt_KeineUrsache()
    {
        datenbank.VerfuegbarOderUeberspringen();
        var ohneDatenbank = new NpgsqlConnectionStringBuilder(datenbank.Grundverbindung) { Database = "gibt_es_nicht_49" };

        (await Verbindungspruefung.PruefenAsync(ohneDatenbank.ConnectionString)).Should().BeNull();
    }

    // AK5
    [Fact]
    public async Task Verbindungspruefung_FalschesKennwort_NenntAnmeldung()
    {
        datenbank.VerfuegbarOderUeberspringen();
        var falsch = new NpgsqlConnectionStringBuilder(datenbank.Verbindung) { Password = "falsch-49" };

        (await Verbindungspruefung.PruefenAsync(falsch.ConnectionString))
            .Should().Be($"Anmeldung als „{falsch.Username}“ abgelehnt: Kennwort falsch oder fehlt.");
    }

    // AK5
    [Fact]
    public async Task Verbindungspruefung_FalscherPort_NenntDienst()
    {
        datenbank.VerfuegbarOderUeberspringen();
        var falsch = new NpgsqlConnectionStringBuilder(datenbank.Verbindung) { Port = 1 };

        (await Verbindungspruefung.PruefenAsync(falsch.ConnectionString))
            .Should().Be($"Keine Verbindung zu {falsch.Host}:1 – läuft der PostgreSQL-Dienst?");
    }
}
