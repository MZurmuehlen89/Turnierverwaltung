using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

/// <summary>DFB-Pokal-Vorlage mit Ligapyramide und „Heimrecht für Unterklassige“ (Issue #393) — im Seed und als
/// Nachrüstung bestehender Vorlagen; Turniere bleiben unangetastet.</summary>
public class DfbPokalUnterklassigeNachruestungTests
{
    private const string PYRAMIDE = "Ligapyramide Deutschland 2025/26";
    private static readonly string[] _rundenMitHeimrecht = ["1. Runde", "2. Runde", "Achtelfinale", "Viertelfinale", "Halbfinale"];

    [Fact]
    public async Task LeereDatenbank_VorlageVerweistBisZumHalbfinaleAufDiePyramide()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);

        await using var ctx = new TurnierverwaltungDbContext(optionen);
        var pyramideId = (await ctx.Ranglisten.SingleAsync(r => r.Name == PYRAMIDE)).Id;
        var runden = await DfbRundenAsync(ctx, TurnierStatus.Vorlage);

        runden.Where(r => _rundenMitHeimrecht.Contains(r.Name)).Should().HaveCount(5).And.AllSatisfy(r =>
        {
            r.StandardRanglisteId.Should().Be(pyramideId);
            r.VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtFuerUnterklassige);
        });
        var finale = runden.Single(r => r.Name == "Finale");
        finale.StandardRanglisteId.Should().BeNull();
        finale.VorgegebeneAuslosungsregeln.Should().BeEmpty();
    }

    [Fact]
    public async Task BestehendeVorlageImAltenStand_WirdNachgeruestet_TurnierBleibtUnveraendert()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            // Vorlage auf den Stand vor #393 zurück, dazu ein Turnier im selben alten Stand.
            foreach (var runde in await DfbRundenAsync(ctx, TurnierStatus.Vorlage))
                AltenStandSetzen(runde);
            var turnier = new TurnierInstanz { Name = "DFB-Pokal 2026", Status = TurnierStatus.Geplant };
            turnier.Runden.Add(AltenStandSetzen(new TurnierRunde { Name = "1. Runde", Reihenfolge = 1, Rundenart = Rundenart.KoRunde }));
            ctx.TurnierInstanzen.Add(turnier);
            await ctx.SaveChangesAsync();
        }

        var protokoll = await SeedLaufAsync(optionen);

        protokoll.Select(e => (e.Anzahl, e.Text)).Should().Equal(
            (5, "Vorlagen-Runden um ihre Standard-Rangliste ergänzt."),
            (5, "Vorlagen-Runden auf „Heimrecht für Unterklassige“ umgestellt."));
        await using var pruef = new TurnierverwaltungDbContext(optionen);
        var turnierRunde = (await DfbRundenAsync(pruef, TurnierStatus.Geplant)).Single();
        turnierRunde.StandardRanglisteId.Should().BeNull("Bewegungsdaten werden nicht angefasst");
        turnierRunde.VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtNachLos);
    }

    [Fact]
    public async Task VomNutzerGeaenderteVorgabe_BleibtErhalten()
    {
        var optionen = NeueDatenbank();
        await SeedLaufAsync(optionen);
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            var ersteRunde = (await DfbRundenAsync(ctx, TurnierStatus.Vorlage)).Single(r => r.Name == "1. Runde");
            ersteRunde.VorgegebeneAuslosungsregeln = [AuslosungsRegelArt.HeimrechtNachSetzliste];
            await ctx.SaveChangesAsync();
        }

        (await SeedLaufAsync(optionen)).Should().BeEmpty();

        await using var pruef = new TurnierverwaltungDbContext(optionen);
        (await DfbRundenAsync(pruef, TurnierStatus.Vorlage)).Single(r => r.Name == "1. Runde")
            .VorgegebeneAuslosungsregeln.Should().Equal(AuslosungsRegelArt.HeimrechtNachSetzliste);
    }

    private static TurnierRunde AltenStandSetzen(TurnierRunde runde)
    {
        runde.StandardRanglisteId = null;
        if (runde.Rundenart == Rundenart.KoRunde)
            runde.VorgegebeneAuslosungsregeln = [AuslosungsRegelArt.HeimrechtNachLos];
        return runde;
    }

    private static Task<List<TurnierRunde>> DfbRundenAsync(TurnierverwaltungDbContext ctx, TurnierStatus status) =>
        ctx.TurnierRunden
            .Where(r => r.TurnierInstanz.Name.StartsWith("DFB-Pokal") && r.TurnierInstanz.Status == status)
            .OrderBy(r => r.Reihenfolge)
            .ToListAsync();

    private static async Task<IReadOnlyList<SeedProtokollEintrag>> SeedLaufAsync(
        DbContextOptions<TurnierverwaltungDbContext> optionen)
    {
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        return await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
    }

    private static DbContextOptions<TurnierverwaltungDbContext> NeueDatenbank() =>
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
