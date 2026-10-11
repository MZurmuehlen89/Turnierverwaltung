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
using Fussballturnierverwaltung.Tests.Spielplan;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Issue #352: neutraler Spielort und Gastgeber in der Turnier-Simulation.</summary>
public class SimulationsServiceSpielortTests
{
    private const int SPIELE = 300;

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SimulationsService ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var spielRepo = new SpielRepository(ctx);
        var instanzRepo = new TurnierInstanzRepository(ctx);
        var dienste = new SpielplanDienste(ctx);
        var ergebnisService = new SpielErgebnisService(
            spielRepo, new SpielErgebnisValidator(), dienste.Spielplan, dienste.Aufloesung, NullLogger<SpielErgebnisService>.Instance);
        var baumService = new TurnierBaumService(
            new TurnierBaumRepository(ctx), instanzRepo, spielRepo, dienste.Aufloesung, dienste.Quellverknuepfung,
            NullLogger<TurnierBaumService>.Instance);
        return new SimulationsService(
            spielRepo, instanzRepo, ergebnisService, baumService,
            new SimulationsLaufRepository(ctx), NullLogger<SimulationsService>.Instance);
    }

    /// <summary>Eine Runde mit <see cref="SPIELE"/> Partien gleich starker Teams mit großem Abstand zwischen
    /// Heim- und Auswärtsstärke; optional sind alle Auswärtsteams Gastgeber des Turniers.</summary>
    private static void SetupAnlegen(TurnierverwaltungDbContext ctx, bool neutral, bool gastIstGastgeber)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1,
            Rundenart = Rundenart.Gruppenphase, RegelwerkId = 1, NeutralerSpielort = neutral
        });

        for (int i = 0; i < SPIELE; i++)
        {
            int heim = 2 * i + 1, gast = 2 * i + 2;
            foreach (var id in new[] { heim, gast })
                ctx.Mannschaften.Add(new Mannschaft
                {
                    Id = id, Name = $"Team {id}", Kurzname = $"T{id}", LandId = 1,
                    OffensivRating = 70, MittelfeldRating = 70, DefensivRating = 70,
                    Heimstaerke = 100, Auswaertsstaerke = 50
                });
            if (gastIstGastgeber)
                ctx.TurnierGastgeber.Add(new TurnierGastgeber { TurnierInstanzId = 1, MannschaftId = gast });

            ctx.Spiele.Add(new Spiel
            {
                Id = i + 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = i + 1, Spieltag = 1,
                HeimMannschaftId = heim, AuswaertsMannschaftId = gast, Status = SpielStatus.Geplant
            });
        }
        ctx.SaveChanges();
    }

    private static async Task<(int HeimTore, int GastTore)> RundeSimulierenAsync(bool neutral, bool gastIstGastgeber)
    {
        await using var ctx = NeuerContext();
        SetupAnlegen(ctx, neutral, gastIstGastgeber);

        await ServiceFuer(ctx).RundeSimulierenAsync(1, SimulationsModus.SchnellSimulation, seed: 352);

        var ergebnisse = ctx.SpielErgebnisse.ToList();
        ergebnisse.Should().HaveCount(SPIELE);
        return (ergebnisse.Sum(e => e.HeimToreRegulaer), ergebnisse.Sum(e => e.AuswaertsToreRegulaer));
    }

    [Fact]
    public async Task OhneNeutralenSpielort_HeimteamHatVorteil()
    {
        var (heim, gast) = await RundeSimulierenAsync(neutral: false, gastIstGastgeber: true);

        heim.Should().BeGreaterThan((int)(gast * 1.5), "Gastgeber zählen nur auf neutralem Platz");
    }

    [Fact]
    public async Task NeutralerSpielort_KeinHeimvorteil()
    {
        var (heim, gast) = await RundeSimulierenAsync(neutral: true, gastIstGastgeber: false);

        // 25 % statt enger: das Poisson-Rauschen bei 300 Spielen liegt bei ~7 %, ein Heimvorteil bei 100/50 bei ~66 %.
        Math.Abs(heim - gast).Should().BeLessThan((int)(0.25 * (heim + gast) / 2));
    }

    [Fact]
    public async Task NeutralerSpielort_GastgeberAlsGastHatVorteil()
    {
        var (heim, gast) = await RundeSimulierenAsync(neutral: true, gastIstGastgeber: true);

        gast.Should().BeGreaterThan((int)(heim * 1.5));
    }
}
