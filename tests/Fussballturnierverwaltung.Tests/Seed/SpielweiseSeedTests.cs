using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Fussballturnierverwaltung.Tests.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Spielweise im Seed (#667, K11): Grundausrichtung aus den Ratings, überschrieben durch die Handliste
/// bekannter Spielweisen; Flexibilität „normal“ außer in der Handliste. Bestehende Datenbanken bekommen dasselbe
/// einmalig per Migration (<see cref="SpielweiseNachruestung"/>).</summary>
public sealed class SpielweiseSeedTests(PostgresDatenbank datenbank) : IClassFixture<PostgresDatenbank>
{
    private static async Task<List<Mannschaft>> GeseedeteMannschaften()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
        return await ctx.Mannschaften.AsNoTracking().ToListAsync();
    }

    private static Mannschaft Team(List<Mannschaft> alle, Mannschaftsart art, string kurzname)
        => alle.Single(m => m.Mannschaftsart == art && m.Kurzname == kurzname);

    [Fact]
    public async Task FrischeDatenbank_HandlisteUeberschreibtDenRatingVorschlag()
    {
        var alle = await GeseedeteMannschaften();

        var barcelona = Team(alle, Mannschaftsart.Verein, "BAR");
        (barcelona.Grundausrichtung, barcelona.Flexibilitaet).Should().Be((Grundausrichtung.SehrOffensiv, Flexibilitaet.Starr));
        var uruguay = Team(alle, Mannschaftsart.Nationalmannschaft, "URU");
        (uruguay.Grundausrichtung, uruguay.Flexibilitaet).Should().Be((Grundausrichtung.Offensiv, Flexibilitaet.Normal));
        var atletico = Team(alle, Mannschaftsart.Verein, "ATM");
        (atletico.Grundausrichtung, atletico.Flexibilitaet).Should().Be((Grundausrichtung.SehrDefensiv, Flexibilitaet.Flexibel),
            "ohne Grundausrichtung in der Handliste gilt der Rating-Vorschlag");
    }

    [Fact]
    public async Task FrischeDatenbank_OhneHandlisteGiltDerRatingVorschlag()
    {
        var alle = await GeseedeteMannschaften();
        var handliste = SpielweiseSeedDaten.Handliste.Select(e => (e.Art, e.Kurzname)).ToHashSet();

        alle.Where(m => !handliste.Contains((m.Mannschaftsart, m.Kurzname))).Should().AllSatisfy(m =>
        {
            m.Grundausrichtung.Should().Be(SpielweisenVorschlag.AusRatings(m.OffensivRating, m.DefensivRating), m.Name);
            m.Flexibilitaet.Should().Be(Flexibilitaet.Normal, m.Name);
        });
        alle.Select(m => m.Grundausrichtung).Distinct().Should().HaveCount(5, "der Bestand deckt alle Stufen ab");
    }

    [Fact]
    public async Task Handliste_JederEintragGibtEsImSeed()
    {
        var alle = (await GeseedeteMannschaften()).Select(m => (m.Mannschaftsart, m.Kurzname)).ToHashSet();

        SpielweiseSeedDaten.Handliste.Where(e => !alle.Contains((e.Art, e.Kurzname))).Should().BeEmpty();
        SpielweiseSeedDaten.Handliste.Select(e => (e.Art, e.Kurzname)).Should().OnlyHaveUniqueItems();
    }

    /// <summary>Die Nachrüstung rechnet in SQL – sie muss dieselben Stufen liefern wie die C#-Funktion und die
    /// Handliste genauso anwenden wie der Seed.</summary>
    [Fact]
    public async Task Nachruestung_BestehendeDatenbank_WieDerSeed()
    {
        datenbank.VerfuegbarOderUeberspringen();
        await using (var ctx = datenbank.NeuerContext())
        {
            var land = new Land
            {
                Name = "Nachrüstland", Kurzname = "NRL", FifaCode = "NRL",
                Verband = new Verband { Name = "Nachrüstverband", Kurzname = "NRV" }
            };
            for (int abweichung = -7; abweichung <= 7; abweichung++)
                ctx.Mannschaften.Add(Neu($"Probe {abweichung}", $"P{abweichung + 10}", Mannschaftsart.Verein, land, 70 + abweichung + 2, 70));
            ctx.Mannschaften.Add(Neu("Barcelona-Probe", "BAR", Mannschaftsart.Verein, land, 88, 84));
            ctx.Mannschaften.Add(Neu("Atlético-Probe", "ATM", Mannschaftsart.Verein, land, 82, 86));
            ctx.Mannschaften.Add(Neu("Barcelona als Nationalteam", "BAR", Mannschaftsart.Nationalmannschaft, land, 88, 84));
            await ctx.SaveChangesAsync();

            await ctx.Database.ExecuteSqlRawAsync(SpielweiseNachruestung.Sql());
        }

        await using var pruefung = datenbank.NeuerContext();
        var alle = await pruefung.Mannschaften.Where(m => m.Land.FifaCode == "NRL").ToListAsync();
        alle.Where(m => m.Name.StartsWith("Probe")).Should().HaveCount(15).And.AllSatisfy(m =>
        {
            m.Grundausrichtung.Should().Be(SpielweisenVorschlag.AusRatings(m.OffensivRating, m.DefensivRating), m.Name);
            m.Flexibilitaet.Should().Be(Flexibilitaet.Normal);
        });
        var barcelona = alle.Single(m => m.Name == "Barcelona-Probe");
        (barcelona.Grundausrichtung, barcelona.Flexibilitaet).Should().Be((Grundausrichtung.SehrOffensiv, Flexibilitaet.Starr));
        var atletico = alle.Single(m => m.Name == "Atlético-Probe");
        (atletico.Grundausrichtung, atletico.Flexibilitaet).Should().Be((Grundausrichtung.SehrDefensiv, Flexibilitaet.Flexibel));
        var falscheArt = alle.Single(m => m.Name == "Barcelona als Nationalteam");
        (falscheArt.Grundausrichtung, falscheArt.Flexibilitaet).Should().Be((Grundausrichtung.Offensiv, Flexibilitaet.Normal),
            "die Handliste gilt nur für die Mannschaftsart ihres Eintrags");
    }

    private static Mannschaft Neu(string name, string kurzname, Mannschaftsart art, Land land, int offensiv, int defensiv) => new()
    {
        Name = name, Kurzname = kurzname, Mannschaftsart = art, Land = land,
        OffensivRating = offensiv, MittelfeldRating = 70, DefensivRating = defensiv, Heimstaerke = 70, Auswaertsstaerke = 60
    };
}
