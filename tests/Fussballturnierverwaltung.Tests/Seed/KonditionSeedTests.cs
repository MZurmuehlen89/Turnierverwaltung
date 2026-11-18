using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Fussballturnierverwaltung.Tests.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Kondition im Seed (#677, K10): die Stufenliste gilt für frische Datenbanken und – einmalig per Migration –
/// für den Bestand (<see cref="KonditionNachruestung"/>); alle anderen Mannschaften sind „normal“.</summary>
public sealed class KonditionSeedTests(PostgresDatenbank datenbank) : IClassFixture<PostgresDatenbank>
{
    private static async Task<List<Mannschaft>> GeseedeteMannschaften()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
        return await ctx.Mannschaften.Include(m => m.Land).AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task FrischeDatenbank_ListeGiltUndAlleAnderenSindNormal()
    {
        var alle = await GeseedeteMannschaften();
        var liste = KonditionSeedListe.Handliste.ToDictionary(e => (e.Art, e.Kurzname, e.Land), e => e.Kondition);

        alle.Should().AllSatisfy(m => m.Kondition.Should().Be(
            liste.GetValueOrDefault((m.Mannschaftsart, m.Kurzname, m.Land.FifaCode), Kondition.Normal), m.Name));
        alle.Select(m => m.Kondition).Distinct().Should().HaveCount(3, "alle drei Stufen kommen vor");
    }

    [Fact]
    public async Task Liste_JederEintragTrifftGenauEineSeedMannschaft_OhneDoppelte()
    {
        var alle = await GeseedeteMannschaften();

        KonditionSeedListe.Handliste.Select(e => (e.Art, e.Kurzname, e.Land)).Should().OnlyHaveUniqueItems();
        KonditionSeedListe.Handliste.Should().AllSatisfy(e =>
            alle.Count(m => m.Mannschaftsart == e.Art && m.Kurzname == e.Kurzname && m.Land.FifaCode == e.Land)
                .Should().Be(1, $"{e.Art} {e.Kurzname} {e.Land}"));
        KonditionSeedListe.Handliste.Should().AllSatisfy(e => e.Kondition.Should().NotBe(Kondition.Normal));
    }

    [Fact]
    public async Task Nachruestung_BestehendeDatenbank_WieDerSeed()
    {
        datenbank.VerfuegbarOderUeberspringen();
        var eintrag = KonditionSeedListe.Handliste.First(e => e.Art == Mannschaftsart.Verein && e.Kondition == Kondition.Stark);
        await using (var ctx = datenbank.NeuerContext())
        {
            var verband = new Verband { Name = "Konditionsverband", Kurzname = "KOV" };
            var land = new Land { Name = "Konditionsland", Kurzname = eintrag.Land, FifaCode = eintrag.Land, Verband = verband };
            var anderswo = new Land { Name = "Anderswo", Kurzname = "ZZZ", FifaCode = "ZZZ", Verband = verband };
            ctx.Mannschaften.Add(Neu("Treffer", eintrag.Kurzname, Mannschaftsart.Verein, land));
            ctx.Mannschaften.Add(Neu("Falsche Art", eintrag.Kurzname, Mannschaftsart.Nationalmannschaft, land));
            ctx.Mannschaften.Add(Neu("Falsches Land", eintrag.Kurzname, Mannschaftsart.Verein, anderswo));
            ctx.Mannschaften.Add(Neu("Ohne Eintrag", "KOK", Mannschaftsart.Verein, land));
            await ctx.SaveChangesAsync();

            await ctx.Database.ExecuteSqlRawAsync(KonditionNachruestung.Sql());
        }

        await using var pruefung = datenbank.NeuerContext();
        var alle = await pruefung.Mannschaften.Where(m => m.Land.Verband.Kurzname == "KOV").ToListAsync();
        alle.Single(m => m.Name == "Treffer").Kondition.Should().Be(Kondition.Stark);
        alle.Single(m => m.Name == "Falsche Art").Kondition.Should().Be(Kondition.Normal,
            "die Liste gilt nur für die Mannschaftsart ihres Eintrags");
        alle.Single(m => m.Name == "Falsches Land").Kondition.Should().Be(Kondition.Normal,
            "ein Verein mit gleichem Kurznamen in einem anderen Land ist nicht gemeint");
        alle.Single(m => m.Name == "Ohne Eintrag").Kondition.Should().Be(Kondition.Normal);
    }

    private static Mannschaft Neu(string name, string kurzname, Mannschaftsart art, Land land) => new()
    {
        Name = name, Kurzname = kurzname, Mannschaftsart = art, Land = land,
        OffensivRating = 70, MittelfeldRating = 70, DefensivRating = 70, Heimstaerke = 70, Auswaertsstaerke = 60
    };
}
