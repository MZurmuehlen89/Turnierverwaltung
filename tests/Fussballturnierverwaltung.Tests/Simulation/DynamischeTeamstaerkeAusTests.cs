using FluentAssertions;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#359 K1: Ein Turnier ohne „Dynamische Teamstärke“ (auch jeder Bestand) rechnet bitgleich wie vor #359. Die
/// Referenzwerte wurden vor der Änderung auf main (7e73bf9) erzeugt: je Seed Tore und Ereigniszahl aller sechs Spiele einer
/// Liga mit vier Teams, einzeln und als Block simuliert.</summary>
public class DynamischeTeamstaerkeAusTests
{
    private const int FESTER_SEED = 123;

    private static TurnierverwaltungDbContext Turnier()
    {
        var ctx = SimulationsDienste.NeuerContext();
        SimulationsServiceTests.GrunddatenAnlegen(ctx, mannschaften: 4);
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, RegelwerkId = 1
        });
        int nr = 0;
        for (int heim = 1; heim <= 4; heim++)
            for (int gast = heim + 1; gast <= 4; gast++)
            {
                nr++;
                ctx.Spiele.Add(new Spiel
                {
                    Id = nr, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = nr, Spieltag = nr,
                    HeimMannschaftId = heim, AuswaertsMannschaftId = gast, Status = SpielStatus.Geplant
                });
            }
        ctx.SaveChanges();
        return ctx;
    }

    private static string Fingerabdruck(TurnierverwaltungDbContext ctx) =>
        string.Join(' ', ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Include(s => s.Ereignisse)
            .OrderBy(s => s.Id)
            .Select(s => $"{s.Ergebnis!.HeimToreRegulaer}:{s.Ergebnis.AuswaertsToreRegulaer}/{s.Ereignisse.Count}"));

    private static async Task<string> Block(int seed)
    {
        await using var ctx = Turnier();
        await SimulationsDienste.Service(ctx, new Zufallsquelle(FESTER_SEED)).SpieleSimulierenAsync([1, 2, 3, 4, 5, 6], seed);
        return Fingerabdruck(ctx);
    }

    private static async Task<string> Einzeln(int seed)
    {
        await using var ctx = Turnier();
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle(FESTER_SEED));
        var rng = new Random(seed);
        for (int id = 1; id <= 6; id++)
            await service.SpielSimulierenAsync(id, rng);
        return Fingerabdruck(ctx);
    }

    [Theory]
    [InlineData(1, "1:1/7 2:2/13 1:0/10 1:2/16 1:1/15 3:2/22")]
    [InlineData(2, "1:1/9 0:2/11 3:4/18 4:1/13 1:2/18 1:1/8")]
    [InlineData(3, "2:2/19 1:0/18 1:3/15 4:1/17 1:1/17 3:1/22")]
    [InlineData(4, "1:1/13 5:0/15 1:2/12 0:1/9 1:2/20 1:3/14")]
    [InlineData(5, "2:1/19 1:1/10 0:2/12 1:1/6 1:3/13 1:1/13")]
    [InlineData(6, "0:3/17 2:2/13 1:4/19 1:3/17 1:4/16 1:1/12")]
    [InlineData(7, "4:4/24 0:3/16 4:2/16 4:2/25 0:3/17 4:0/16")]
    [InlineData(8, "3:5/22 2:1/16 1:1/14 1:1/8 1:1/8 0:2/14")]
    [InlineData(9, "1:1/5 2:0/15 3:1/19 3:4/28 2:2/16 0:2/12")]
    [InlineData(10, "3:7/25 1:1/11 3:1/13 3:1/22 2:1/8 2:2/19")]
    [InlineData(11, "1:5/19 2:1/15 3:1/12 4:1/17 1:3/10 2:3/18")]
    [InlineData(12, "2:0/10 1:4/14 1:0/12 1:3/11 2:2/17 1:1/15")]
    [InlineData(13, "3:0/11 2:2/10 4:2/18 0:3/16 5:2/25 1:4/18")]
    [InlineData(14, "1:1/18 1:1/11 2:2/16 2:2/11 3:2/18 0:1/12")]
    [InlineData(15, "1:1/12 0:5/20 0:0/9 0:2/10 1:0/7 4:1/22")]
    public async Task Aus_RechnetBitgleichWieVorDerDynamik(int seed, string erwartet)
    {
        (await Block(seed)).Should().Be(erwartet);
        (await Einzeln(seed)).Should().Be(erwartet);
    }
}
