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

        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 8));

        quote.Id.Should().BeGreaterThan(0);
        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ErstellenAsync_DuplikatVerband_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 8));

        var eingabe = new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 4);

        await Assert.ThrowsAsync<ValidationException>(() => service.ErstellenAsync(eingabe));
    }

    [Fact]
    public async Task AktualisierenAsync_AendertAnzahl()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 8));

        await service.AktualisierenAsync(quote.Id, new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 6));

        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Single().Anzahl.Should().Be(6);
    }

    [Fact]
    public async Task AktualisierenAsync_EigeneQuote_WirdNichtAlsDuplikatAbgelehnt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 8));

        await service.AktualisierenAsync(quote.Id, new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 9));

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Single().Anzahl.Should().Be(9);
    }

    [Fact]
    public async Task LoeschenAsync_EntferntQuote()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out _);
        var service = ServiceFuer(ctx);
        var quote = await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 8));

        await service.LoeschenAsync(quote.Id);

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().BeEmpty();
    }

    [Fact]
    public async Task ZweiVerbaendeImSelbenTurnier_BeideAnlegbar()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out int zweiterVerbandId);
        var service = ServiceFuer(ctx);

        await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, verbandId, Anzahl: 8));
        await service.ErstellenAsync(new VerbandsQuoteEingabe(turnierInstanzId, zweiterVerbandId, Anzahl: 6));

        (await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId)).Should().HaveCount(2);
    }

    // ── QuotenErsetzenAsync (WPF-Turnierkopf-Grid, Issue #161/#154) ───────────────────────────

    [Fact]
    public async Task QuotenErsetzenAsync_LegtMehrereQuotenInEinemAufrufAn()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out int zweiterVerbandId);
        var service = ServiceFuer(ctx);

        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(verbandId, Anzahl: 8),
            new VerbandsQuoteZeileEingabe(zweiterVerbandId, Anzahl: 4)
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
            [new VerbandsQuoteZeileEingabe(verbandId, Anzahl: 8)]);

        await service.QuotenErsetzenAsync(turnierInstanzId,
            [new VerbandsQuoteZeileEingabe(zweiterVerbandId, Anzahl: 5)]);

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
            [new VerbandsQuoteZeileEingabe(verbandId, Anzahl: 8)]);

        var ungueltigeZeilen = new[] { new VerbandsQuoteZeileEingabe(verbandId, Anzahl: 0) };

        await Assert.ThrowsAsync<ValidationException>(() => service.QuotenErsetzenAsync(turnierInstanzId, ungueltigeZeilen));
        var quoten = await service.AlleNachTurnierInstanzIdAsync(turnierInstanzId);
        quoten.Should().ContainSingle(q => q.VerbandId == verbandId && q.Anzahl == 8);
    }

    [Fact]
    public async Task QuotenErsetzenAsync_SummeUeberKapazitaet_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int turnierInstanzId, out int verbandId, out int zweiterVerbandId);
        var service = ServiceFuer(ctx);

        var zeilen = new[]
        {
            new VerbandsQuoteZeileEingabe(verbandId, Anzahl: 8),
            new VerbandsQuoteZeileEingabe(zweiterVerbandId, Anzahl: 10)
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.QuotenErsetzenAsync(turnierInstanzId, zeilen, eingangsKapazitaet: 16));
    }
}
