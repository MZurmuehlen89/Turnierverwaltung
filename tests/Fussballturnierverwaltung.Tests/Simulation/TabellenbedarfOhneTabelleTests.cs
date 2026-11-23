using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#176 K8: Alles ohne Tabellenlage – freies Einzelspiel und K.-o.-Runde mit Hin- und Rückspiel – rechnet
/// bitgleich wie vor #176 und zieht keinen zusätzlichen Zufall. Die Referenzwerte wurden vor der Änderung auf main
/// (3e78019) erzeugt: je Seed Tore und Ereigniszahl.</summary>
public class TabellenbedarfOhneTabelleTests
{
    private const int FESTER_SEED = 123;

    private static string Einzelspiel(int seed)
    {
        var service = SimulationsDienste.Service(SimulationsDienste.NeuerContext(), new Zufallsquelle(FESTER_SEED));
        var rng = new Random(seed);
        var ergebnis = service.EinzelspielSimulieren(
            SimulationsEngineTestdaten.Team(1, off: 78, mid: 74, def: 70), SimulationsEngineTestdaten.Team(2),
            SimulationsEngineTestdaten.VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen),
            SimulationsModus.AusfuehrlicheSimulation, rng);
        return $"{ergebnis.HeimToreRegulaer}:{ergebnis.AuswaertsToreRegulaer}/{ergebnis.Ereignisse.Count}";
    }

    private static async Task<string> KoRunde(int seed)
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        SimulationsServiceTests.GrunddatenAnlegen(ctx, mannschaften: 4);
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1, Rundenart = Rundenart.KoRunde,
            RegelwerkId = 1, MitRueckspiel = true
        });
        for (int i = 0; i < 2; i++)
        {
            int a = 2 * i + 1, b = 2 * i + 2;
            ctx.Spiele.Add(new Spiel
            {
                Id = a, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = a, Spieltag = 1,
                HeimMannschaftId = a, AuswaertsMannschaftId = b, Status = SpielStatus.Geplant
            });
            ctx.Spiele.Add(new Spiel
            {
                Id = b, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = b, Spieltag = 2,
                HeimMannschaftId = b, AuswaertsMannschaftId = a, Status = SpielStatus.Geplant,
                IstRueckspiel = true, HinspielId = a
            });
        }
        ctx.SaveChanges();

        await SimulationsDienste.Service(ctx, new Zufallsquelle(FESTER_SEED)).RundeSimulierenAsync(1, seed);
        return string.Join(' ', ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Include(s => s.Ereignisse)
            .OrderBy(s => s.Id)
            .Select(s => $"{s.Ergebnis!.HeimToreGesamt}:{s.Ergebnis.AuswaertsToreGesamt}/{s.Ereignisse.Count}"));
    }

    [Theory]
    [InlineData(1, "2:0/11 | 1:1/7 1:1/15 2:2/11 0:3/17")]
    [InlineData(2, "0:1/12 | 1:1/9 5:4/20 2:2/15 2:1/13")]
    [InlineData(3, "1:1/18 | 2:2/19 3:1/15 3:0/17 1:1/12")]
    [InlineData(4, "0:1/13 | 1:1/13 1:1/19 2:0/15 3:1/29")]
    [InlineData(5, "1:1/16 | 2:1/19 2:1/33 1:1/10 1:0/12")]
    [InlineData(6, "1:1/17 | 0:3/17 1:2/20 0:2/12 1:6/16")]
    [InlineData(7, "3:2/13 | 4:4/24 2:0/12 1:1/12 0:0/21")]
    [InlineData(8, "1:1/24 | 3:5/22 0:2/23 3:0/13 1:5/14")]
    [InlineData(9, "2:0/11 | 1:1/5 1:0/16 5:0/20 5:4/30")]
    [InlineData(10, "1:2/18 | 3:7/25 3:0/13 1:0/10 4:0/20")]
    [InlineData(11, "4:0/17 | 1:5/19 1:2/14 0:3/14 5:0/14")]
    [InlineData(12, "1:1/15 | 2:0/10 2:1/11 1:2/12 1:3/18")]
    [InlineData(13, "2:0/7 | 3:0/11 3:3/22 2:2/8 3:0/16")]
    [InlineData(14, "2:2/26 | 1:1/18 2:2/31 2:1/13 1:2/11")]
    [InlineData(15, "0:0/14 | 1:1/12 1:0/13 0:2/18 1:3/19")]
    public async Task OhneTabelle_RechnetBitgleichWieVorDemBedarf(int seed, string erwartet) =>
        $"{Einzelspiel(seed)} | {await KoRunde(seed)}".Should().Be(erwartet);
}
