using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
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
        new(new LandesQuoteRepository(ctx), new LandesQuoteValidator(), NullLogger<LandesQuoteService>.Instance);

    private static void GrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int turnierInstanzId, out int landId, out int zweitesLandId)
    {
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = 1, Name = "Klub-WM", SaisonOderJahr = "2026" });
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" });
        ctx.Laender.AddRange(
            new Land { Id = 1, Name = "Deutschland", Kurzname = "GER", FifaCode = "GER", VerbandId = 1 },
            new Land { Id = 2, Name = "England", Kurzname = "ENG", FifaCode = "ENG", VerbandId = 1 });
        ctx.SaveChanges();
        turnierInstanzId = 1;
        landId = 1;
        zweitesLandId = 2;
    }

    [Fact]
    public async Task ErstellenAsync_GueltigeEingabe_LegtQuoteAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);

        var quote = await service.ErstellenAsync(new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 3));

        quote.Id.Should().BeGreaterThan(0);
        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ErstellenAsync_DuplikatLand_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);
        await service.ErstellenAsync(new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 3));

        var eingabe = new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 4);

        await Assert.ThrowsAsync<ValidationException>(() => service.ErstellenAsync(eingabe));
    }

    [Fact]
    public async Task AktualisierenAsync_AendertAnzahl()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 3));

        await service.AktualisierenAsync(quote.Id, new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 2));

        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Single().Anzahl.Should().Be(2);
    }

    [Fact]
    public async Task AktualisierenAsync_EigeneQuote_WirdNichtAlsDuplikatAbgelehnt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 3));

        await service.AktualisierenAsync(quote.Id, new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 4));

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Single().Anzahl.Should().Be(4);
    }

    [Fact]
    public async Task LoeschenAsync_EntferntQuote()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 3));

        await service.LoeschenAsync(quote.Id);

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().BeEmpty();
    }

    [Fact]
    public async Task ZweiLaenderImSelbenTurnier_BeideAnlegbar()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int landId, out int zweitesLandId);
        var service = ServiceFuer(ctx);

        await service.ErstellenAsync(new LandesQuoteEingabe(turnierInstanzId, landId, Anzahl: 3));
        await service.ErstellenAsync(new LandesQuoteEingabe(turnierInstanzId, zweitesLandId, Anzahl: 4));

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(2);
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
}
