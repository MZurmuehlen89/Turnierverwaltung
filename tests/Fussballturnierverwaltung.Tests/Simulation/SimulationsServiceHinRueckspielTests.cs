using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Issue #346: Simulation von K.-o.-Paarungen mit Hin- und Rückspiel — das Hinspiel kennt kein
/// Entscheidungsverfahren, das Rückspiel nur bei ausgeglichenem Gesamtergebnis.</summary>
public class SimulationsServiceHinRueckspielTests
{
    private const int PAARUNGEN = 30;

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SimulationsService ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var instanzRepo = new TurnierInstanzRepository(ctx);
        var spielplanService = new SpielplanService(
            rundeRepo, spielRepo, new MannschaftRepository(ctx), new TurnierBaumRepository(ctx),
            new UebergangsregelRepository(ctx),
            new SpielplanGeneratorFactory([new KoRundeGenerator(), new RoundRobinGenerator()]),
            new TurnierValidator(), new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)),
            new AuslosungRepository(ctx), NullLogger<SpielplanService>.Instance);
        var ergebnisService = new SpielErgebnisService(
            spielRepo, new SpielErgebnisValidator(), spielplanService, NullLogger<SpielErgebnisService>.Instance);
        var baumService = new TurnierBaumService(
            new TurnierBaumRepository(ctx), instanzRepo, spielRepo, spielplanService, NullLogger<TurnierBaumService>.Instance);
        return new SimulationsService(
            spielRepo, instanzRepo, ergebnisService, baumService,
            new SimulationsLaufRepository(ctx), NullLogger<SimulationsService>.Instance);
    }

    /// <summary>K.-o.-Runde mit Rückspiel und Elfmeterschießen als einzigem Entscheidungsverfahren;
    /// <see cref="PAARUNGEN"/> Paarungen gleich starker Teams (viele ausgeglichene Gesamtergebnisse).</summary>
    private static void SetupAnlegen(TurnierverwaltungDbContext ctx)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Achtelfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, RegelwerkId = 1, MitRueckspiel = true
        });

        for (int i = 0; i < PAARUNGEN; i++)
        {
            int teamA = 2 * i + 1, teamB = 2 * i + 2;
            foreach (var id in new[] { teamA, teamB })
                ctx.Mannschaften.Add(new Mannschaft
                {
                    Id = id, Name = $"Team {id}", Kurzname = $"T{id}", LandId = 1,
                    OffensivRating = 70, MittelfeldRating = 70, DefensivRating = 70,
                    Heimstaerke = 100, Auswaertsstaerke = 100
                });

            int hinId = 2 * i + 1;
            ctx.Spiele.Add(new Spiel
            {
                Id = hinId, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = hinId, Spieltag = 1,
                HeimMannschaftId = teamA, AuswaertsMannschaftId = teamB, Status = SpielStatus.Geplant
            });
            ctx.Spiele.Add(new Spiel
            {
                Id = hinId + 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = hinId + 1, Spieltag = 2,
                HeimMannschaftId = teamB, AuswaertsMannschaftId = teamA, Status = SpielStatus.Geplant,
                IstRueckspiel = true, HinspielId = hinId
            });
        }
        ctx.SaveChanges();
    }

    [Fact]
    public async Task RundeSimulierenAsync_HinspielOhneVerfahren_RueckspielEntscheidetGesamtergebnis()
    {
        await using var ctx = NeuerContext();
        SetupAnlegen(ctx);
        var service = ServiceFuer(ctx);

        await service.RundeSimulierenAsync(1, SimulationsModus.SchnellSimulation, seed: 346);

        var spiele = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.TurnierRundeId == 1).ToList();
        spiele.Should().OnlyContain(s => s.Ergebnis != null);

        foreach (var hin in spiele.Where(s => !s.IstRueckspiel))
        {
            var rueck = spiele.Single(s => s.HinspielId == hin.Id);
            var h = hin.Ergebnis!;
            var r = rueck.Ergebnis!;

            h.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit, "das Hinspiel hat kein Entscheidungsverfahren");
            h.HeimElfmetertore.Should().BeNull();

            int teamA = h.HeimToreRegulaer + r.AuswaertsToreRegulaer;
            int teamB = h.AuswaertsToreRegulaer + r.HeimToreRegulaer;
            if (teamA == teamB)
            {
                r.EntscheidungArt.Should().Be(EntscheidungArt.Elfmeterschiessen);
                r.SiegerMannschaftId.Should().NotBeNull();
            }
            else
            {
                r.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit,
                    "bei entschiedenem Gesamtergebnis gibt es kein Elfmeterschießen im Rückspiel");
                r.SiegerMannschaftId.Should().Be(teamA > teamB ? hin.HeimMannschaftId : hin.AuswaertsMannschaftId);
            }
        }

        // Plausibilitätscheck, dass beide Zweige tatsächlich durchlaufen wurden.
        spiele.Where(s => s.IstRueckspiel).Select(s => s.Ergebnis!.EntscheidungArt).Distinct()
            .Should().Contain([EntscheidungArt.RegulaereSpielzeit, EntscheidungArt.Elfmeterschiessen]);
    }

    // ── Issue #386: Platzhalter-Auflösung einmal je Block ────────────────────────────────

    [Fact]
    public async Task RundeSimulierenAsync_LoestFolgerundenPlatzhalterNachDemBlockAuf()
    {
        await using var ctx = NeuerContext();
        SetupAnlegen(ctx);
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Viertelfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, RegelwerkId = 1
        });
        ctx.Spiele.Add(new Spiel
        {
            Id = 1000, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 1, Spieltag = 1,
            HeimPlatzhalterName = "Sieger Achtelfinale 1", AuswaertsPlatzhalterName = "Sieger Achtelfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var service = ServiceFuer(ctx);

        await service.RundeSimulierenAsync(1, SimulationsModus.SchnellSimulation, seed: 386);

        // Paarung N = N-tes Hinspiel; den Gesamtsieger trägt dessen Rückspiel (Ids 2 und 4).
        var sieger = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.Id == 2 || s.Id == 4)
            .ToDictionary(s => s.Id, s => s.Ergebnis!.SiegerMannschaftId);
        var folgespiel = ctx.Spiele.AsNoTracking().Single(s => s.Id == 1000);
        folgespiel.HeimMannschaftId.Should().Be(sieger[2]);
        folgespiel.AuswaertsMannschaftId.Should().Be(sieger[4]);
    }

    // ── Issue #387: Hinspiele / Rückspiele einer K.-o.-Runde getrennt simulieren ─────────

    [Fact]
    public async Task RundenHaelfteSimulierenAsync_Hinspiele_SimuliertNurHinspiele()
    {
        await using var ctx = NeuerContext();
        SetupAnlegen(ctx);
        var service = ServiceFuer(ctx);

        int anzahl = await service.RundenHaelfteSimulierenAsync(1, istRueckrunde: false, SimulationsModus.SchnellSimulation, seed: 387);

        anzahl.Should().Be(PAARUNGEN);
        var spiele = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.TurnierRundeId == 1).ToList();
        spiele.Where(s => !s.IstRueckspiel).Should().OnlyContain(s => s.Ergebnis != null);
        spiele.Where(s => s.IstRueckspiel).Should().OnlyContain(s => s.Ergebnis == null);
    }

    [Fact]
    public async Task RundenHaelfteSimulierenAsync_Rueckspiele_UeberspringtRueckspieleMitOffenemHinspiel()
    {
        await using var ctx = NeuerContext();
        SetupAnlegen(ctx);
        var service = ServiceFuer(ctx);
        // Nur das Hinspiel der ersten Paarung ist gespielt.
        await service.SpielSimulierenAsync(1, SimulationsModus.SchnellSimulation, new Random(387));

        int anzahl = await service.RundenHaelfteSimulierenAsync(1, istRueckrunde: true, SimulationsModus.SchnellSimulation, seed: 387);

        anzahl.Should().Be(1, "nur das Rückspiel mit gespieltem Hinspiel ist simulierbar");
        var spiele = ctx.Spiele.Include(s => s.Ergebnis).Where(s => s.TurnierRundeId == 1).ToList();
        spiele.Single(s => s.HinspielId == 1).Ergebnis.Should().NotBeNull();
        spiele.Where(s => s.IstRueckspiel && s.HinspielId != 1).Should().OnlyContain(s => s.Ergebnis == null);
    }
}
