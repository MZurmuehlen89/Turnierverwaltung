using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class UebergangsregelRepositoryTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void GrundSetupAnlegen(TurnierverwaltungDbContext ctx, out int ligaRundeId, out int koRundeId)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        var liga = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga-Phase", Reihenfolge = 1,
            Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = 36, AusgangsMannschaftsanzahl = 24,
            RegelwerkId = 1
        };
        var ko = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Achtelfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 16, AusgangsMannschaftsanzahl = 8,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(liga, ko);
        ctx.SaveChanges();

        ligaRundeId = liga.Id;
        koRundeId = ko.Id;
    }

    [Fact]
    public async Task HinzufuegenUndFindenNachId_LiefertGespeicherteRegel()
    {
        using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out var ligaRundeId, out var koRundeId);
        var repo = new UebergangsregelRepository(ctx);

        var regel = new Uebergangsregel
        {
            QuellRundeId = ligaRundeId, ZielRundeId = koRundeId,
            PlatzierungVon = 1, PlatzierungBis = 8
        };
        await repo.HinzufuegenAsync(regel);

        var gefunden = await repo.FindenNachIdAsync(regel.Id);

        gefunden.Should().NotBeNull();
        gefunden!.PlatzierungVon.Should().Be(1);
        gefunden.PlatzierungBis.Should().Be(8);
    }

    [Fact]
    public async Task AlleNachQuellRundeIdAsync_LiefertNurRegelnDieserQuellrundeSortiertNachPlatzierung()
    {
        using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out var ligaRundeId, out var koRundeId);
        var repo = new UebergangsregelRepository(ctx);

        await repo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 9, PlatzierungBis = 24 });
        await repo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 1, PlatzierungBis = 8 });
        // Regel einer anderen Quellrunde darf nicht mitgeliefert werden.
        await repo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = koRundeId, ZielRundeId = ligaRundeId, PlatzierungVon = 1, PlatzierungBis = 1 });

        var regeln = await repo.AlleNachQuellRundeIdAsync(ligaRundeId);

        regeln.Should().HaveCount(2);
        regeln.Select(r => r.PlatzierungVon).Should().ContainInOrder(1, 9);
        regeln.Should().OnlyContain(r => r.ZielRunde != null);
    }

    [Fact]
    public async Task AktualisierenAsync_SpeichertGeaendertePlatzierung()
    {
        using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out var ligaRundeId, out var koRundeId);
        var repo = new UebergangsregelRepository(ctx);
        var regel = new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 1, PlatzierungBis = 8 };
        await repo.HinzufuegenAsync(regel);

        regel.PlatzierungBis = 10;
        await repo.AktualisierenAsync(regel);

        var neuGeladen = await repo.FindenNachIdAsync(regel.Id);
        neuGeladen!.PlatzierungBis.Should().Be(10);
    }

    [Fact]
    public async Task LoeschenAsync_EntferntRegel()
    {
        using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out var ligaRundeId, out var koRundeId);
        var repo = new UebergangsregelRepository(ctx);
        var regel = new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 1, PlatzierungBis = 8 };
        await repo.HinzufuegenAsync(regel);

        await repo.LoeschenAsync(regel);

        (await repo.FindenNachIdAsync(regel.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AlleNachZielRundeIdAsync_LiefertNurRegelnDieserZielrundeMitQuellrundeGeladen()
    {
        using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out var ligaRundeId, out var koRundeId);
        var repo = new UebergangsregelRepository(ctx);

        await repo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 1, PlatzierungBis = 8 });
        // Regel mit anderer Zielrunde darf nicht mitgeliefert werden.
        await repo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = koRundeId, ZielRundeId = ligaRundeId, PlatzierungVon = 1, PlatzierungBis = 1 });

        var regeln = await repo.AlleNachZielRundeIdAsync(koRundeId);

        regeln.Should().HaveCount(1);
        regeln[0].QuellRunde.Should().NotBeNull();
        regeln[0].QuellRunde.Id.Should().Be(ligaRundeId);
    }

    [Fact]
    public async Task AlleNachTurnierInstanzIdAsync_LiefertAlleRegelnDesTurniers()
    {
        using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out var ligaRundeId, out var koRundeId);
        var repo = new UebergangsregelRepository(ctx);

        await repo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 1, PlatzierungBis = 8 });
        await repo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 9, PlatzierungBis = 24 });

        var regeln = await repo.AlleNachTurnierInstanzIdAsync(1);

        regeln.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindenNachIdMitRundenAsync_LaedtQuellUndZielrunde()
    {
        using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out var ligaRundeId, out var koRundeId);
        var repo = new UebergangsregelRepository(ctx);
        var regel = new Uebergangsregel
            { QuellRundeId = ligaRundeId, ZielRundeId = koRundeId, PlatzierungVon = 1, PlatzierungBis = 8 };
        await repo.HinzufuegenAsync(regel);

        var geladen = await repo.FindenNachIdMitRundenAsync(regel.Id);

        geladen.Should().NotBeNull();
        geladen!.QuellRunde.Should().NotBeNull();
        geladen.ZielRunde.Should().NotBeNull();
    }
}
