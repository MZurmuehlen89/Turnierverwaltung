using FluentAssertions;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

public class SeedProtokollTests
{
    private const string FILTER_ERGAENZT = "Turniervorlagen um Verband-/Land-/Mannschaftsartfilter ergänzt.";

    [Fact]
    public async Task InitialisierenAsync_LeereDatenbank_ProtokolliertJedenSchrittDerEtwasAngelegtHat()
    {
        var optionen = NeueDatenbank();
        await using var ctx = new TurnierverwaltungDbContext(optionen);

        var protokoll = await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        protokoll.Select(e => e.Text).Should().Equal(
            "Verbände angelegt.",
            "Länder nachgerüstet.",
            "Verbands-/Länder-Bilder nachgerüstet.",
            "Mannschaften nachgerüstet.",
            "Mannschafts-Wappen nachgerüstet.",
            "RankingRegeln nachgerüstet.",
            "RankingRegelSets nachgerüstet.",
            "Regelwerke nachgerüstet.",
            "Ligapyramiden nachgerüstet.",
            "Rangliste „FIFA-Weltrangliste (Herren)“ nachgerüstet.",
            "Rangliste „UEFA-Klub-Koeffizient (Kassies)“ nachgerüstet.",
            "Rangliste „FIFA-Klub-WM-Ranking CONMEBOL 2021–2024“ nachgerüstet.",
            "Rangliste „FIFA-Klub-WM-Ranking CONCACAF 2021–2024“ nachgerüstet.",
            "Rangliste „FIFA-Klub-WM-Ranking AFC 2021–2024“ nachgerüstet.",
            "Rangliste „FIFA-Klub-WM-Ranking CAF 2021–2024“ nachgerüstet.",
            "Rangliste „FIFA-Klub-WM-Ranking OFC 2021–2024“ nachgerüstet.",
            "neue Turniervorlagen nachgerüstet.",
            "Turniervorlagen um ihr Turnierlogo ergänzt.");
        protokoll.Should().OnlyContain(e => e.Anzahl > 0);
    }

    [Fact]
    public async Task InitialisierenAsync_OhneBildmaterial_LegtAllesAnUndLaesstNurDieBildschritteAus()
    {
        // Wie in der CI und bei einem fremden Klon: das private Bildmaterial fehlt (#633).
        await using var ctx = new TurnierverwaltungDbContext(NeueDatenbank());

        var protokoll = await SeedTestDienst.Erzeugen(ctx, SeedTestDienst.KeineBilder).InitialisierenAsync();

        protokoll.Select(e => e.Text).Should()
            .Contain(["Mannschaften nachgerüstet.", "neue Turniervorlagen nachgerüstet."])
            .And.NotContain([
                "Verbands-/Länder-Bilder nachgerüstet.",
                "Mannschafts-Wappen nachgerüstet.",
                "Turniervorlagen um ihr Turnierlogo ergänzt."]);
        (await ctx.Mannschaften.CountAsync(m => m.Wappen != null)).Should().Be(0);
    }

    [Fact]
    public async Task InitialisierenAsync_VollstaendigeDatenbank_LegtNichtsMehrAn()
    {
        var optionen = NeueDatenbank();
        await ErsterLaufAsync(optionen);

        await using var zweiterLauf = new TurnierverwaltungDbContext(optionen);
        var protokoll = await SeedTestDienst.Erzeugen(zweiterLauf).InitialisierenAsync();

        protokoll.Should().BeEmpty();
    }

    [Fact]
    public async Task InitialisierenAsync_BestandsVorlageOhneFilter_TraegtFilterAusSeedNach()
    {
        var optionen = NeueDatenbank();
        await ErsterLaufAsync(optionen);
        int vorlageId;
        (int? VerbandId, int? LandId, MannschaftsartFilter Art) soll;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            var vorlage = await ctx.TurnierInstanzen.FirstAsync(i => i.Status == TurnierStatus.Vorlage
                && (i.VerbandId != null || i.LandId != null || i.MannschaftsartFilter != MannschaftsartFilter.Alle));
            vorlageId = vorlage.Id;
            soll = (vorlage.VerbandId, vorlage.LandId, vorlage.MannschaftsartFilter);
            vorlage.VerbandId = null;
            vorlage.LandId = null;
            vorlage.MannschaftsartFilter = MannschaftsartFilter.Alle;
            await ctx.SaveChangesAsync();
        }

        await using var zweiterLauf = new TurnierverwaltungDbContext(optionen);
        var protokoll = await SeedTestDienst.Erzeugen(zweiterLauf).InitialisierenAsync();

        protokoll.Select(e => (e.Anzahl, e.Text)).Should().Equal((1, FILTER_ERGAENZT));
        var nachgetragen = await zweiterLauf.TurnierInstanzen.SingleAsync(i => i.Id == vorlageId);
        (nachgetragen.VerbandId, nachgetragen.LandId, nachgetragen.MannschaftsartFilter).Should().Be(soll);
    }

    private static async Task ErsterLaufAsync(DbContextOptions<TurnierverwaltungDbContext> optionen)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
    }

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
