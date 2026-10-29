using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class LandesQuoteServiceTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static LandesQuoteService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new LandesQuoteRepository(ctx), new LandRepository(ctx), new LandesQuoteValidator(),
            NullLogger<LandesQuoteService>.Instance);

    private static void GrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int turnierInstanzId, out int landId, out int zweitesLandId)
    {
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = 1, Name = "Klub-WM", SaisonOderJahr = "2026" });
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" });
        ctx.Ranglisten.Add(new Rangliste
        {
            Id = 1, Name = "Ligapyramide Deutschland",
            Abschnitte =
            [
                new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 18 },
                new RanglistenAbschnitt { Name = "2. Bundesliga", Spielklasse = 2, BisPlatz = 36 },
            ],
        });
        ctx.Laender.AddRange(
            new Land { Id = 1, Name = "Deutschland", Kurzname = "GER", FifaCode = "GER", VerbandId = 1, RanglisteId = 1 },
            new Land { Id = 2, Name = "England", Kurzname = "ENG", FifaCode = "ENG", VerbandId = 1 });
        ctx.SaveChanges();
        turnierInstanzId = 1;
        landId = 1;
        zweitesLandId = 2;
    }

    // ── QuotenErsetzenAsync (WinUi-Turnierkopf-Grid, Issue #189) ───────────────────────────

    [Fact]
    public async Task QuotenErsetzenAsync_LegtMehrereQuotenInEinemAufrufAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out int zweitesLandId);
        var service = ServiceFuer(ctx);

        var zeilen = new[]
        {
            new LandesQuoteZeileEingabe(landId, Anzahl: 3),
            new LandesQuoteZeileEingabe(zweitesLandId, Anzahl: 4)
        };

        await service.QuotenErsetzenAsync(turnierInstanzId, zeilen);

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task QuotenErsetzenAsync_ErsetztBestehendeListeVollstaendig()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out int zweitesLandId);
        var service = ServiceFuer(ctx);
        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new LandesQuoteZeileEingabe(landId, Anzahl: 3)]);

        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new LandesQuoteZeileEingabe(zweitesLandId, Anzahl: 5)]);

        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Should().ContainSingle(q => q.LandId == zweitesLandId);
        quoten.Should().NotContain(q => q.LandId == landId);
    }

    [Fact]
    public async Task QuotenErsetzenAsync_UngueltigeListe_WirftValidationExceptionUndLaesstBestandUnveraendert()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);
        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new LandesQuoteZeileEingabe(landId, Anzahl: 3)]);

        var ungueltigeZeilen = new[] { new LandesQuoteZeileEingabe(landId, Anzahl: 0) };

        await Assert.ThrowsAsync<ValidationException>(() => service.QuotenErsetzenAsync(turnierInstanzId, ungueltigeZeilen));
        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Should().ContainSingle(q => q.LandId == landId && q.Anzahl == 3);
    }

    [Fact]
    public async Task QuotenErsetzenAsync_SummeUeberKapazitaet_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out int zweitesLandId);
        var service = ServiceFuer(ctx);

        var zeilen = new[]
        {
            new LandesQuoteZeileEingabe(landId, Anzahl: 8),
            new LandesQuoteZeileEingabe(zweitesLandId, Anzahl: 10)
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.QuotenErsetzenAsync(turnierInstanzId, zeilen, eingangsKapazitaet: 16));
    }

    // ── Kontingente (#572) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task QuotenErsetzenAsync_MehrereKontingenteEinesLandes_WerdenGespeichert()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);

        await service.QuotenErsetzenAsync(turnierInstanzId,
        [
            new LandesQuoteZeileEingabe(landId, Anzahl: 4, Spielklasse: 1, AbPlatz: 1),
            new LandesQuoteZeileEingabe(landId, Anzahl: 18, Spielklasse: 2),
        ]);

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId))
            .Select(q => (q.Spielklasse, q.AbPlatz, q.Anzahl))
            .Should().BeEquivalentTo([((int?)1, (int?)1, 4), (2, 1, 18)]);
    }

    [Fact]
    public async Task QuotenErsetzenAsync_GleichesKontingent_BehaeltDieZeileUndAendertDieAnzahl()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);
        var vorher = await service.QuotenErsetzenAsync(turnierInstanzId,
            [new LandesQuoteZeileEingabe(landId, Anzahl: 4, Spielklasse: 1, AbPlatz: 5)]);

        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new LandesQuoteZeileEingabe(landId, Anzahl: 2, Spielklasse: 1, AbPlatz: 5)]);

        var quote = (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Single();
        (quote.Id, quote.Anzahl).Should().Be((vorher.Single().Id, 2));
    }

    [Fact]
    public async Task QuotenErsetzenAsync_SpielklasseOhneLigapyramide_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out _, out int zweitesLandId);
        var service = ServiceFuer(ctx);

        await Assert.ThrowsAsync<ValidationException>(() => service.QuotenErsetzenAsync(turnierInstanzId,
            [new LandesQuoteZeileEingabe(zweitesLandId, Anzahl: 4, Spielklasse: 1)]));
    }
}
