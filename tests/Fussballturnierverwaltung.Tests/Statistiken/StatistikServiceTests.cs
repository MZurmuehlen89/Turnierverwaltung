using FluentAssertions;
using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Statistiken;

public class StatistikServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private const int INSTANZ = 1;

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static StatistikService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new SpielRepository(ctx), new TurnierRundeRepository(ctx), new RegelwerkRepository(ctx));

    private static void RegelwerkAnlegen(TurnierverwaltungDbContext ctx, int id, int punkteFuerSieg)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = id, Name = $"Regelwerk {id}" });
        ctx.PunkteRegeln.Add(new PunkteRegel
        {
            Id = id, RegelwerkId = id,
            PunkteFuerSieg = punkteFuerSieg, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0
        });
    }

    private static void StammdatenAnlegen(TurnierverwaltungDbContext ctx, int? standardRegelwerkId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Deutschland", Kurzname = "GER", FifaCode = "GER", VerbandId = 1 });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = INSTANZ, Name = "Turnier", StandardRegelwerkId = standardRegelwerkId });
    }

    private static void RundeMitHeimsiegAnlegen(TurnierverwaltungDbContext ctx, int id, int? regelwerkId)
    {
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = id, TurnierInstanzId = INSTANZ, Name = $"Runde {id}", Reihenfolge = id, RegelwerkId = regelwerkId
        });
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = INSTANZ, TurnierRundeId = id, Spielnummer = id,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis { Id = id, SpielId = id, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0 });
    }

    private static int PunkteVonTeamA(IReadOnlyList<MannschaftsSpiel> spiele)
        => spiele.Where(s => s.Mannschaft.Id == 1).Sum(s => s.Punkte);

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MannschaftsSpieleLaden_RundenregelwerkUeberschreibtStandardRegelwerk()
    {
        using var ctx = NeuerContext();
        RegelwerkAnlegen(ctx, id: 1, punkteFuerSieg: 3);
        RegelwerkAnlegen(ctx, id: 2, punkteFuerSieg: 2);
        StammdatenAnlegen(ctx, standardRegelwerkId: 1);
        RundeMitHeimsiegAnlegen(ctx, id: 1, regelwerkId: null);
        RundeMitHeimsiegAnlegen(ctx, id: 2, regelwerkId: 2);
        ctx.SaveChanges();

        var spiele = await ServiceFuer(ctx).MannschaftsSpieleLadenAsync(INSTANZ);

        spiele.Should().HaveCount(4);
        PunkteVonTeamA(spiele).Should().Be(5);
    }

    [Fact]
    public async Task MannschaftsSpieleLaden_OhneRegelwerk_GiltDreiEinsNull()
    {
        using var ctx = NeuerContext();
        StammdatenAnlegen(ctx, standardRegelwerkId: null);
        RundeMitHeimsiegAnlegen(ctx, id: 1, regelwerkId: null);
        ctx.SaveChanges();

        var spiele = await ServiceFuer(ctx).MannschaftsSpieleLadenAsync(INSTANZ);

        PunkteVonTeamA(spiele).Should().Be(3);
    }
}
