using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Issue #355 K11: Vorlagen „Österreichische Bundesliga“ (Meister- und Qualifikationsgruppe, halbiert abgerundet) und
/// „Belgische Pro League“ (Play-offs halbiert aufgerundet, Abstiegs-Play-offs mit allen Punkten, Kriterium
/// <see cref="RankingRegelArt.PlatzVorrunde"/>) samt Nachrüsten im Bestand.</summary>
public class PunkteuebernahmeSeedTests
{
    private const string OESTERREICH = "Österreichische Bundesliga";
    private const string BELGIEN = "Belgische Pro League";

    private static readonly List<TurnierInstanz> _vorlagen = TurnierVorlagenSeedDaten.Erstellen();

    private static TurnierInstanz Vorlage(string name) => _vorlagen.Single(v => v.Name == name);

    private static Dictionary<string, Punktevortrag> Modi(string name) =>
        Vorlage(name).Runden.ToDictionary(r => r.Name, r => r.Punktevortrag);

    // ── Seed-Stand ─────────────────────────────────────────────────────────────

    [Fact]
    public void Oesterreich_GrunddurchgangDannMeisterUndQualifikationsgruppeMitHalbiertenPunkten()
    {
        var vorlage = Vorlage(OESTERREICH);

        vorlage.Runden.OrderBy(r => r.Reihenfolge).Select(r => (r.Name, r.EingangsMannschaftsanzahl))
            .Should().Equal(("Grunddurchgang", 12), ("Meistergruppe", 6), ("Qualifikationsgruppe", 6));
        Modi(OESTERREICH).Should().Equal(new Dictionary<string, Punktevortrag>
        {
            ["Grunddurchgang"] = Punktevortrag.Keine,
            ["Meistergruppe"] = Punktevortrag.HalbiertAbgerundet,
            ["Qualifikationsgruppe"] = Punktevortrag.HalbiertAbgerundet
        });
        vorlage.Runden.Should().OnlyContain(r => r.MitRueckspiel);
    }

    [Fact]
    public void Belgien_PlayOffsHalbiertAufgerundet_AbstiegsPlayOffsMitAllenPunkten()
    {
        var vorlage = Vorlage(BELGIEN);

        vorlage.Runden.OrderBy(r => r.Reihenfolge).Select(r => (r.Name, r.EingangsMannschaftsanzahl))
            .Should().Equal(("Grunddurchgang", 16), ("Champions-Play-offs", 6), ("Europe-Play-offs", 6), ("Abstiegs-Play-offs", 4));
        Modi(BELGIEN).Should().Equal(new Dictionary<string, Punktevortrag>
        {
            ["Grunddurchgang"] = Punktevortrag.Keine,
            ["Champions-Play-offs"] = Punktevortrag.HalbiertAufgerundet,
            ["Europe-Play-offs"] = Punktevortrag.HalbiertAufgerundet,
            ["Abstiegs-Play-offs"] = Punktevortrag.Voll
        });
    }

    [Theory]
    [InlineData(OESTERREICH, "Meistergruppe", 1, 6)]
    [InlineData(OESTERREICH, "Qualifikationsgruppe", 7, 12)]
    [InlineData(BELGIEN, "Champions-Play-offs", 1, 6)]
    [InlineData(BELGIEN, "Europe-Play-offs", 7, 12)]
    [InlineData(BELGIEN, "Abstiegs-Play-offs", 13, 16)]
    public void Folgerunden_WerdenAusDenPlaetzenDesGrunddurchgangsGespeist(string vorlage, string runde, int von, int bis)
    {
        var regel = Vorlage(vorlage).Runden.Single(r => r.Name == "Grunddurchgang").Uebergangsregeln
            .Single(u => u.ZielRunde.Name == runde);

        (regel.PlatzierungVon, regel.PlatzierungBis).Should().Be((von, bis));
    }

    // ── Regelwerk und Kriterium ────────────────────────────────────────────────

    [Fact]
    public void Regelset_OrdnetPunkteDannPlatzInDerVorrunde()
    {
        var (sets, eintraege) = RegelwerkeSeedDaten.RankingRegelSetsErstellen();

        sets.Should().Contain(s => s.Name == RegelwerkeSeedDaten.PLAYOFFS_PUNKTEUEBERNAHME_SET);
        eintraege.Where(e => e.SetName == RegelwerkeSeedDaten.PLAYOFFS_PUNKTEUEBERNAHME_SET)
            .OrderBy(e => e.Reihenfolge).Select(e => e.Regelart)
            .Should().Equal(RankingRegelArt.Punkte, RankingRegelArt.PlatzVorrunde, RankingRegelArt.Losentscheid);
    }

