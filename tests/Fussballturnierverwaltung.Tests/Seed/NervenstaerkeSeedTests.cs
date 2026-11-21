using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Fussballturnierverwaltung.Tests.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Nervenstärke im Seed (#669, K8): die Stufenliste gilt für frische Datenbanken und – einmalig per Migration –
/// für den Bestand (<see cref="NervenstaerkeNachruestung"/>); alle anderen Mannschaften sind „normal“.</summary>
public sealed class NervenstaerkeSeedTests(PostgresDatenbank datenbank) : IClassFixture<PostgresDatenbank>
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
        var liste = NervenstaerkeSeedListe.Handliste.ToDictionary(e => (e.Art, e.Kurzname, e.Land), e => e.Nervenstaerke);

        alle.Should().AllSatisfy(m => m.Nervenstaerke.Should().Be(
            liste.GetValueOrDefault((m.Mannschaftsart, m.Kurzname, m.Land.FifaCode), Nervenstaerke.Normal), m.Name));
        alle.Select(m => m.Nervenstaerke).Distinct().Should().HaveCount(3, "alle drei Stufen kommen vor");
    }

    [Fact]
    public async Task Liste_JederEintragTrifftGenauEineSeedMannschaft_OhneDoppelte()
    {
        var alle = await GeseedeteMannschaften();

        NervenstaerkeSeedListe.Handliste.Select(e => (e.Art, e.Kurzname, e.Land)).Should().OnlyHaveUniqueItems();
        NervenstaerkeSeedListe.Handliste.Should().AllSatisfy(e =>
            alle.Count(m => m.Mannschaftsart == e.Art && m.Kurzname == e.Kurzname && m.Land.FifaCode == e.Land)
                .Should().Be(1, $"{e.Art} {e.Kurzname} {e.Land}"));
        NervenstaerkeSeedListe.Handliste.Should().AllSatisfy(e => e.Nervenstaerke.Should().NotBe(Nervenstaerke.Normal));
    }

    [Fact]
    public async Task SpaeterGeaenderterWert_KommtBeimNaechstenStartNichtZurueck()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
        var eintrag = NervenstaerkeSeedListe.Handliste.First(e => e.Art == Mannschaftsart.Verein && e.Nervenstaerke == Nervenstaerke.Stark);
        var team = await ctx.Mannschaften.SingleAsync(m =>
            m.Mannschaftsart == eintrag.Art && m.Kurzname == eintrag.Kurzname && m.Land.FifaCode == eintrag.Land);
        team.Nervenstaerke = Nervenstaerke.Schwach;
        await ctx.SaveChangesAsync();

        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        ctx.ChangeTracker.Clear();
        (await ctx.Mannschaften.SingleAsync(m => m.Id == team.Id)).Nervenstaerke.Should().Be(Nervenstaerke.Schwach);
    }

    [Fact]
    public async Task Nachruestung_BestehendeDatenbank_WieDerSeed()
    {
        datenbank.VerfuegbarOderUeberspringen();
        var eintrag = NervenstaerkeSeedListe.Handliste.First(e => e.Art == Mannschaftsart.Verein && e.Nervenstaerke == Nervenstaerke.Stark);
        await using (var ctx = datenbank.NeuerContext())
        {
            var verband = new Verband { Name = "Nervenstaerkesverband", Kurzname = "NEV" };
            var land = new Land { Name = "Nervenstaerkesland", Kurzname = eintrag.Land, FifaCode = eintrag.Land, Verband = verband };
            var anderswo = new Land { Name = "Anderswo", Kurzname = "ZZZ", FifaCode = "ZZZ", Verband = verband };
            ctx.Mannschaften.Add(Neu("Treffer", eintrag.Kurzname, Mannschaftsart.Verein, land));
            ctx.Mannschaften.Add(Neu("Falsche Art", eintrag.Kurzname, Mannschaftsart.Nationalmannschaft, land));
            ctx.Mannschaften.Add(Neu("Falsches Land", eintrag.Kurzname, Mannschaftsart.Verein, anderswo));
            ctx.Mannschaften.Add(Neu("Ohne Eintrag", "NEK", Mannschaftsart.Verein, land));
            await ctx.SaveChangesAsync();

            await ctx.Database.ExecuteSqlRawAsync(NervenstaerkeNachruestung.Sql());
        }

        await using var pruefung = datenbank.NeuerContext();
        var alle = await pruefung.Mannschaften.Where(m => m.Land.Verband.Kurzname == "NEV").ToListAsync();
        alle.Single(m => m.Name == "Treffer").Nervenstaerke.Should().Be(Nervenstaerke.Stark);
        alle.Single(m => m.Name == "Falsche Art").Nervenstaerke.Should().Be(Nervenstaerke.Normal,
            "die Liste gilt nur für die Mannschaftsart ihres Eintrags");
        alle.Single(m => m.Name == "Falsches Land").Nervenstaerke.Should().Be(Nervenstaerke.Normal,
            "ein Verein mit gleichem Kurznamen in einem anderen Land ist nicht gemeint");
        alle.Single(m => m.Name == "Ohne Eintrag").Nervenstaerke.Should().Be(Nervenstaerke.Normal);
    }

    private static Mannschaft Neu(string name, string kurzname, Mannschaftsart art, Land land) => new()
    {
        Name = name, Kurzname = kurzname, Mannschaftsart = art, Land = land,
        OffensivRating = 70, MittelfeldRating = 70, DefensivRating = 70, Heimstaerke = 70, Auswaertsstaerke = 60
    };
}
