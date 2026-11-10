using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>Seed der Vereinsranglisten außerhalb Europas nach dem FIFA-Klub-WM-Ranking 2021–2024 (Issue #581): je
/// Kontinentalverband eine Liste, am Verband gesetzt.</summary>
public class KlubWmRankingNachruestungTests
{
    private static readonly DateOnly SEED_STAND = new(2025, 6, 13);

    private static string Rangliste(string verband) => $"FIFA-Klub-WM-Ranking {verband} 2021–2024";

    // Lückenlose Ränge 1…n belegen zugleich: jeder Eintrag der Seed-Daten trifft einen Verein des Seeds.
    [Theory]
    [InlineData("CONMEBOL", 67)]
    [InlineData("CONCACAF", 46)]
    [InlineData("AFC", 71)]
    [InlineData("CAF", 30)]
    [InlineData("OFC", 17)]
    public async Task LeereDatenbank_VerbandVerweistAufSeineVollstaendigeListe(string verband, int eintraege)
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var rangliste = await RanglisteLadenAsync(ctx, verband);
        rangliste.Datum.Should().Be(SEED_STAND);
        rangliste.Eintraege.Select(e => e.Rang).Should().BeEquivalentTo(Enumerable.Range(1, eintraege));
        (await ctx.Verbaende.SingleAsync(v => v.Kurzname == verband)).VereinsRanglisteId.Should().Be(rangliste.Id);
    }

    [Fact]
    public async Task LeereDatenbank_StichprobenAusDenListen()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        (await RangUndPunkteAsync(ctx, "OFC", "ACK", "NZL")).Should().Be((1, 66), "Auckland City, neu mit #581");
        (await RangUndPunkteAsync(ctx, "AFC", "HIL", "KSA")).Should().Be((1, 118));
        (await RangUndPunkteAsync(ctx, "CAF", "MSU", "RSA")).Should().Be((4, 98), "Mamelodi Sundowns, neu mit #581");
        (await RangUndPunkteAsync(ctx, "CONMEBOL", "BFR", "BRA")).Should().Be((22, 37), "Botafogo, neu mit #581");
        (await RangUndPunkteAsync(ctx, "CONCACAF", "AME", "MEX")).Should().Be((2, 44), "Club León fehlt in der FIFA-Liste");
    }

    // Die Auslosung verlangt jede Verbandsquote als Mindestzahl (#574); die Teilnehmerauswahl eines Vereinswettbewerbs
    // füllt sie aus der Vereinsrangliste des Verbands. So war die Klub-WM bis #581 gesperrt (kein Verein aus Ozeanien).
    [Fact]
    public async Task JedeVerbandsquoteEinesVereinswettbewerbs_FindetGenugVereineInDerVereinsrangliste()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var quoten = await ctx.TurnierInstanzen
            .Where(t => t.Status == TurnierStatus.Vorlage && t.MannschaftsartFilter == MannschaftsartFilter.Verein)
            .SelectMany(t => t.VerbandsQuoten.Select(q => new
            {
                Vorlage = t.Name + " " + t.Variante,
                q.Verband.Kurzname,
                q.Anzahl,
                Vorhanden = q.Verband.VereinsRangliste == null ? 0 : q.Verband.VereinsRangliste.Eintraege.Count,
            }))
            .ToListAsync();

        quoten.Should().Contain(q => q.Vorlage == "FIFA Klub-WM 32 Teams · ab 2025" && q.Kurzname == "OFC");
        quoten.Where(q => q.Vorhanden < q.Anzahl).Should().BeEmpty();
    }

    // Ein Verband, dem der Nutzer schon eine eigene Vereinsrangliste gegeben hat, behält sie, auch wenn die Seed-Liste
    // neu entsteht.
    [Fact]
    public async Task VerbandMitEigenerVereinsrangliste_BehaeltSie()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        int eigeneId;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            var eigene = new Rangliste { Name = "Eigene CAF-Liste", Datum = new DateOnly(2026, 1, 1) };
            ctx.Ranglisten.Add(eigene);
            var caf = await ctx.Verbaende.SingleAsync(v => v.Kurzname == "CAF");
            caf.VereinsRangliste = eigene;
            ctx.Ranglisten.Remove(await RanglisteLadenAsync(ctx, "CAF"));
            await ctx.SaveChangesAsync();
            eigeneId = eigene.Id;
        }

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Select(p => p.Text).Should().Contain($"Rangliste „{Rangliste("CAF")}“ nachgerüstet.");
        await using var pruef = new TurnierverwaltungDbContext(optionen);
        (await pruef.Verbaende.SingleAsync(v => v.Kurzname == "CAF")).VereinsRanglisteId.Should().Be(eigeneId);
    }

    private static Task<Rangliste> RanglisteLadenAsync(TurnierverwaltungDbContext ctx, string verband) =>
        ctx.Ranglisten.Include(r => r.Eintraege).SingleAsync(r => r.Name == Rangliste(verband));

    private static async Task<(int Rang, int? Punkte)> RangUndPunkteAsync(
        TurnierverwaltungDbContext ctx, string verband, string kurzname, string land)
    {
        var rangliste = await RanglisteLadenAsync(ctx, verband);
        int id = await ctx.Mannschaften
            .Where(m => m.Mannschaftsart == Mannschaftsart.Verein && m.Kurzname == kurzname && m.Land.FifaCode == land)
            .Select(m => m.Id).SingleAsync();
        var eintrag = rangliste.Eintraege.Single(e => e.MannschaftId == id);
        return (eintrag.Rang, eintrag.Punkte);
    }

    private static async Task<IReadOnlyList<SeedProtokollEintrag>> SeedLaufAsync(
        DbContextOptions<TurnierverwaltungDbContext> optionen)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        return await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
    }

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
