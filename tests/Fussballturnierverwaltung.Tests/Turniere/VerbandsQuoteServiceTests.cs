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

public class VerbandsQuoteServiceTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static VerbandsQuoteService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new VerbandsQuoteRepository(ctx), new VerbandsQuoteValidator(), NullLogger<VerbandsQuoteService>.Instance);

    private static void GrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int turnierInstanzId, out int verbandId, out int zweiterVerbandId)
    {
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = 1, Name = "WM", SaisonOderJahr = "2026" });
        ctx.Verbaende.AddRange(
            new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" },
            new Verband { Id = 2, Name = "CONMEBOL", Kurzname = "CONMEBOL" });
        ctx.SaveChanges();
        turnierInstanzId = 1;
        verbandId = 1;
        zweiterVerbandId = 2;
    }

    [Fact]
    public async Task ErstellenAsync_GueltigeEingabe_LegtQuoteAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);

        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 8, MinAnzahl: 2));

        quote.Id.Should().BeGreaterThan(0);
        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ErstellenAsync_DuplikatVerband_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 8, MinAnzahl: null));

        var eingabe = new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 4, MinAnzahl: null);

        await Assert.ThrowsAsync<ValidationException>(() => service.ErstellenAsync(eingabe));
    }

    [Fact]
    public async Task AktualisierenAsync_AendertMaxAnzahl()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 8, MinAnzahl: null));

        await service.AktualisierenAsync(quote.Id, new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 6, MinAnzahl: 1));

        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Single().MaxAnzahl.Should().Be(6);
        quoten.Single().MinAnzahl.Should().Be(1);
    }

    [Fact]
    public async Task AktualisierenAsync_EigeneQuote_WirdNichtAlsDuplikatAbgelehnt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 8, MinAnzahl: null));

        await service.AktualisierenAsync(quote.Id, new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 9, MinAnzahl: null));

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Single().MaxAnzahl.Should().Be(9);
    }

    [Fact]
    public async Task LoeschenAsync_EntferntQuote()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 8, MinAnzahl: null));

        await service.LoeschenAsync(quote.Id);

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().BeEmpty();
    }

    [Fact]
    public async Task ZweiVerbaendeImSelbenTurnier_BeideAnlegbar()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out int zweiterVerbandId);
        var service = ServiceFuer(ctx);

        await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, MaxAnzahl: 8, MinAnzahl: null));
        await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, zweiterVerbandId, MaxAnzahl: 6, MinAnzahl: null));

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(2);
    }

    // ── QuotenErsetzenAsync (WPF-Turnierkopf-Grid, Issue #161) ────────────────────────────────

    [Fact]
    public async Task QuotenErsetzenAsync_LegtMehrereQuotenInEinemAufrufAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out int zweiterVerbandId);
        var service = ServiceFuer(ctx);

        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(verbandId, MaxAnzahl: 8, MinAnzahl: 2),
            new VerbandsQuoteZeileEingabe(zweiterVerbandId, MaxAnzahl: 4, MinAnzahl: null)
        };

        await service.QuotenErsetzenAsync(turnierInstanzId, zeilen);

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task QuotenErsetzenAsync_ErsetztBestehendeListeVollstaendig()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out int zweiterVerbandId);
        var service = ServiceFuer(ctx);
        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new VerbandsQuoteZeileEingabe(verbandId, MaxAnzahl: 8, MinAnzahl: null)]);

        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new VerbandsQuoteZeileEingabe(zweiterVerbandId, MaxAnzahl: 5, MinAnzahl: null)]);

        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Should().ContainSingle(q => q.VerbandId == zweiterVerbandId);
        quoten.Should().NotContain(q => q.VerbandId == verbandId);
    }

    [Fact]
    public async Task QuotenErsetzenAsync_UngueltigeListe_WirftValidationExceptionUndLaesstBestandUnveraendert()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new VerbandsQuoteZeileEingabe(verbandId, MaxAnzahl: 8, MinAnzahl: null)]);

        var ungueltigeZeilen = new[] { new VerbandsQuoteZeileEingabe(verbandId, MaxAnzahl: 0, MinAnzahl: null) };

        await Assert.ThrowsAsync<ValidationException>(() => service.QuotenErsetzenAsync(turnierInstanzId, ungueltigeZeilen));
        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Should().ContainSingle(q => q.VerbandId == verbandId && q.MaxAnzahl == 8);
    }
}