    [Fact]
    public void RankingRegel_PlatzVorrundeSortiertAufsteigend()
    {
        RegelwerkeSeedDaten.RankingRegelnErstellen().Single(r => r.Regelart == RankingRegelArt.PlatzVorrunde)
            .Sortierrichtung.Should().Be(Sortierrichtung.Aufsteigend);
    }

    // ── Datenbank: frisch und nachgerüstet ─────────────────────────────────────

    private static TurnierverwaltungDbContext NeuerContext(string datenbank) =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>().UseInMemoryDatabase(datenbank).Options);

    [Fact]
    public async Task Initialisieren_LegtRegelwerkSetUndVorlagenAnUndVerknuepftDiePlayOffs()
    {
        await using var ctx = NeuerContext(Guid.NewGuid().ToString());

        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        var regelwerk = await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.LIGA_PLAYOFFS_PUNKTEUEBERNAHME);
        var belgien = await ctx.TurnierInstanzen.Include(i => i.Runden)
            .SingleAsync(i => i.Name == BELGIEN && i.Status == TurnierStatus.Vorlage);
        belgien.Runden.Where(r => r.Name != "Grunddurchgang").Should().OnlyContain(r => r.RegelwerkId == regelwerk.Id);
        belgien.Runden.Single(r => r.Name == "Champions-Play-offs").Punktevortrag.Should().Be(Punktevortrag.HalbiertAufgerundet);
        (await ctx.TurnierInstanzen.AnyAsync(i => i.Name == OESTERREICH && i.Status == TurnierStatus.Vorlage)).Should().BeTrue();
    }

    [Fact]
    public async Task Initialisieren_RuestetFehlendeVorlagenKriteriumUndRegelsetImBestandNach()
    {
        string datenbank = Guid.NewGuid().ToString();
        await using (var ctx = NeuerContext(datenbank))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
            // Bestand vor #355: weder die beiden Vorlagen noch Regelwerk, Regelset und Kriterium sind da.
            var vorlagen = await ctx.TurnierInstanzen.Include(i => i.Runden).ThenInclude(r => r.TabellenBereiche)
                .Where(i => i.Name == OESTERREICH || i.Name == BELGIEN).ToListAsync();
            var rundenIds = vorlagen.SelectMany(i => i.Runden).Select(r => r.Id).ToList();
            ctx.Uebergangsregeln.RemoveRange(ctx.Uebergangsregeln.Where(u => rundenIds.Contains(u.QuellRundeId)));
            ctx.TurnierInstanzen.RemoveRange(vorlagen);
            ctx.Regelwerke.RemoveRange(ctx.Regelwerke.Where(r => r.Name == TurnierVorlagenSeedDaten.LIGA_PLAYOFFS_PUNKTEUEBERNAHME));
            ctx.RankingRegelSets.RemoveRange(ctx.RankingRegelSets.Include(s => s.Eintraege)
                .Where(s => s.Name == RegelwerkeSeedDaten.PLAYOFFS_PUNKTEUEBERNAHME_SET));
            ctx.RankingRegeln.RemoveRange(ctx.RankingRegeln.Where(r => r.Regelart == RankingRegelArt.PlatzVorrunde));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NeuerContext(datenbank))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            (await ctx.RankingRegeln.CountAsync(r => r.Regelart == RankingRegelArt.PlatzVorrunde)).Should().Be(1);
            var set = await ctx.RankingRegelSets.Include(s => s.Eintraege).ThenInclude(e => e.RankingRegel)
                .SingleAsync(s => s.Name == RegelwerkeSeedDaten.PLAYOFFS_PUNKTEUEBERNAHME_SET);
            set.Eintraege.OrderBy(e => e.Reihenfolge).Select(e => e.RankingRegel.Regelart)
                .Should().Equal(RankingRegelArt.Punkte, RankingRegelArt.PlatzVorrunde, RankingRegelArt.Losentscheid);
            var regelwerk = await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.LIGA_PLAYOFFS_PUNKTEUEBERNAHME);
            regelwerk.RankingRegelSetId.Should().Be(set.Id);
            var belgien = await ctx.TurnierInstanzen.Include(i => i.Runden)
                .SingleAsync(i => i.Name == BELGIEN && i.Status == TurnierStatus.Vorlage);
            belgien.Runden.Single(r => r.Name == "Abstiegs-Play-offs").RegelwerkId.Should().Be(regelwerk.Id);
            (await ctx.TurnierInstanzen.CountAsync(i => i.Name == OESTERREICH && i.Status == TurnierStatus.Vorlage)).Should().Be(1);
        }
    }
}
